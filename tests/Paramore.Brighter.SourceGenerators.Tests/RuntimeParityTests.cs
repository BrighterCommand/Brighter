using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.SourceGenerators.Tests;

/// <summary>
/// The round-trip test the whole feature's value rests on: compile a fixture assembly with the
/// generator applied, load it, run the generated <c>AddFromThisAssembly</c> against a real
/// <see cref="IServiceCollection"/>, and compare the resulting registrations with what the
/// assembly-scanning path produces for the same types. Emitted-text tests can't catch a parity
/// gap that compiles fine but registers less than scanning does (e.g. the framework's own
/// pipeline handlers); this can.
/// </summary>
public class RuntimeParityTests
{
    private const string FixtureSource = """
        using Paramore.Brighter;
        using Paramore.Brighter.Extensions.DependencyInjection;

        namespace ParityFixture;

        public class GreetingCommand : Command
        {
            public GreetingCommand() : base(System.Guid.NewGuid()) { }
        }

        public class GreetingEvent : Event
        {
            public GreetingEvent() : base(System.Guid.NewGuid()) { }
        }

        public class GreetingHandler : RequestHandler<GreetingCommand>
        {
            public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
        }

        public class GreetingHandlerAsync : RequestHandlerAsync<GreetingCommand>
        {
        }

        public class OpenHandler<T> : RequestHandler<T> where T : class, IRequest
        {
        }

        public class GreetingMapper : IAmAMessageMapper<GreetingEvent>
        {
            public IRequestContext? Context { get; set; }
            public Message MapToMessage(GreetingEvent request, Publication publication) => new();
            public GreetingEvent MapToRequest(Message message) => new();
        }

        public class GreetingMapperAsync : IAmAMessageMapperAsync<GreetingEvent>
        {
            public IRequestContext? Context { get; set; }
            public System.Threading.Tasks.Task<Message> MapToMessageAsync(GreetingEvent request, Publication publication, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(new Message());
            public System.Threading.Tasks.Task<GreetingEvent> MapToRequestAsync(Message message, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(new GreetingEvent());
        }

        public class NoOpTransform : IAmAMessageTransform
        {
            public IRequestContext? Context { get; set; }
            public void InitializeWrapFromAttributeParams(params object?[] init) { }
            public void InitializeUnwrapFromAttributeParams(params object?[] init) { }
            public Message Wrap(Message m, Publication p) => m;
            public Message Unwrap(Message m) => m;
            public void Dispose() { }
        }

        public static partial class Registrations
        {
            [BrighterRegistrations]
            public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
        }
        """;

    private static readonly Lazy<Assembly> s_fixture = new(CompileFixture);

    /// <summary>
    /// Compile the fixture with the generator applied and load the result. References come from
    /// the trusted platform assemblies, which for a framework-dependent test app include both the
    /// runtime and everything in the test's output directory (the Brighter assemblies included).
    /// </summary>
    private static Assembly CompileFixture()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(static path => !string.IsNullOrEmpty(path))
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "BrighterParityFixture",
            new[] { CSharpSyntaxTree.ParseText(FixtureSource) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new BrighterRegistrationsGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

        using var stream = new MemoryStream();
        var emit = updated.Emit(stream);
        Assert.True(emit.Success, string.Join(
            "\n", emit.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray());
    }

    /// <summary>Run the generated registration method against a fresh ServiceCollection.</summary>
    private static IServiceCollection RunGeneratedPath()
    {
        var services = new ServiceCollection();
        var builder = services.AddBrighter();

        var registrations = s_fixture.Value.GetType("ParityFixture.Registrations")!;
        var method = registrations.GetMethod("AddFromThisAssembly", BindingFlags.Public | BindingFlags.Static)!;
        method.Invoke(null, new object[] { builder });

        return services;
    }

    /// <summary>
    /// Run the assembly-scanning path over exactly the fixture assembly. This mirrors what
    /// AutoFromAssemblies does, minus its AppDomain sweep (which would be racy under a test
    /// runner that has other assemblies loaded).
    /// </summary>
    private static IServiceCollection RunScanningPath()
    {
        var services = new ServiceCollection();
        var builder = services.AddBrighter();

        var assemblies = new[] { s_fixture.Value };
        builder.MapperRegistryFromAssemblies(assemblies);
        builder.HandlersFromAssemblies(assemblies, null);
        builder.AsyncHandlersFromAssemblies(assemblies, null);
        builder.TransformsFromAssemblies(assemblies);

        return services;
    }

    [Fact]
    public void GeneratedRegistrations_ProduceSameServiceDescriptors_AsAssemblyScanning()
    {
        // This is what would have caught the framework-pipeline-handler gap: scanning registers
        // ExceptionPolicyHandler<>, RequestLoggingHandler<> etc. from the framework assembly, and
        // the generated method must land on the identical set of service registrations.
        var generated = Describe(RunGeneratedPath());
        var scanned = Describe(RunScanningPath());

        Assert.Equal(scanned, generated);
    }

    [Fact]
    public void GeneratedRegistrations_ProduceSameSubscriberRegistry_AsAssemblyScanning()
    {
        var generated = HandlersByRequestType(RunGeneratedPath());
        var scanned = HandlersByRequestType(RunScanningPath());

        Assert.Equal(scanned, generated);
    }

    [Fact]
    public void GeneratedRegistrations_ProduceSameMapperRegistry_AsAssemblyScanning()
    {
        var generatedMappers = MapperBuilder(RunGeneratedPath());
        var scannedMappers = MapperBuilder(RunScanningPath());

        Assert.Equal(DescribeMappers(scannedMappers.Mappers), DescribeMappers(generatedMappers.Mappers));
        Assert.Equal(DescribeMappers(scannedMappers.AsyncMappers), DescribeMappers(generatedMappers.AsyncMappers));
    }

    [Fact]
    public void DuplicateMapperForSameRequestType_Throws()
    {
        // Pins the duplicate-registration behaviour the generated `r.Add(typeof(X), ...)` calls
        // inherit: a second mapper for the same request type throws rather than last-write-wins.
        // The scanning path funnels through the same Add, so the two paths agree here too.
        var builder = new ServiceCollectionMessageMapperRegistryBuilder(new ServiceCollection());
        builder.Add(typeof(PinnedEvent), typeof(PinnedMapperA));

        var exception = Assert.Throws<ArgumentException>(
            () => builder.Add(typeof(PinnedEvent), typeof(PinnedMapperB)));

        Assert.Contains("already been registered", exception.Message);
    }

    /// <summary>Project a ServiceCollection to an order-independent, comparable description.</summary>
    private static string[] Describe(IServiceCollection services) =>
        services
            .Select(static d => string.Join("|",
                d.Lifetime,
                d.ServiceType.FullName,
                d.ImplementationType?.FullName
                    ?? d.ImplementationInstance?.GetType().FullName
                    ?? "factory"))
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

    private static string[] HandlersByRequestType(IServiceCollection services)
    {
        var inspector = (IAmASubscriberRegistryInspector)services
            .Single(static d => d.ServiceType == typeof(IAmASubscriberRegistryInspector))
            .ImplementationInstance!;

        return inspector.GetRegisteredRequestTypes()
            .SelectMany(request => inspector.GetHandlerTypes(request)
                .Select(handler => $"{request.FullName} -> {handler.FullName}"))
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();
    }

    private static ServiceCollectionMessageMapperRegistryBuilder MapperBuilder(IServiceCollection services) =>
        (ServiceCollectionMessageMapperRegistryBuilder)services
            .Single(static d => d.ServiceType == typeof(ServiceCollectionMessageMapperRegistryBuilder))
            .ImplementationInstance!;

    private static string[] DescribeMappers(IReadOnlyDictionary<Type, Type> mappers) =>
        mappers
            .Select(static kv => $"{kv.Key.FullName} -> {kv.Value.FullName}")
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

    // Deliberately NOT implementing IAmAMessageMapper<>: this test assembly's name starts with
    // "Paramore.Brighter", so AddBrighter's built-in assembly sweep would scan these types and
    // hit the duplicate-mapper conflict before any test logic ran. Add() doesn't validate the
    // mapper type, so plain classes still pin its duplicate-registration behaviour.
    public class PinnedEvent : Event
    {
        public PinnedEvent() : base(Guid.NewGuid()) { }
    }

    public class PinnedMapperA;

    public class PinnedMapperB;
}

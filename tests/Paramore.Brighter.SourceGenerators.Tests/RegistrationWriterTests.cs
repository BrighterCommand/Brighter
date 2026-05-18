using Paramore.Brighter.SourceGenerators;
using Paramore.Brighter.SourceGenerators.Model;

namespace Paramore.Brighter.SourceGenerators.Tests;

public class RegistrationWriterTests
{
    private static RegistrationModel EmptyModel(
        EquatableArray<HandlerEntry>? handlers = null,
        EquatableArray<HandlerEntry>? asyncHandlers = null,
        EquatableArray<MapperEntry>? mappers = null,
        EquatableArray<MapperEntry>? asyncMappers = null,
        EquatableArray<string>? transforms = null) =>
        new(
            Namespace: "MyApp",
            ContainingTypeAccessibility: "public",
            ContainingTypeName: "Registrations",
            ContainingTypeIsStatic: true,
            MethodAccessibility: "public",
            MethodName: "AddFromThisAssembly",
            ReturnTypeFullyQualified: "global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder",
            ParameterTypeFullyQualified: "global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder",
            ParameterName: "builder",
            IsExtensionMethod: true,
            Handlers: handlers ?? EquatableArray<HandlerEntry>.Empty,
            AsyncHandlers: asyncHandlers ?? EquatableArray<HandlerEntry>.Empty,
            Mappers: mappers ?? EquatableArray<MapperEntry>.Empty,
            AsyncMappers: asyncMappers ?? EquatableArray<MapperEntry>.Empty,
            Transforms: transforms ?? EquatableArray<string>.Empty,
            HintName: "MyApp_Registrations__AddFromThisAssembly.g.cs");

    [Fact]
    public void EmptyModel_EmitsScaffoldAndReturnsBuilder()
    {
        var output = RegistrationWriter.Write(EmptyModel());

        Assert.Contains("namespace MyApp", output);
        Assert.Contains("public static partial class Registrations", output);
        Assert.Contains("public static partial global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder AddFromThisAssembly(this global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder builder)", output);
        Assert.Contains("return builder;", output);
        Assert.DoesNotContain(".Handlers(", output);
        Assert.DoesNotContain(".MapperRegistry(", output);
        Assert.DoesNotContain(".Transforms(", output);
    }

    [Fact]
    public void ClosedHandler_EmitsRegistryAddWithFullyQualifiedTypes()
    {
        var model = EmptyModel(handlers: new EquatableArray<HandlerEntry>(new[]
        {
            new HandlerEntry("global::MyApp.GreetingCommand", "global::MyApp.GreetingCommandHandler", IsOpenGeneric: false)
        }));

        var output = RegistrationWriter.Write(model);

        Assert.Contains("builder.Handlers(r =>", output);
        Assert.Contains("var registry = (global::Paramore.Brighter.Extensions.DependencyInjection.ServiceCollectionSubscriberRegistry)r;", output);
        Assert.Contains("registry.Add(typeof(global::MyApp.GreetingCommand), typeof(global::MyApp.GreetingCommandHandler));", output);
    }

    [Fact]
    public void OpenGenericHandler_EmitsEnsureHandlerIsRegistered()
    {
        var model = EmptyModel(handlers: new EquatableArray<HandlerEntry>(new[]
        {
            new HandlerEntry(string.Empty, "global::MyApp.PolicyHandler<>", IsOpenGeneric: true)
        }));

        var output = RegistrationWriter.Write(model);

        Assert.Contains("registry.EnsureHandlerIsRegistered(typeof(global::MyApp.PolicyHandler<>));", output);
        Assert.DoesNotContain("registry.Add(typeof(", output);
    }

    [Fact]
    public void AsyncHandlerOnly_UsesAsyncHandlersCallback()
    {
        var model = EmptyModel(asyncHandlers: new EquatableArray<HandlerEntry>(new[]
        {
            new HandlerEntry("global::MyApp.GreetingCommand", "global::MyApp.GreetingCommandHandlerAsync", IsOpenGeneric: false)
        }));

        var output = RegistrationWriter.Write(model);

        Assert.Contains("builder.AsyncHandlers(r =>", output);
        Assert.DoesNotContain("builder.Handlers(r =>", output);
    }

    [Fact]
    public void Mappers_EmitsAddAndAddAsyncCalls()
    {
        var model = EmptyModel(
            mappers: new EquatableArray<MapperEntry>(new[]
            {
                new MapperEntry("global::MyApp.GreetingEvent", "global::MyApp.GreetingEventMapper")
            }),
            asyncMappers: new EquatableArray<MapperEntry>(new[]
            {
                new MapperEntry("global::MyApp.GreetingEvent", "global::MyApp.GreetingEventMapperAsync")
            }));

        var output = RegistrationWriter.Write(model);

        Assert.Contains("builder.MapperRegistry(r =>", output);
        Assert.Contains("r.Add(typeof(global::MyApp.GreetingEvent), typeof(global::MyApp.GreetingEventMapper));", output);
        Assert.Contains("r.AddAsync(typeof(global::MyApp.GreetingEvent), typeof(global::MyApp.GreetingEventMapperAsync));", output);
    }

    [Fact]
    public void Transforms_EmitsTransformsCallback()
    {
        var model = EmptyModel(transforms: new EquatableArray<string>(new[]
        {
            "global::MyApp.NoOpTransformer"
        }));

        var output = RegistrationWriter.Write(model);

        Assert.Contains("builder.Transforms(r =>", output);
        Assert.Contains("r.Add(typeof(global::MyApp.NoOpTransformer));", output);
    }

    [Fact]
    public void GlobalNamespace_OmitsNamespaceWrapper()
    {
        var model = EmptyModel() with { Namespace = null };

        var output = RegistrationWriter.Write(model);

        Assert.DoesNotContain("namespace ", output);
        Assert.Contains("public static partial class Registrations", output);
    }

    [Fact]
    public void NonStaticContainingType_EmitsPartialClassWithoutStatic()
    {
        var model = EmptyModel() with { ContainingTypeIsStatic = false, IsExtensionMethod = false };

        var output = RegistrationWriter.Write(model);

        Assert.Contains("public partial class Registrations", output);
        Assert.DoesNotContain("static partial class", output);
        Assert.DoesNotContain("this global::Paramore.Brighter", output);
    }
}

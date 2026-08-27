using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Paramore.Brighter.SourceGenerators.Tests;

/// <summary>
/// Exercises the auto-registration pipeline: gated on the BrighterAutoRegistration MSBuild
/// property (made CompilerVisible by the build/ props file shipped with the package).
/// </summary>
public class AutoRegistrationTests
{
    private const string UserSource = """
        using Paramore.Brighter;
        using Paramore.Brighter.Extensions.DependencyInjection;

        namespace App;

        public class GreetingCommand : Command
        {
            public GreetingCommand() : base(System.Guid.NewGuid()) { }
        }

        public class GreetingHandler : RequestHandler<GreetingCommand>
        {
            public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
        }
        """;

    private const string UserSourceWithManualRegistration = """
        using Paramore.Brighter;
        using Paramore.Brighter.Extensions.DependencyInjection;

        namespace App;

        public class GreetingCommand : Command
        {
            public GreetingCommand() : base(System.Guid.NewGuid()) { }
        }

        public class GreetingHandler : RequestHandler<GreetingCommand>
        {
            public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
        }

        public static partial class Registrations
        {
            [BrighterRegistrations]
            public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
        }
        """;

    private static readonly MetadataReference CoreReference =
        MetadataReference.CreateFromFile(typeof(Paramore.Brighter.IRequest).Assembly.Location);

    private static readonly MetadataReference DependencyInjectionReference =
        MetadataReference.CreateFromFile(typeof(Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder).Assembly.Location);

    private static readonly MetadataReference[] BaseReferences =
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
    };

    private static GeneratorDriverRunResult Run(string source, string? autoRegistrationValue) =>
        Run(source, autoRegistrationValue, BaseReferences.Concat(new[] { CoreReference, DependencyInjectionReference }).ToArray());

    private static GeneratorDriverRunResult Run(string source, string? autoRegistrationValue, MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            "TestAsm",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var globalOptions = autoRegistrationValue is null
            ? ImmutableDictionary<string, string>.Empty
            : ImmutableDictionary<string, string>.Empty
                .Add("build_property.BrighterAutoRegistration", autoRegistrationValue);

        var optionsProvider = new StubOptionsProvider(globalOptions);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new BrighterRegistrationsGenerator().AsSourceGenerator() },
            additionalTexts: ImmutableArray<AdditionalText>.Empty,
            parseOptions: CSharpParseOptions.Default,
            optionsProvider: optionsProvider);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    [Fact]
    public void PropertyTrue_GeneratesBrighterAssemblyRegistrations()
    {
        var result = Run(UserSource, "true");

        var single = result.Results.Single();
        Assert.Empty(single.Diagnostics);

        var generated = single.GeneratedSources
            .Single(g => g.HintName == "BrighterAssemblyRegistrations__AddFromThisAssembly.g.cs")
            .SourceText.ToString();

        Assert.Contains("namespace Paramore.Brighter.Extensions.DependencyInjection", generated);
        Assert.Contains("internal static class BrighterAssemblyRegistrations", generated);
        Assert.Contains("internal static global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder AddFromThisAssembly", generated);
        Assert.Contains("r.Register<global::App.GreetingCommand, global::App.GreetingHandler>();", generated);
        Assert.DoesNotContain("partial", generated);
    }

    [Fact]
    public void PropertyFalse_DoesNotGenerateAutoClass()
    {
        var result = Run(UserSource, "false");

        Assert.DoesNotContain(
            result.Results.Single().GeneratedSources,
            g => g.HintName == "BrighterAssemblyRegistrations__AddFromThisAssembly.g.cs");
    }

    [Fact]
    public void PropertyMissing_DoesNotGenerateAutoClass()
    {
        // Simulates a transitive consumer that picked up the analyzer but not the build/ props.
        var result = Run(UserSource, autoRegistrationValue: null);

        Assert.DoesNotContain(
            result.Results.Single().GeneratedSources,
            g => g.HintName == "BrighterAssemblyRegistrations__AddFromThisAssembly.g.cs");
    }

    [Fact]
    public void PropertyTrue_WithManualRegistrationMethod_SuppressesAutoClass()
    {
        // Auto-on + a hand-written [BrighterRegistrations] method would otherwise double-register
        // (and collide on the AddFromThisAssembly name). The manual method wins; auto is suppressed.
        var single = Run(UserSourceWithManualRegistration, "true").Results.Single();

        Assert.DoesNotContain(
            single.GeneratedSources,
            g => g.HintName == "BrighterAssemblyRegistrations__AddFromThisAssembly.g.cs");

        // The user's own registration method is still implemented.
        Assert.Contains(
            single.GeneratedSources,
            g => g.HintName.StartsWith("App_Registrations") && g.HintName.EndsWith("__AddFromThisAssembly.g.cs"));

        // ...and the user is told why the auto class vanished (BRGEN007, info-level).
        Assert.Contains(single.Diagnostics, d => d.Id == "BRGEN007");
    }

    [Fact]
    public void GeneratedSource_HasNoNullableDirective()
    {
        // "#nullable enable" is an error (CS8370) below C# 8, which is the *default* LangVersion for
        // netstandard2.0 and net48 projects — and a consumer cannot edit generated source to remove
        // it. The emitted code carries no nullable annotations, so the directive buys nothing.
        var generated = Run(UserSource, "true").Results.Single()
            .GeneratedSources.Single().SourceText.ToString();

        Assert.DoesNotContain("#nullable", generated);
    }

    [Fact]
    public void Generator_DoesNotEmitTheMarkerAttributes()
    {
        // [BrighterRegistrations] / [ExcludeFromBrighterRegistration] live in core Brighter. Emitting
        // them per-assembly conflicts under InternalsVisibleTo (CS0436) and pushes a generated file
        // into every transitive consumer of a library that uses the generator.
        var sources = Run(UserSource, "true").Results.Single().GeneratedSources;

        Assert.DoesNotContain(sources, g => g.HintName == "BrighterRegistrationsAttributes.g.cs");
    }

    [Fact]
    public void PropertyTrue_WithoutDependencyInjectionPackage_ReportsBRGEN010AndGeneratesNothing()
    {
        // The layered case: a domain library that references core Brighter (where IHandleRequests
        // lives) but not the DI package. Discovery needs IBrighterBuilder, so nothing can be
        // generated — and saying nothing leaves the project building clean with no registrations.
        var single = Run(UserSource, "true", BaseReferences.Concat(new[] { CoreReference }).ToArray())
            .Results.Single();

        Assert.Contains(single.Diagnostics, d => d.Id == "BRGEN010");
        Assert.Empty(single.GeneratedSources);
    }

    [Fact]
    public void PropertyTrue_WithNoBrighterTypes_DoesNotGenerateAutoClass()
    {
        // An empty AddFromThisAssembly() compiles, reads like it registered the solution's handlers,
        // and registers nothing — the silent miss this generator exists to prevent. Better to leave
        // the call site failing to compile.
        const string source = """
            namespace App;

            public class NotABrighterType
            {
            }
            """;

        var single = Run(source, "true").Results.Single();

        Assert.Empty(single.GeneratedSources);
    }

    [Fact]
    public void PropertyTrue_WhenAnotherAssemblyExposesTheAutoClass_ReportsBRGEN011()
    {
        // A library and its test project see each other's internals, so both copies of the auto
        // class are in scope and every AddFromThisAssembly() call is ambiguous (CS0121). The raw
        // compiler error names the same signature twice and gives no way out.
        var single = Run(UserSource, "true", BaseReferences
            .Concat(new[] { CoreReference, DependencyInjectionReference, LibraryExposingAutoClass() })
            .ToArray())
            .Results.Single();

        var diagnostic = Assert.Single(single.Diagnostics, d => d.Id == "BRGEN011");
        Assert.Contains("OtherAsm", diagnostic.GetMessage());
    }

    [Fact]
    public void PropertyTrue_WithTwoHandlersForOneCommand_ReportsBRGEN012()
    {
        const string source = """
            using Paramore.Brighter;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public class GreetingHandler : RequestHandler<GreetingCommand> { }
            public class RivalGreetingHandler : RequestHandler<GreetingCommand> { }
            """;

        var diagnostic = Assert.Single(Run(source, "true").Results.Single().Diagnostics, d => d.Id == "BRGEN012");

        Assert.Contains("GreetingCommand", diagnostic.GetMessage());
        Assert.Contains("GreetingHandler", diagnostic.GetMessage());
        Assert.Contains("RivalGreetingHandler", diagnostic.GetMessage());
    }

    [Fact]
    public void PropertyTrue_WithTwoHandlersForABareRequest_ReportsBRGEN012()
    {
        // A request that implements IRequest directly is neither command nor event, but Send still
        // insists on exactly one handler — so the duplicate is just as broken, and keying the check
        // on ICommand missed it.
        const string source = """
            using Paramore.Brighter;

            namespace App;

            public class BareRequest : IRequest
            {
                public Id Id { get; set; } = Id.Random();
                public Id? CorrelationId { get; set; }
                public int HandledCount { get; set; }
                public System.Diagnostics.Activity? Span { get; set; }
            }

            public class BareHandler : RequestHandler<BareRequest> { }
            public class RivalBareHandler : RequestHandler<BareRequest> { }
            """;

        var diagnostic = Assert.Single(Run(source, "true").Results.Single().Diagnostics, d => d.Id == "BRGEN012");

        Assert.Contains("BareRequest", diagnostic.GetMessage());
    }

    [Fact]
    public void PropertyTrue_WithSyncAndAsyncHandlerForOneCommand_ReportsNothing()
    {
        // Send and SendAsync resolve independently, so this pair is a normal working arrangement.
        const string source = """
            using Paramore.Brighter;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public class GreetingHandler : RequestHandler<GreetingCommand> { }
            public class GreetingHandlerAsync : RequestHandlerAsync<GreetingCommand> { }
            """;

        Assert.DoesNotContain(Run(source, "true").Results.Single().Diagnostics, d => d.Id == "BRGEN012");
    }

    [Fact]
    public void PropertyTrue_WhenThisCompilationAlreadyDeclaresTheAutoClass_ReportsBRGEN014()
    {
        // Emitting over the top would be CS0101 in a generated file the user cannot edit.
        const string source = """
            using Paramore.Brighter;

            namespace App
            {
                public class GreetingCommand : Command
                {
                    public GreetingCommand() : base(System.Guid.NewGuid()) { }
                }

                public class GreetingHandler : RequestHandler<GreetingCommand> { }
            }

            namespace Paramore.Brighter.Extensions.DependencyInjection
            {
                internal static class BrighterAssemblyRegistrations
                {
                }
            }
            """;

        var single = Run(source, "true").Results.Single();

        Assert.Contains(single.Diagnostics, d => d.Id == "BRGEN014");
        Assert.Empty(single.GeneratedSources);
    }

    [Fact]
    public void PropertyTrue_WithoutDependencyInjectionPackage_AndAManualMethod_ReportsOnlyBRGEN009()
    {
        // One missing reference, one diagnostic: BRGEN009 already names the method and points at it.
        const string source = """
            using Paramore.Brighter;

            namespace App;

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial object AddFromThisAssembly(object builder);
            }
            """;

        var single = Run(source, "true", BaseReferences.Concat(new[] { CoreReference }).ToArray())
            .Results.Single();

        Assert.Contains(single.Diagnostics, d => d.Id == "BRGEN009");
        Assert.DoesNotContain(single.Diagnostics, d => d.Id == "BRGEN010");
    }

    [Fact]
    public void PropertyTrue_WithTwoHandlersForOneEvent_ReportsNothing()
    {
        // Several handlers for one event is the whole point of Publish, so BRGEN012 must not fire.
        const string source = """
            using Paramore.Brighter;

            namespace App;

            public class GreetingEvent : Event
            {
                public GreetingEvent() : base(System.Guid.NewGuid()) { }
            }

            public class GreetingHandler : RequestHandler<GreetingEvent> { }
            public class SecondGreetingHandler : RequestHandler<GreetingEvent> { }
            """;

        Assert.DoesNotContain(Run(source, "true").Results.Single().Diagnostics, d => d.Id == "BRGEN012");
    }

    [Fact]
    public void PropertyNotABoolean_ReportsBRGEN013AndDoesNotGenerate()
    {
        // "1" is not an MSBuild boolean; treating it silently as "off" loses every registration in
        // the project to a typo.
        var single = Run(UserSource, "1").Results.Single();

        var diagnostic = Assert.Single(single.Diagnostics, d => d.Id == "BRGEN013");
        Assert.Contains("'1'", diagnostic.GetMessage());
        Assert.Empty(single.GeneratedSources);
    }

    /// <summary>
    /// A compiled assembly holding the auto class the generator emits, with InternalsVisibleTo to
    /// the test compilation — i.e. what a library built with auto-registration on looks like from
    /// its own test project.
    /// </summary>
    private static MetadataReference LibraryExposingAutoClass()
    {
        const string source = """
            using System.Runtime.CompilerServices;

            [assembly: InternalsVisibleTo("TestAsm")]

            namespace Paramore.Brighter.Extensions.DependencyInjection
            {
                internal static class BrighterAssemblyRegistrations
                {
                }
            }
            """;

        var compilation = CSharpCompilation.Create(
            "OtherAsm",
            new[] { CSharpSyntaxTree.ParseText(source) },
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    [Fact]
    public void PropertyTrue_WithInvalidManualMethod_StillGeneratesAutoClass()
    {
        // Only a *valid* [BrighterRegistrations] method suppresses the auto class; an invalid one
        // (which produces its own BRGEN diagnostic and no implementation) does not.
        const string source = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public class GreetingHandler : RequestHandler<GreetingCommand>
            {
                public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial void NotAValidSignature();
            }
            """;
        var single = Run(source, "true").Results.Single();

        Assert.Contains(
            single.GeneratedSources,
            g => g.HintName == "BrighterAssemblyRegistrations__AddFromThisAssembly.g.cs");
    }

    private sealed class StubOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly StubOptions _global;
        public StubOptionsProvider(ImmutableDictionary<string, string> globals) => _global = new StubOptions(globals);
        public override AnalyzerConfigOptions GlobalOptions => _global;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => StubOptions.Empty;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => StubOptions.Empty;

        private sealed class StubOptions : AnalyzerConfigOptions
        {
            public static readonly StubOptions Empty = new(ImmutableDictionary<string, string>.Empty);
            private readonly ImmutableDictionary<string, string> _values;
            public StubOptions(ImmutableDictionary<string, string> values) => _values = values;
            public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
        }
    }
}

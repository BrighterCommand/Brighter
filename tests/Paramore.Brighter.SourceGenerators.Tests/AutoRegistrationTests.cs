using System.Collections.Generic;
using System.Collections.Immutable;
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

    private static GeneratorDriverRunResult Run(string source, string? autoRegistrationValue)
    {
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Paramore.Brighter.IRequest).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder).Assembly.Location),
        };
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

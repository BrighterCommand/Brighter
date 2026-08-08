using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Paramore.Brighter.SourceGenerators.Tests;

/// <summary>
/// Verifies that the incremental pipeline really is incremental: when an edit doesn't change the
/// semantically relevant shape of the compilation, the model stages must not be re-computed into a
/// new value. The classic failure is a pipeline value that lost value equality (a record holding a
/// raw array, or a Roslyn symbol that escaped a transform) — that shows up here as a tracked stage
/// flipping to <see cref="IncrementalStepRunReason.Modified"/> on an unrelated edit.
/// </summary>
public class IncrementalCachingTests
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

        public static partial class Registrations
        {
            [BrighterRegistrations]
            public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
        }
        """;

    private static readonly string[] ModelStages =
    {
        TrackingNames.MethodCandidates,
        TrackingNames.DiscoveryBatches,
        TrackingNames.DiscoveredEntries,
        TrackingNames.RegistrationInputs,
    };

    private static (GeneratorDriver Driver, Compilation Compilation) RunInitial(string source)
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

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new BrighterRegistrationsGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        return (driver, compilation);
    }

    private static IncrementalStepRunReason[] StageReasons(GeneratorRunResult result, string stage) =>
        result.TrackedSteps.TryGetValue(stage, out var steps)
            ? steps.SelectMany(s => s.Outputs).Select(o => o.Reason).ToArray()
            : Array.Empty<IncrementalStepRunReason>();

    private static IncrementalStepRunReason[] OutputReasons(GeneratorRunResult result) =>
        result.TrackedOutputSteps.SelectMany(kv => kv.Value).SelectMany(s => s.Outputs).Select(o => o.Reason).ToArray();

    private static void AssertNotRecomputed(GeneratorRunResult result, string stage)
    {
        var reasons = StageReasons(result, stage);
        Assert.NotEmpty(reasons);
        Assert.All(reasons, reason => Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Stage '{stage}' was {reason}; expected Cached or Unchanged. " +
            "A Modified/New here means a pipeline value lost its value equality."));
    }

    [Fact]
    public void UnrelatedSyntaxEdit_ModelStagesAreNotInvalidated()
    {
        var (driver, compilation) = RunInitial(UserSource);

        // Touch the source: append a trailing comment. Nothing semantically relevant changes, so
        // the transforms may re-run but must produce equal values (Unchanged), and everything
        // downstream of the Collect must stay Cached.
        var tree = compilation.SyntaxTrees.Single();
        var newCompilation = compilation.ReplaceSyntaxTree(
            tree, CSharpSyntaxTree.ParseText(tree.GetText().ToString() + "\n// trailing comment\n"));

        var result = driver.RunGenerators(newCompilation).GetRunResult().Results.Single();

        foreach (var stage in ModelStages)
            AssertNotRecomputed(result, stage);

        // Stages downstream of the Collect see an unchanged collected input, so they are fully
        // cached (never even re-executed). This is the strongest guarantee that equality held.
        Assert.All(StageReasons(result, TrackingNames.DiscoveredEntries),
            r => Assert.Equal(IncrementalStepRunReason.Cached, r));
        Assert.All(StageReasons(result, TrackingNames.RegistrationInputs),
            r => Assert.Equal(IncrementalStepRunReason.Cached, r));

        // And no generated output is regenerated.
        Assert.All(OutputReasons(result), r => Assert.True(
            r is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"A source output was {r}; expected Cached or Unchanged."));
    }

    [Fact]
    public void AddingUnrelatedClass_DoesNotInvalidateRegistration()
    {
        var (driver, compilation) = RunInitial(UserSource);

        // A new file with a class that implements no Brighter interface contributes zero entries,
        // so the discovered-entries list — and therefore the registration — is unchanged.
        var newTree = CSharpSyntaxTree.ParseText("""
            namespace App.Other;

            public class Bystander
            {
                public int X { get; set; }
            }
            """);
        var result = driver.RunGenerators(compilation.AddSyntaxTrees(newTree)).GetRunResult().Results.Single();

        AssertNotRecomputed(result, TrackingNames.DiscoveredEntries);
        AssertNotRecomputed(result, TrackingNames.RegistrationInputs);
        Assert.All(OutputReasons(result), r => Assert.True(
            r is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"A source output was {r}; expected Cached or Unchanged."));
    }

    [Fact]
    public void AddingNewHandler_InvalidatesRegistration()
    {
        var (driver, compilation) = RunInitial(UserSource);

        // Adding a real handler must flow through to a regenerated registration — the positive
        // control proving the cache invalidates when it genuinely should.
        var newTree = CSharpSyntaxTree.ParseText("""
            using Paramore.Brighter;

            namespace App;

            public class OtherCommand : Command
            {
                public OtherCommand() : base(System.Guid.NewGuid()) { }
            }

            public class OtherHandler : RequestHandler<OtherCommand>
            {
                public override OtherCommand Handle(OtherCommand command) => base.Handle(command);
            }
            """);
        var result = driver.RunGenerators(compilation.AddSyntaxTrees(newTree)).GetRunResult().Results.Single();

        Assert.Contains(IncrementalStepRunReason.Modified, StageReasons(result, TrackingNames.DiscoveredEntries));
        Assert.Contains(IncrementalStepRunReason.Modified, OutputReasons(result));

        var generated = result.GeneratedSources
            .First(gs => gs.HintName.EndsWith("__AddFromThisAssembly.g.cs"))
            .SourceText.ToString();
        Assert.Contains("global::App.OtherHandler", generated);
    }
}

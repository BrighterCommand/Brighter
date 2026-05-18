using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paramore.Brighter.SourceGenerators.Tests;

/// <summary>
/// Verifies that the incremental pipeline's outputs are cached when an edit doesn't change
/// the semantically relevant shape of the compilation. These tests exercise the actual
/// IncrementalStepRunReason values that Roslyn records — the contract that "the same input
/// produces a cached result" is what determines real-world IDE responsiveness.
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

    [Fact]
    public void UnrelatedSyntaxEdit_CachesAllOutputs()
    {
        var (driver, compilation) = RunInitial(UserSource);

        // Touch the source: add a blank line at the bottom. Nothing semantically relevant changes.
        var edited = compilation.SyntaxTrees.Single();
        var editedText = edited.GetText().ToString() + "\n// trailing comment\n";
        var newCompilation = compilation
            .ReplaceSyntaxTree(edited, CSharpSyntaxTree.ParseText(editedText));

        driver = driver.RunGenerators(newCompilation);

        var runResult = driver.GetRunResult();
        var stepReasons = runResult.Results
            .Single()
            .TrackedOutputSteps
            .SelectMany(kv => kv.Value)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToArray();

        Assert.NotEmpty(stepReasons);
        Assert.All(stepReasons, reason =>
            Assert.True(
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"Expected Cached or Unchanged but got {reason}"));
    }

    [Fact]
    public void AddingUnrelatedClass_CachesRegistrationOutput()
    {
        var (driver, compilation) = RunInitial(UserSource);

        // Add a completely unrelated class in a new tree — nothing implementing handler/mapper.
        var newTree = CSharpSyntaxTree.ParseText("""
            namespace App.Other;

            public class Bystander
            {
                public int X { get; set; }
            }
            """);
        var newCompilation = compilation.AddSyntaxTrees(newTree);

        driver = driver.RunGenerators(newCompilation);

        var runResult = driver.GetRunResult();
        var allReasons = runResult.Results
            .Single()
            .TrackedSteps
            .SelectMany(kv => kv.Value)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToArray();

        // The new class adds a fresh "ReadClass" run (New), but downstream of FlattenAndSort the
        // discovered list must be Unchanged because the new class produces zero entries.
        Assert.Contains(IncrementalStepRunReason.Unchanged, allReasons);

        // And the final generated source step shouldn't be Modified.
        var sourceReasons = runResult.Results
            .Single()
            .TrackedOutputSteps
            .SelectMany(kv => kv.Value)
            .SelectMany(step => step.Outputs)
            .Select(o => o.Reason)
            .ToArray();
        Assert.All(sourceReasons, reason =>
            Assert.True(
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"Expected Cached or Unchanged for output step but got {reason}"));
    }

    [Fact]
    public void AddingNewHandler_RegeneratesRegistrationOutput()
    {
        var (driver, compilation) = RunInitial(UserSource);

        // Add a NEW handler in a separate file. This must invalidate the output.
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
        var newCompilation = compilation.AddSyntaxTrees(newTree);

        driver = driver.RunGenerators(newCompilation);

        var runResult = driver.GetRunResult();
        var sourceReasons = runResult.Results
            .Single()
            .TrackedOutputSteps
            .SelectMany(kv => kv.Value)
            .SelectMany(step => step.Outputs)
            .Select(o => o.Reason)
            .ToArray();

        // At least one source output must be Modified (the registration file).
        Assert.Contains(IncrementalStepRunReason.Modified, sourceReasons);

        // And the generated source itself must mention the new handler.
        var generated = runResult.Results.Single().GeneratedSources
            .First(gs => gs.HintName.EndsWith("__AddFromThisAssembly.g.cs"))
            .SourceText.ToString();
        Assert.Contains("global::App.OtherHandler", generated);
    }
}

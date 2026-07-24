
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Paramore.Brighter.Analyzer.Tests.CodeFixes
{
    public abstract class BaseCodeFixTest<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        protected CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier> testContext;

        protected BaseCodeFixTest()
        {
            testContext = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90
            };
            testContext.TestState.OutputKind = OutputKind.ConsoleApplication;
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Publication).Assembly.Location));
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));
            testContext.CompilerDiagnostics = CompilerDiagnostics.None;
        }
    }
}

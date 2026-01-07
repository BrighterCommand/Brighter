
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;

namespace Paramore.Brighter.Analyzer.Tests.Analyzers
{
    public abstract class BaseAnalyzerTest<T> where T : DiagnosticAnalyzer, new()
    {
        protected  CSharpAnalyzerTest<T, DefaultVerifier> testContext;
        protected BaseAnalyzerTest()
        {
            testContext = new CSharpAnalyzerTest<T, DefaultVerifier>
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90
            };
            testContext.TestState.OutputKind = OutputKind.ConsoleApplication;
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Publication).Assembly.Location));
            testContext.CompilerDiagnostics = CompilerDiagnostics.None;
        }
    }
}

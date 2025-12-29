using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;


namespace Paramore.Brighter.Analyzer.Test.PublicationRequestTypeMissing
{
    public class PublicationRequestTypeAssignmentAnalyzerTest
    {
        private readonly CSharpAnalyzerTest<PublicationRequestTypeAssignmentAnalyzer, DefaultVerifier> testContext;

        public PublicationRequestTypeAssignmentAnalyzerTest()
        {
            testContext = new CSharpAnalyzerTest<PublicationRequestTypeAssignmentAnalyzer, DefaultVerifier>();
            testContext.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
            testContext.TestState.OutputKind = OutputKind.ConsoleApplication;
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Publication).Assembly.Location));
            testContext.CompilerDiagnostics = CompilerDiagnostics.None;
        }

        [Fact]
        public async Task When_Initializing_Publication_WithOut_RequestType()
        {

            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
namespace TestNamespace
{
var publication = {|#0:new PublicationTest()|};
        
    public class PublicationTest : Publication
    {
    }
        }
""";

            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(PublicationRequestTypeAssignmentAnalyzer.RequestTypeMissingRule).WithLocation(0).WithArguments("PublicationTest"));
            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Initializing_Publication_With_Right_RequestType()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
namespace TestNamespace
{
var publication = {|#0:new PublicationTest()
{
RequestType = typeof(EventSample)
}|};
        
    public class PublicationTest : Publication
    {
    }
    public class EventSample : Event
{
    public EventSample(Id id) : base(id)
    {
    }
}
        }
""";
            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Initializing_Publication_With_Wrong_RequestType()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
namespace TestNamespace
{
var publication = new PublicationTest()
{
RequestType = {|#0:typeof(EventSample)|}
};
        
    public class PublicationTest : Publication
    {
    }
    public class EventSample
{
}
        }
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(PublicationRequestTypeAssignmentAnalyzer.WrongRequestTypeRule).WithLocation(0).WithArguments("EventSample"));

            await testContext.RunAsync();
        }
    }
}

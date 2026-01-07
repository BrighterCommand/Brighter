using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;
using Paramore.Brighter.Analyzer.Tests.Analyzers;


namespace Paramore.Brighter.Analyzer.Test.Analyzers
{
    public class PublicationRequestTypeAssignmentAnalyzerTest : BaseAnalyzerTest<PublicationRequestTypeAssignmentAnalyzer>
    {

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
var publication =new PublicationTest()
{
RequestType = typeof(EventSample)
};
        
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
    {}
    public class EventSample{}
        }
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(PublicationRequestTypeAssignmentAnalyzer.WrongRequestTypeRule).WithLocation(0).WithArguments("EventSample"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Initializing_Non_Publication_Type()
        {
            testContext.TestCode = /* lang=c#-test */ """
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public class AnyClass
                {
                    public void Method()
                    {
                        var list = new List<string>(); // Should be ignored
                    }
                }
            }
""";
            await testContext.RunAsync();
        }
    }
}

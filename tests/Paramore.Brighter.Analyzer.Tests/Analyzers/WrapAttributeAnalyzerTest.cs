using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;
using Paramore.Brighter.Analyzer.Tests.Analyzers;

namespace Paramore.Brighter.Analyzer.Test.Analyzers
{
    public class WrapAttributeAnalyzerTest: BaseAnalyzerTest<WrapAttributeAnalyzer>
    {

        [Fact]
        public async Task When_Adding_Attribute_To_MessageMapper()
        {

            testContext.TestCode = /* lang=c#-test */ """
             using Paramore.Brighter;
            using Paramore.Brighter.Transforms.Attributes;
            namespace TestNamespace
            { 
            public class WrapWithSample: IAmAMessageMapper<SampleEvent>
            {
                public IRequestContext? Context { get; set ; }

                [{|#0:Compress(0)|}]
                public SampleEvent MapToRequest(Message message)
                {
                    return new SampleEvent(message.Id);
                }
                [{|#1:Decompress(0)|}]
                public Message MapToMessage(SampleEvent request, Publication publication)
                {
                    throw new NotImplementedException();
                }
            }


            public class SampleEvent(Id id) : Event(id)
            {
            }
""";

           testContext.ExpectedDiagnostics.Add(new DiagnosticResult(WrapAttributeAnalyzer.WrapAttributeRule ).WithLocation(0).WithArguments("CompressAttribute"));
           testContext.ExpectedDiagnostics.Add(new DiagnosticResult(WrapAttributeAnalyzer.UnWrapWithAttributeRule ).WithLocation(1).WithArguments("DecompressAttribute"));
            await testContext.RunAsync();
        }
    }
}

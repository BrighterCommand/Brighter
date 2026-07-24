using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;
using Paramore.Brighter.Analyzer.CodeFixes;
using Xunit;

namespace Paramore.Brighter.Analyzer.Tests.CodeFixes
{
    public class MissingPartitionerCodeFixProviderTest
        : BaseCodeFixTest<KafkaPublicationPartitionerAnalyzer, MissingPartitionerCodeFixProvider>
    {
        [Fact]
        public async Task When_Partitioner_Is_Missing_Should_Add_Murmur2Random()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = {|#0:new KafkaPublication()|};
        }
    }
}
""";

            testContext.FixedCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new KafkaPublication() { Partitioner = Partitioner.Murmur2Random };
        }
    }
}
""";

            testContext.ExpectedDiagnostics.Add(
                new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.s_missingPartitionerRule).WithLocation(0).WithArguments("KafkaPublication"));

            await testContext.RunAsync();
        }
    }
}

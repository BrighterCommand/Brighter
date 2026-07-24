using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;
using Paramore.Brighter.Analyzer.CodeFixes;
using Xunit;

namespace Paramore.Brighter.Analyzer.Tests.CodeFixes
{
    public class PartitionerValueCodeFixProviderTest
        : BaseCodeFixTest<KafkaPublicationPartitionerAnalyzer, PartitionerValueCodeFixProvider>
    {
        [Fact]
        public async Task When_ConsistentRandom_Is_Used_Should_Offer_Murmur2Random()
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
            var publication = {|#0:new KafkaPublication
            {
                Partitioner = Partitioner.ConsistentRandom
            }|};
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
            var publication = new KafkaPublication
            {
                Partitioner = Partitioner.Murmur2Random
            };
        }
    }
}
""";

            testContext.ExpectedDiagnostics.Add(
                new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.s_consistentRandomPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Consistent_Is_Used_Should_Offer_Murmur2()
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
            var publication = {|#0:new KafkaPublication
            {
                Partitioner = Partitioner.Consistent
            }|};
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
            var publication = new KafkaPublication
            {
                Partitioner = Partitioner.Murmur2
            };
        }
    }
}
""";

            testContext.ExpectedDiagnostics.Add(
                new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.s_consistentPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }
    }
}

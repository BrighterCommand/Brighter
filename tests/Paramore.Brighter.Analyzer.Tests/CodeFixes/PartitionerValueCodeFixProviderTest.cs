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
                new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentRandomPartitionerRule).WithLocation(0));

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
                new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Bare_Identifier_Via_Using_Static_Is_Used_Should_Offer_Murmur2Random()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;
using static Paramore.Brighter.MessagingGateway.Kafka.Partitioner;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = {|#0:new KafkaPublication
            {
                Partitioner = ConsistentRandom
            }|};
        }
    }
}
""";

            testContext.FixedCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;
using static Paramore.Brighter.MessagingGateway.Kafka.Partitioner;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new KafkaPublication
            {
                Partitioner = Murmur2Random
            };
        }
    }
}
""";

            testContext.ExpectedDiagnostics.Add(
                new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentRandomPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Consistent_Is_Set_After_Construction_Should_Offer_Murmur2()
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
            var publication = new KafkaPublication();
            {|#0:publication.Partitioner = Partitioner.Consistent|};
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
            var publication = new KafkaPublication();
            publication.Partitioner = Partitioner.Murmur2;
        }
    }
}
""";

            testContext.ExpectedDiagnostics.Add(
                new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Value_Is_Parenthesized_Should_Not_Offer_Fix()
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
                Partitioner = (Partitioner.Consistent)
            }|};
        }
    }
}
""";

            testContext.ExpectedDiagnostics.Add(
                new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }
    }
}

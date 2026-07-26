using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;
using Paramore.Brighter.Analyzer.CodeFixes;
using Xunit;

namespace Paramore.Brighter.Analyzer.Tests.CodeFixes;

public class MissingPartitionerCodeFixProviderTest
    : BaseCodeFixTest<KafkaPublicationPartitionerAnalyzer, MissingPartitionerCodeFixProvider>
{
    [Fact]
    public async Task When_Partitioner_Is_Missing_Should_Add_Murmur2Random()
    {
        testContext.TestCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new {|#0:KafkaPublication|}();
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
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
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }

    [Fact]
    public async Task When_Kafka_Using_Is_Missing_Should_Add_Fully_Qualified_Partitioner()
    {
        testContext.TestCode = /* lang=c#-test */
            """
namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new {|#0:Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication|}();
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication() { Partitioner = Paramore.Brighter.MessagingGateway.Kafka.Partitioner.Murmur2Random };
        }
    }
}
""";

        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }

    [Fact]
    public async Task When_Partitioner_Is_Missing_Should_Append_To_Existing_Initializer()
    {
        testContext.TestCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new {|#0:KafkaPublication|}
            {
                Topic = new RoutingKey("x"),
                NumPartitions = 3
            };
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
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
                Topic = new RoutingKey("x"),
                NumPartitions = 3,
                Partitioner = Partitioner.Murmur2Random
            };
        }
    }
}
""";

        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }

    [Fact]
    public async Task When_Partitioner_Is_Missing_Should_Append_After_Trailing_Comment()
    {
        testContext.TestCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new {|#0:KafkaPublication|}
            {
                Topic = new RoutingKey("x"),
                NumPartitions = 3 // one per shard
            };
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
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
                Topic = new RoutingKey("x"),
                NumPartitions = 3, // one per shard
                Partitioner = Partitioner.Murmur2Random
            };
        }
    }
}
""";

        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }

    [Fact]
    public async Task When_Partitioner_Is_Missing_On_Target_Typed_New_Should_Add_Murmur2Random()
    {
        testContext.TestCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            KafkaPublication publication = {|#0:new|}();
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            KafkaPublication publication = new()
            {
                Partitioner = Partitioner.Murmur2Random
            };
        }
    }
}
""";

        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }

    [Fact]
    public async Task When_Partitioner_Is_Missing_On_Single_Line_Initializer_Should_Append_On_Same_Line()
    {
        testContext.TestCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new {|#0:KafkaPublication|} { Topic = new RoutingKey("x") };
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new KafkaPublication { Topic = new RoutingKey("x"), Partitioner = Partitioner.Murmur2Random };
        }
    }
}
""";

        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }

    [Fact]
    public async Task FixAll_Should_Add_Partitioner_To_All_Publications()
    {
        testContext.TestCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var first = new {|#0:KafkaPublication|}();
            var second = new {|#1:KafkaPublication|}();
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var first = new KafkaPublication() { Partitioner = Partitioner.Murmur2Random };
            var second = new KafkaPublication() { Partitioner = Partitioner.Murmur2Random };
        }
    }
}
""";

        testContext.BatchFixedCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var first = new KafkaPublication() { Partitioner = Partitioner.Murmur2Random };
            var second = new KafkaPublication() { Partitioner = Partitioner.Murmur2Random };
        }
    }
}
""";

        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );
        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(1)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }

    [Fact]
    public async Task When_Partitioner_Is_Missing_On_Empty_Initializer_Should_Add_Murmur2Random()
    {
        testContext.TestCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new {|#0:KafkaPublication|} { };
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new KafkaPublication { Partitioner = Partitioner.Murmur2Random };
        }
    }
}
""";

        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }

    [Fact]
    public async Task When_Partitioner_Is_Missing_Should_Append_After_Trailing_Comma()
    {
        testContext.TestCode = /* lang=c#-test */
            """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new {|#0:KafkaPublication|}
            {
                Topic = new RoutingKey("x"),
                NumPartitions = 3,
            };
        }
    }
}
""";

        testContext.FixedCode = /* lang=c#-test */
            """
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
                Topic = new RoutingKey("x"),
                NumPartitions = 3,
                Partitioner = Partitioner.Murmur2Random
            };
        }
    }
}
""";

        testContext.ExpectedDiagnostics.Add(
            new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule)
                .WithLocation(0)
                .WithArguments("KafkaPublication")
        );

        await testContext.RunAsync();
    }
}

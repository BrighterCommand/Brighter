using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Paramore.Brighter.Analyzer.Analyzers;

namespace Paramore.Brighter.Analyzer.Tests.Analyzers
{
    public class KafkaPublicationPartitionerAnalyzerTest : BaseAnalyzerTest<KafkaPublicationPartitionerAnalyzer>
    {
        [Fact]
        public async Task When_KafkaPublication_Is_Created_Without_Partitioner_Should_Report_Missing_Partitioner()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

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
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule).WithLocation(0).WithArguments("KafkaPublication"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Generic_Is_Created_Without_Partitioner_Should_Report_Missing_Partitioner()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class MyRequest : IRequest 
    { 
        public System.Guid Id { get; set; }
        public System.Guid SpanId { get; set; }
    }

    class TypeName
    {
        public void Method()
        {
            var publication = {|#0:new KafkaPublication<MyRequest>()|};
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule).WithLocation(0).WithArguments("KafkaPublication<MyRequest>"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Is_Created_With_ConsistentRandom_Should_Report_Warning()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

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
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentRandomPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Is_Created_With_Consistent_Should_Report_Warning()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

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
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Is_Created_With_Murmur2Random_Should_Not_Report()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

            testContext.TestCode = /* lang=c#-test */ """
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
            
            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Without_Partitioner_Is_Nested_In_Another_Object_Creation_Should_Report_Once_At_Publication()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class Holder
    {
        public Holder(KafkaPublication publication) { }
    }

    class TypeName
    {
        public void Method()
        {
            var holder = new Holder({|#0:new KafkaPublication()|});
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule).WithLocation(0).WithArguments("KafkaPublication"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_With_Consistent_Is_Nested_In_Another_Object_Creation_Should_Report_Once_At_Publication()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class Holder
    {
        public Holder(KafkaPublication publication) { }
    }

    class TypeName
    {
        public void Method()
        {
            var holder = new Holder({|#0:new KafkaPublication
            {
                Partitioner = Partitioner.Consistent
            }|});
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Is_Created_With_Random_Should_Not_Report()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

            testContext.TestCode = /* lang=c#-test */ """
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
                Partitioner = Partitioner.Random
            };
        }
    }
}
""";

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Nested_Object_Has_Own_Partitioner_Property_Should_Still_Report_Missing_Partitioner()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class Config
    {
        public int Partitioner { get; set; }
    }

    class TypeName
    {
        public void Method()
        {
            var publication = {|#0:new KafkaPublication
            {
                DefaultHeaders = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["key"] = new Config { Partitioner = 3 }
                }
            }|};
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule).WithLocation(0).WithArguments("KafkaPublication"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Partitioner_Is_Set_After_Construction_Should_Not_Report()
        {
            testContext.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location));

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
            publication.Partitioner = Partitioner.Murmur2Random;
        }
    }
}
""";

            await testContext.RunAsync();
        }
    }
}

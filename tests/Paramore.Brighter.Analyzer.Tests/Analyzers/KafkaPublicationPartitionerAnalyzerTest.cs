using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Paramore.Brighter.Analyzer.Analyzers;

namespace Paramore.Brighter.Analyzer.Tests.Analyzers
{
    public class KafkaPublicationPartitionerAnalyzerTest : BaseKafkaAnalyzerTest
    {
        [Fact]
        public async Task When_KafkaPublication_Is_Created_Without_Partitioner_Should_Report_Missing_Partitioner()
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
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule).WithLocation(0).WithArguments("KafkaPublication"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Generic_Is_Created_Without_Partitioner_Should_Report_Missing_Partitioner()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class MyRequest : IRequest 
    { 
        public Id Id { get; set; }
        public Id? CorrelationId { get; set; }
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
                {|#0:Partitioner = Partitioner.ConsistentRandom|}
            };
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
                {|#0:Partitioner = Partitioner.Consistent|}
            };
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
            var holder = new Holder(new KafkaPublication
            {
                {|#0:Partitioner = Partitioner.Consistent|}
            });
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

        [Fact]
        public async Task When_Consistent_Is_Set_After_Construction_Should_Report_Warning_At_Assignment()
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
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_ConsistentRandom_Is_Set_After_Construction_Should_Report_Warning_At_Assignment()
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
            {|#0:publication.Partitioner = Partitioner.ConsistentRandom|};
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentRandomPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Plain_Publication_Is_Created_Should_Not_Report()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;

namespace ConsoleApplication1
{
    class TypeName
    {
        public void Method()
        {
            var publication = new Publication();
        }
    }
}
""";

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Subclass_Is_Created_Without_Partitioner_Should_Report_Missing_Partitioner()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class MyPublication : KafkaPublication
    {
    }

    class TypeName
    {
        public void Method()
        {
            var publication = {|#0:new MyPublication()|};
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule).WithLocation(0).WithArguments("MyPublication"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Partitioner_Is_Set_On_Field_After_Construction_Should_Not_Report()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        private KafkaPublication _publication;

        public void Method()
        {
            _publication = new KafkaPublication();
            _publication.Partitioner = Partitioner.Murmur2Random;
        }
    }
}
""";

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Partitioner_Is_Set_Before_Construction_Should_Still_Report_Missing_Partitioner()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class TypeName
    {
        private KafkaPublication _publication;

        public void Method()
        {
            _publication.Partitioner = Partitioner.Murmur2Random;
            _publication = {|#0:new KafkaPublication()|};
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule).WithLocation(0).WithArguments("KafkaPublication"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Is_Created_With_Target_Typed_New_Without_Partitioner_Should_Report_Missing_Partitioner()
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
            KafkaPublication publication = {|#0:new()|};
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.MissingPartitionerRule).WithLocation(0).WithArguments("KafkaPublication"));

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_KafkaPublication_Generic_Is_Created_With_ConsistentRandom_Should_Report_Warning()
        {
            testContext.TestCode = /* lang=c#-test */ """
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace ConsoleApplication1
{
    class MyRequest : IRequest 
    { 
        public Id Id { get; set; }
        public Id? CorrelationId { get; set; }
    }

    class TypeName
    {
        public void Method()
        {
            var publication = new KafkaPublication<MyRequest>
            {
                {|#0:Partitioner = Partitioner.ConsistentRandom|}
            };
        }
    }
}
""";
            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(KafkaPublicationPartitionerAnalyzer.ConsistentRandomPartitionerRule).WithLocation(0));

            await testContext.RunAsync();
        }
    }
}

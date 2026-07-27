using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;

namespace Paramore.Brighter.Analyzer.Tests.Analyzers;

public abstract class BaseKafkaAnalyzerTest : BaseAnalyzerTest<KafkaPublicationPartitionerAnalyzer>
{
    protected BaseKafkaAnalyzerTest()
    {
        testContext.TestState.OutputKind = OutputKind.DynamicallyLinkedLibrary;
        testContext.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(
                typeof(Paramore.Brighter.MessagingGateway.Kafka.KafkaPublication).Assembly.Location
            )
        );
        testContext.CompilerDiagnostics = CompilerDiagnostics.Errors;
    }
}

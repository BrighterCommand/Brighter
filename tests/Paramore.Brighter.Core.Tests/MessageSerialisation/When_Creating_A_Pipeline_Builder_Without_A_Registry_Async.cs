using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class AsyncTransformPipelineMissingRegistryTests
{
    [Test]
    public async Task When_Creating_A_Pipeline_Builder_Without_A_Registry()
    {
        //arrange
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => new MySimpleTransformAsync()));
        //act
        var exception = Catch.Exception(() => new TransformPipelineBuilderAsync(null, messageTransformerFactory, InstrumentationOptions.All));
        //assert
        await Assert.That(exception).IsNotNull();
        await Assert.That((exception) is ConfigurationException).IsTrue();
    }
}
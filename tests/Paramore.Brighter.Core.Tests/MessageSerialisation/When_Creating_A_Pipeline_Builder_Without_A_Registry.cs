using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class TransformPipelineMissingRegistryTests
{
    [Test]
    public async Task When_Creating_A_Pipeline_Builder_Without_A_Registry()
    {
        //arrange
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransform()));
        //act
        var exception = Catch.Exception(() => new TransformPipelineBuilder(null, messageTransformerFactory));
        //assert
        await Assert.That(exception).IsNotNull();
        await Assert.That((exception) is ConfigurationException).IsTrue();
    }
}
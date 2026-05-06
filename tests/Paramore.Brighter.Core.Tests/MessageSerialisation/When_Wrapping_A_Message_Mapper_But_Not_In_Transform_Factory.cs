using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class MessageWrapRequestMissingTransformTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    public MessageWrapRequestMissingTransformTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()), null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Test]
    public async Task When_Wrapping_A_Message_Mapper_But_Not_In_Transform_Factory()
    {
        //act
        var exception = Catch.Exception(() => _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());
        await Assert.That(exception).IsNotNull();
        await Assert.That((exception) is ConfigurationException).IsTrue();
    }
}
using System.Text.Json;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class MessageWrapRequestTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;
    public MessageWrapRequestTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()), null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();
        _myCommand = new MyTransformableCommand();
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransform()));
        _publication = new Publication
        {
            Topic = new RoutingKey("MyTransformableCommand")
        };
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Test]
    public async Task When_Wrapping_A_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = _transformPipeline.Wrap(_myCommand, new RequestContext(), _publication);
        //assert
        await Assert.That(message.Body.Value).IsEqualTo(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString());
        await Assert.That(message.Header.Bag[MySimpleTransformAsync.HEADER_KEY]).IsEqualTo(MySimpleTransformAsync.TRANSFORM_VALUE);
    }
}
using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class MessageUnwrapRequestTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly Message _message;

    public MessageUnwrapRequestTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()),
            null
        );
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();

        MyTransformableCommand myCommand = new();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransform()));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);

        _message = new Message(
            new MessageHeader(myCommand.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        _message.Header.Bag[MySimpleTransformAsync.HEADER_KEY] = MySimpleTransformAsync.TRANSFORM_VALUE;
    }
    
    [Fact]
    public void When_Unwrapping_A_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>();
        var request = _transformPipeline.Unwrap(_message);
        
        //assert
        request.Value.Should().Be( MySimpleTransformAsync.TRANSFORM_VALUE);
    }
}

using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class MessageUnwrapRequestWithAttributesTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly Message _message;

    public MessageUnwrapRequestWithAttributesTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             new SimpleMessageMapperFactory(_ => new MyParameterizedTransformMessageMapper()),
             null);
         mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();

        var myCommand = new MyTransformableCommand();
        myCommand.Value = "Hello World";
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MyParameterizedTransform()));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);

        _message = new Message(
            new MessageHeader(myCommand.Id, "transform.event", MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General))));
    }
    
    [Fact]
    public void When_Wrapping_A_Message_Mapper_With_Attributes()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>();
        var request = _transformPipeline.Unwrap(_message);
        
        //assert
        request.Value.Should().Be("I am a parameterized template: Hello World");
    }
}

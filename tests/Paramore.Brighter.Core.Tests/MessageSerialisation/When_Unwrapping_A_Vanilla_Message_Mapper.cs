using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class VanillaMessageUnwrapRequestTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Message _message;

    public VanillaMessageUnwrapRequestTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyVanillaCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);

        _message = new Message(
            new MessageHeader(_myCommand.Id, new("transform.event"), MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        _message.Header.Bag[MySimpleTransformAsync.HEADER_KEY] = MySimpleTransformAsync.TRANSFORM_VALUE;
    }
    
    [Fact]
    public void When_Unwrapping_A_Vanilla_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>();
        var request = _transformPipeline.Unwrap(_message, new RequestContext());
        
        //assert
        request.Value = _myCommand.Value;
    }
}

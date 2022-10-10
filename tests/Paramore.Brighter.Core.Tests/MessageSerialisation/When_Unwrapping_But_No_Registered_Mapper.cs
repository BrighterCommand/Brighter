using System;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageUnwrapRequestMissingMapperTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Message _message;

    public MessageUnwrapRequestMissingMapperTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);

        _message = new Message(
            new MessageHeader(_myCommand.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        _message.Header.Bag[MySimpleTransformAsync.HEADER_KEY] = MySimpleTransformAsync.TRANSFORM_VALUE;
    }
    
    [Fact]
    public void When_Wrapping_But_No_Registered_Mapper()
    {
        //act
        var exception = Catch.Exception(() => _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline(_myCommand));
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConfigurationException>();
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
    }
}

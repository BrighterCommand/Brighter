using System;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageUnwrapRequestMissingTransformTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;

    public MessageUnwrapRequestMissingTransformTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);

        Message message = new(
            new MessageHeader(_myCommand.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        message.Header.Bag[MySimpleTransformAsync.HEADER_KEY] = MySimpleTransformAsync.TRANSFORM_VALUE;
    }
    
    [Fact]
    public void When_Unwrapping_A_Message_Mapper_But_Not_In_Transform_Factory()
    {
        //act
        var exception = Catch.Exception(() =>  _pipelineBuilder.BuildUnwrapPipeline(_myCommand));
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConfigurationException>();
    }
}

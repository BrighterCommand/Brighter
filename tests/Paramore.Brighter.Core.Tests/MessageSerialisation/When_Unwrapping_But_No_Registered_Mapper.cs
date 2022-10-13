using System;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class MessageUnwrapRequestMissingMapperTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;

    public MessageUnwrapRequestMissingMapperTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        MyTransformableCommand myCommand = new();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);

        Message message = new(
            new MessageHeader(myCommand.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        message.Header.Bag[MySimpleTransformAsync.HEADER_KEY] = MySimpleTransformAsync.TRANSFORM_VALUE;
    }
    
    [Fact]
    public void When_Wrapping_But_No_Registered_Mapper()
    {
        //act
        var exception = Catch.Exception(() => _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>());
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConfigurationException>();
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
    }
}

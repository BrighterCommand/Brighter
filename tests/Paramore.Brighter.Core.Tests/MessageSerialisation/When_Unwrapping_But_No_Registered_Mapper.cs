using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class MessageUnwrapRequestMissingMapperTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    public MessageUnwrapRequestMissingMapperTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()), null);
        MyTransformableCommand myCommand = new();
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransform()));
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
        Message message = new(new MessageHeader(myCommand.Id, new RoutingKey("transform.event"), MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow), new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        message.Header.Bag[MySimpleTransformAsync.HEADER_KEY] = MySimpleTransformAsync.TRANSFORM_VALUE;
    }

    [Test]
    public async Task When_Wrapping_But_No_Registered_Mapper()
    {
        //act
        var exception = Catch.Exception(() => _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>());
        await Assert.That(exception).IsNotNull();
        await Assert.That((exception) is ConfigurationException).IsTrue();
        await Assert.That((exception.InnerException) is InvalidOperationException).IsTrue();
    }
}
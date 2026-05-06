using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class MessageUnwrapRequestMissingTransformTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    public MessageUnwrapRequestMissingTransformTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()), null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();
        MyTransformableCommand myCommand = new();
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
        Message message = new(new MessageHeader(myCommand.Id, new("transform.event"), MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow), new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        message.Header.Bag[MySimpleTransformAsync.HEADER_KEY] = MySimpleTransformAsync.TRANSFORM_VALUE;
    }

    [Test]
    public async Task When_Unwrapping_A_Message_Mapper_But_Not_In_Transform_Factory()
    {
        //act
        var exception = Catch.Exception(() => _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>());
        await Assert.That(exception).IsNotNull();
        await Assert.That((exception) is ConfigurationException).IsTrue();
    }
}
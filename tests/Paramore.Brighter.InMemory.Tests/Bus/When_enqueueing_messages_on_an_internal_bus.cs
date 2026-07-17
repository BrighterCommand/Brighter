using System;
using System.Linq;

namespace Paramore.Brighter.InMemory.Tests.Bus;

public class InternalBusEnqueueTests 
{
    [Test]
    public async Task When_enqueueing_messages_on_an_internal_bus()
    {
        //arrange
        var internalBus = new InternalBus();
        var messageId = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey("test");
         
        //act
        internalBus.Enqueue(new Message(new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND), new MessageBody("test_content")));
        
        //assert
        var message = internalBus.Stream(routingKey).Single();
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Body.Value).IsEqualTo("test_content");
        await Assert.That(message.Header.Topic).IsEqualTo(routingKey);
        await Assert.That(message.Header.MessageId).IsEqualTo(messageId);
    }
}

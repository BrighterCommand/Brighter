using System;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Bus;

public class InternalBusEnqueueTests 
{
    [Fact]
    public void When_enqueueing_messages_on_an_internal_bus()
    {
        //arrange
        var internalBus = new InternalBus();
        var messageId = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey("test");
         
        //act
        internalBus.Enqueue(new Message(new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND), new MessageBody("test_content")));
        
        //assert
        var message = internalBus.Stream(routingKey).Single();
        Assert.NotNull(message);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal("test_content", message.Body.Value);
        Assert.Equal(routingKey, message.Header.Topic);
        Assert.Equal(messageId, message.Header.MessageId);
    }
}

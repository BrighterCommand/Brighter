using System;
using System.Linq;
using FluentAssertions;
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
        message.Should().NotBeNull();
        message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
        message.Body.Value.Should().Be("test_content");
        message.Header.Topic.Should().Be(routingKey);
        message.Header.MessageId.Should().Be(messageId);
    }
}

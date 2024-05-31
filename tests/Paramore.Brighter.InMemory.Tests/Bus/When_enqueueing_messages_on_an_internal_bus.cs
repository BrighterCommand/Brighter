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
        const string topic = "test";
         
        //act
       internalBus.Enqueue(new Message(new MessageHeader(messageId, topic, MessageType.MT_COMMAND), new MessageBody("test_content")));
        
        //assert
        var message = internalBus.Stream(new RoutingKey(topic)).Single();
        message.Should().NotBeNull();
        message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
        message.Body.Value.Should().Be("test_content");
        message.Header.Topic.Should().Be(topic);
        message.Header.Id.Should().Be(messageId);
    }
}

using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class InMemoryConsumerRejectWithDeadLetterTests 
{
    [Fact]
    public void When_rejecting_a_message_with_a_dead_letter_channel()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);
        const string deadLetterTopic = "dlq";
        var deadLetterRoutingKey = new RoutingKey(deadLetterTopic);

        var expectedMessage = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var timeProvider = new FakeTimeProvider();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, TimeSpan.FromMilliseconds(1000)) as IAmAMessageConsumerSync;
        consumer.AddDeadLetterChannel(deadLetterTopic);
        
        //act
        var receivedMessage = consumer.Receive().Single();
        consumer.Reject(receivedMessage);
        
        timeProvider.Advance(TimeSpan.FromSeconds(2));  //-- the message should be returned to the bus if there is no Acknowledge or Reject
        
        //assert
        Assert.Empty(bus.Stream(routingKey)); 
        Assert.NotEmpty(bus.Stream(deadLetterRoutingKey));
    }
}

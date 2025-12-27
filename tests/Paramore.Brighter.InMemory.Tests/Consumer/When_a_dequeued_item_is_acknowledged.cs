using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class InMemoryConsumerAcknowledgeTests
{
    [Fact]
    public void When_a_dequeud_item_lock_expires()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);

        var expectedMessage = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var timeProvider = new FakeTimeProvider();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000));
        
        //act
        var receivedMessage = consumer.Receive().Single();
        
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        
        //assert
        Assert.Single(bus.Stream(routingKey));  //-- the message should be returned to the bus if there is no Acknowledge or Reject
        
    }

    [Fact]
    public void When_a_dequeued_item_is_acknowledged()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);

        var expectedMessage = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var timeProvider = new FakeTimeProvider();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000));
        
        //act
        var receivedMessage = consumer.Receive().Single();
        consumer.Acknowledge(receivedMessage);
        
        timeProvider.Advance(TimeSpan.FromSeconds(2));  //-- the message should be returned to the bus if there is no Acknowledge or Reject
        
        //assert
        Assert.Empty(bus.Stream(routingKey));
    }
}

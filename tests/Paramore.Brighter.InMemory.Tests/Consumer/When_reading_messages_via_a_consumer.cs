using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class InMemoryChannelTests
{
    [Fact]
    public void When_reading_messages_via_a_consumer()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);
        
        var expectedMessage = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), myTopic, MessageType.MT_EVENT),
            new MessageBody("a test body"));


        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var consumer = new InMemoryMessageConsumer(routingKey, bus, new FakeTimeProvider(), 1000);
        
        //act
        var receivedMessage = consumer.Receive().Single();
        consumer.Acknowledge(receivedMessage);

        //assert
        Assert.Equal(expectedMessage, receivedMessage);
        Assert.Empty(bus.Stream(routingKey));
    }

    [Fact]
    public void When_requeueing_a_message_it_should_be_available_again()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);

        var expectedMessage = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), myTopic, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var consumer = new InMemoryMessageConsumer(routingKey, bus, new FakeTimeProvider(), 1000);
        
        //act
        var receivedMessage = consumer.Receive().Single();
        consumer.Requeue(receivedMessage, 0);
        
        //assert
        Assert.Single(bus.Stream(routingKey));
        
    }
    
    [Fact]
    public void When_requeueing_a_message_with_a_delay_it_should_not_be_available_immediately()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);

        var expectedMessage = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), myTopic, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var timeProvider = new FakeTimeProvider();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, 1000);
        
        //act
        var receivedMessage = consumer.Receive().Single();
        consumer.Requeue(receivedMessage, 1000);
        
        //assert
        Assert.Empty(bus.Stream(routingKey));
        
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        
        Assert.Single(bus.Stream(routingKey));
    }

    [Fact]
    public void When_a_dequeud_item_lock_expires()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);

        var expectedMessage = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), myTopic, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var timeProvider = new FakeTimeProvider();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, 1000);
        
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
            new MessageHeader(Guid.NewGuid().ToString(), myTopic, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var timeProvider = new FakeTimeProvider();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, 1000);
        
        //act
        var receivedMessage = consumer.Receive().Single();
        consumer.Acknowledge(receivedMessage);
        
        timeProvider.Advance(TimeSpan.FromSeconds(2));  //-- the message should be returned to the bus if there is no Acknowledge or Reject
        
        //assert
        Assert.Empty(bus.Stream(routingKey));
    }
    
    [Fact]
    public void When_a_dequeued_item_is_rejected()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);

        var expectedMessage = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), myTopic, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var timeProvider = new FakeTimeProvider();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, 1000);
        
        //act
        var receivedMessage = consumer.Receive().Single();
        consumer.Reject(receivedMessage);
        
        timeProvider.Advance(TimeSpan.FromSeconds(2));  //-- the message should be returned to the bus if there is no Acknowledge or Reject
        
        //assert
        Assert.Empty(bus.Stream(routingKey));
    }

}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class AsyncInMemoryConsumerRequeueTests
{
    [Fact]
    public async Task When_requeueing_a_message_it_should_be_available_again()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);

        var expectedMessage = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var consumer = new InMemoryMessageConsumer(routingKey, bus, new FakeTimeProvider(), ackTimeout: TimeSpan.FromMilliseconds(1000));
        
        //act
        var receivedMessage = await consumer.ReceiveAsync();
        await consumer.RequeueAsync(receivedMessage.Single(), TimeSpan.Zero);
        
        //assert
        Assert.Single(bus.Stream(routingKey));
        
    }
    
    [Fact]
    public async Task When_requeueing_a_message_with_a_delay_it_should_not_be_available_immediately()
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
        var receivedMessage = await consumer.ReceiveAsync();
        await consumer.RequeueAsync(receivedMessage.Single(), TimeSpan.FromMilliseconds(1000));
        
        //assert
        Assert.Empty(bus.Stream(routingKey));
        
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        
        Assert.Single(bus.Stream(routingKey));
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class AsyncInMemoryConsumerReceiveTests
{
    private readonly InMemoryConsumerRequeueTests _inMemoryConsumerRequeueTests = new InMemoryConsumerRequeueTests();
    private readonly InMemoryConsumerAcknowledgeTests _inMemoryConsumerAcknowledgeTests = new InMemoryConsumerAcknowledgeTests();
    private readonly InMemoryConsumerRejectTests _inMemoryConsumerRejectTests = new InMemoryConsumerRejectTests();

    [Fact]
    public async Task When_reading_messages_via_a_consumer()
    {
        //arrange
        const string myTopic = "my topic";
        var routingKey = new RoutingKey(myTopic);
        
        var expectedMessage = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));


        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var consumer = new InMemoryMessageConsumer(routingKey, bus, new FakeTimeProvider(), TimeSpan.FromMilliseconds(1000));
        
        //act
        var receivedMessage = await consumer.ReceiveAsync();
        var message = receivedMessage.Single();
        await consumer.AcknowledgeAsync(message);

        //assert
        Assert.Equal(expectedMessage, message);
        Assert.Empty(bus.Stream(routingKey));
    }
}

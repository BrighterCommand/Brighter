using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class InMemoryConsumerReceiveTests
{
    private readonly InMemoryConsumerRequeueTests _inMemoryConsumerRequeueTests = new InMemoryConsumerRequeueTests();
    private readonly InMemoryConsumerAcknowledgeTests _inMemoryConsumerAcknowledgeTests = new InMemoryConsumerAcknowledgeTests();
    private readonly InMemoryConsumerRejectTests _inMemoryConsumerRejectTests = new InMemoryConsumerRejectTests();

    [Test]
    public async Task When_reading_messages_via_a_consumer()
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
        var receivedMessage = (await consumer.ReceiveAsync()).Single();
        await consumer.AcknowledgeAsync(receivedMessage);

        //assert
        await Assert.That(receivedMessage).IsEqualTo(expectedMessage);
        await Assert.That(bus.Stream(routingKey)).IsEmpty();
    }
}

using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Producer;

public class InMemoryMessageProducerTests
{
    [Fact]
    public void When_adding_messages_to_the_producer()
    {
        // arrange
        const string topic = "test_topic";
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(topic), MessageType.MT_DOCUMENT), new MessageBody("test_content"));
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, new FakeTimeProvider(), instrumentationOptions:InstrumentationOptions.All);

        // act
        producer.Send(message);

        // assert
        var messages = bus.Stream(new RoutingKey(topic));
        Assert.Single(messages);
        Assert.Equal(message.Id, messages.First().Id);
    }
}

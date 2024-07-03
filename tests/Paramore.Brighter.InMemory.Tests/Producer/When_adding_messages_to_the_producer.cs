using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.InMemory.Tests.Producer;

public class InMemoryProducerTests 
{
   public void When_adding_messages_to_the_producer()
   {
       // arrange
       const string topic = "test_topic";
       var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_DOCUMENT), new MessageBody("test_content"));
       var bus = new InternalBus();
       var producer = new InMemoryProducer(bus, new FakeTimeProvider());

       // act
       producer.Send(message);

       // assert
       var messages = bus.Stream(new RoutingKey(topic)); 
       messages.Should().HaveCount(1);
       messages.First().Id.Should().Be(message.Id);
   }
}

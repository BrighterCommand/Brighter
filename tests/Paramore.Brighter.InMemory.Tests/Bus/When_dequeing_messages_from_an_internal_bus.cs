using System;

namespace Paramore.Brighter.InMemory.Tests.Bus;

public class InternalBusDequeueTests
{ 
   [Test]
   public async Task When_dequeing_messages_from_an_internal_bus ()
   {
      // arrange
      var routingKey = new RoutingKey("test");
      var internalBus = new InternalBus();
      
      const string body = "test_content";
      var messageId = Guid.NewGuid().ToString();

      internalBus.Enqueue(new Message(
          new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND), 
          new MessageBody(body))
      );
      
      //act
      var message = internalBus.Dequeue(routingKey);
      
      //assert
      await Assert.That(message).IsNotNull();
      await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
      await Assert.That(message.Body.Value).IsEqualTo(body);
      await Assert.That(message.Header.Topic).IsEqualTo(routingKey);
      await Assert.That(message.Header.MessageId).IsEqualTo(messageId);
   }
   
   [Test]
   public async Task When_dequeing_messages_from_an_internal_bus_and_no_messages()
   {
      // arrange
      const string topic = "test";
      var internalBus = new InternalBus();
      
      //act
      var message = internalBus.Dequeue(new RoutingKey(topic));
      
      //assert
      await Assert.That(message).IsNotNull();
      await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
   }
}

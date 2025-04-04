using System;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Bus;

public class InternalBusDequeueTests
{ 
   [Fact]
   public void When_dequeing_messages_from_an_internal_bus ()
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
      Assert.NotNull(message);
      Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
      Assert.Equal(body, message.Body.Value);
      Assert.Equal(routingKey, message.Header.Topic);
      Assert.Equal(messageId, message.Header.MessageId);
   }
   
   [Fact]
   public void When_dequeing_messages_from_an_internal_bus_and_no_messages()
   {
      // arrange
      const string topic = "test";
      var internalBus = new InternalBus();
      
      //act
      var message = internalBus.Dequeue(new RoutingKey(topic));
      
      //assert
      Assert.NotNull(message);
      Assert.Equal(MessageType.MT_NONE, message.Header.MessageType);
   }
}

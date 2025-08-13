using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class InMemoryOutboxTests
    {
        private FakeTimeProvider _timeProvider = new();

        [Fact]
        public void When_reading_from_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox(_timeProvider) { Tracer = new BrighterTracer() };

            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            //Act
            var context = new RequestContext();
            outbox.Add(messageToAdd, context);

            var retrievedMessage = outbox.Get(messageId, context);
            
            //Assert
            Assert.NotNull(retrievedMessage);
            Assert.Equal(messageId, retrievedMessage.Id);
            Assert.Equal(messageToAdd.Header.Topic, retrievedMessage.Header.Topic);
            Assert.Equal(messageToAdd.Header.MessageType, retrievedMessage.Header.MessageType);
            Assert.Equal(messageToAdd.Body.Value, retrievedMessage.Body.Value);

        }

        [Fact]
        public void When_marking_dispatched_in_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox(_timeProvider){Tracer = new BrighterTracer()};
            
            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            var context = new RequestContext();
            outbox.Add(messageToAdd, context);
            var dispatchedAt = _timeProvider.GetUtcNow();
            outbox.MarkDispatched(messageId, context, dispatchedAt);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(10));

            var dispatchedMessages = outbox.DispatchedMessages(TimeSpan.FromSeconds(5), context);

            //Assert
            IEnumerable<Message> collection = dispatchedMessages as Message[] ?? dispatchedMessages.ToArray();
            Assert.Single(collection);
            Assert.Equal(messageId, collection.First().Id);

        }

        [Fact]
        public void When_looking_for_undispatched_messages_in_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox(_timeProvider){Tracer = new BrighterTracer()};
            
            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            var context = new RequestContext();
            outbox.Add(messageToAdd, context);
            
            _timeProvider.Advance(TimeSpan.FromMilliseconds(500));

            var outstandingMessages = outbox.OutstandingMessages(TimeSpan.Zero, context);
            
            //Assert
            IEnumerable<Message> collection = outstandingMessages as Message[] ?? outstandingMessages.ToArray();
            Assert.Single(collection);
            Assert.Equal(messageId, collection.First().Id);

        }

        [Fact]
        public void When_there_are_multiple_items_retrieve_by_id()
        {
            //Arrange
            var outbox = new InMemoryOutbox( _timeProvider){Tracer = new BrighterTracer()};

            var messageIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), };
            var context = new RequestContext();
            for(int i =0; i <= 4; i++)
            {
                RequestContext requestContext = context;
                outbox.Add(new MessageTestDataBuilder().WithId(messageIds[i]), requestContext);
            }

            //Act 
            var message = outbox.Get(messageIds[2], context);
            
            //Assert
            Assert.Equal(messageIds[2], message.Id);
        }

        [Fact]
        public void When_there_are_multiple_items_and_some_are_dispatched()
        {
            //Arrange
            var outbox = new InMemoryOutbox(_timeProvider){Tracer = new BrighterTracer()};

            var messageIds = new string[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), };
            var context = new RequestContext();
            for(int i =0; i <= 4; i++)
            {
                RequestContext requestContext = context;
                outbox.Add(new MessageTestDataBuilder().WithId(messageIds[i]), requestContext);
            }

            //Act 
            var now = _timeProvider.GetUtcNow();
            outbox.MarkDispatched(messageIds[0], context, now);
            outbox.MarkDispatched(messageIds[4], context, now);

            _timeProvider.Advance(TimeSpan.FromSeconds(10));

            var sentMessages = outbox.DispatchedMessages(TimeSpan.FromSeconds(5), context);
            var outstandingMessages = outbox.OutstandingMessages(TimeSpan.Zero, context);

            //Assert
            var messages = sentMessages as Message[] ?? sentMessages.ToArray();
            Assert.Equal(2, messages.Length);
            Assert.Contains(messages, msg => msg.Id == messageIds[0]);
            Assert.Contains(messages, msg => msg.Id == messageIds[4]);

             var collection = outstandingMessages as Message[] ?? outstandingMessages.ToArray();
            Assert.Equal(3, collection.Length);
            Assert.Contains(collection, msg => msg.Id == messageIds[1]);
            Assert.Contains(collection, msg => msg.Id == messageIds[2]);
            Assert.Contains(collection, msg => msg.Id == messageIds[3]);        }
   }
}

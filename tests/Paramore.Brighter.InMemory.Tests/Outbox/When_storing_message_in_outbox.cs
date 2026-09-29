using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Category("InMemory")]
    public class InMemoryOutboxTests
    {
        private FakeTimeProvider _timeProvider = new();

        [Test]
        public async Task When_reading_from_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox(_timeProvider) { Tracer = new BrighterTracer() };

            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT),
                new MessageBody("message body"));

            //Act
            var context = new RequestContext();
            await outbox.AddAsync(messageToAdd, context);

            var retrievedMessage = await outbox.GetAsync(messageId, context);

            //Assert
            await Assert.That(retrievedMessage).IsNotNull();
            await Assert.That(retrievedMessage.Id).IsEqualTo(messageId);
            await Assert.That(retrievedMessage.Header.Topic).IsEqualTo(messageToAdd.Header.Topic);
            await Assert.That(retrievedMessage.Header.MessageType).IsEqualTo(messageToAdd.Header.MessageType);
            await Assert.That(retrievedMessage.Body.Value).IsEqualTo(messageToAdd.Body.Value);

        }

        [Test]
        public async Task When_marking_dispatched_in_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox(_timeProvider){Tracer = new BrighterTracer()};

            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT),
                new MessageBody("message body"));


            //Act
            var context = new RequestContext();
            await outbox.AddAsync(messageToAdd, context);
            var dispatchedAt = _timeProvider.GetUtcNow();
            await outbox.MarkDispatchedAsync(messageId, context, dispatchedAt);

            _timeProvider.Advance(TimeSpan.FromSeconds(10));

            var dispatchedMessages = await outbox.DispatchedMessagesAsync(TimeSpan.FromSeconds(5), context);

            //Assert
            IEnumerable<Message> collection = dispatchedMessages as Message[] ?? dispatchedMessages.ToArray();
            await Assert.That(collection).HasSingleItem();
            await Assert.That(collection.First().Id).IsEqualTo(messageId);

        }

        [Test]
        public async Task When_looking_for_undispatched_messages_in_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox(_timeProvider){Tracer = new BrighterTracer()};

            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT),
                new MessageBody("message body"));


            //Act
            var context = new RequestContext();
            await outbox.AddAsync(messageToAdd, context);

            _timeProvider.Advance(TimeSpan.FromMilliseconds(500));

            var outstandingMessages = await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);

            //Assert
            IEnumerable<Message> collection = outstandingMessages as Message[] ?? outstandingMessages.ToArray();
            await Assert.That(collection).HasSingleItem();
            await Assert.That(collection.First().Id).IsEqualTo(messageId);

        }

        [Test]
        public async Task When_there_are_multiple_items_retrieve_by_id()
        {
            //Arrange
            var outbox = new InMemoryOutbox( _timeProvider){Tracer = new BrighterTracer()};

            var messageIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), };
            var context = new RequestContext();
            for(int i =0; i <= 4; i++)
            {
                RequestContext requestContext = context;
                await outbox.AddAsync(new MessageTestDataBuilder().WithId(messageIds[i]), requestContext);
            }

            //Act
            var message = await outbox.GetAsync(messageIds[2], context);

            //Assert
            await Assert.That(message.Id).IsEqualTo(messageIds[2]);
        }

        [Test]
        public async Task When_there_are_multiple_items_and_some_are_dispatched()
        {
            //Arrange
            var outbox = new InMemoryOutbox(_timeProvider){Tracer = new BrighterTracer()};

            var messageIds = new string[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), };
            var context = new RequestContext();
            for(int i =0; i <= 4; i++)
            {
                RequestContext requestContext = context;
                await outbox.AddAsync(new MessageTestDataBuilder().WithId(messageIds[i]), requestContext);
            }

            //Act
            var now = _timeProvider.GetUtcNow();
            await outbox.MarkDispatchedAsync(messageIds[0], context, now);
            await outbox.MarkDispatchedAsync(messageIds[4], context, now);

            _timeProvider.Advance(TimeSpan.FromSeconds(10));

            var sentMessages = await outbox.DispatchedMessagesAsync(TimeSpan.FromSeconds(5), context);
            var outstandingMessages = await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);

            //Assert
            var messages = sentMessages as Message[] ?? sentMessages.ToArray();
            await Assert.That(messages.Length).IsEqualTo(2);
            await Assert.That((messages).Any(msg => msg.Id == messageIds[0])).IsTrue();
            await Assert.That((messages).Any(msg => msg.Id == messageIds[4])).IsTrue();

             var collection = outstandingMessages as Message[] ?? outstandingMessages.ToArray();
            await Assert.That(collection.Length).IsEqualTo(3);
            await Assert.That((collection).Any(msg => msg.Id == messageIds[1])).IsTrue();
            await Assert.That((collection).Any(msg => msg.Id == messageIds[2])).IsTrue();
            await Assert.That((collection).Any(msg => msg.Id == messageIds[3])).IsTrue();        }
   }
}

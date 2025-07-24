using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway
{
    [Trait("Category", "MSSQL")]
    public class PostMessageTestAsync : IAsyncDisposable, IDisposable
    {
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topicName = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly IAmAMessageConsumerAsync _consumer;

        public PostMessageTestAsync()
        {
            var testHelper = new MsSqlTestHelper();
            testHelper.SetupQueueDb();

            var routingKey = new RoutingKey(_topicName);

            var sub = new Subscription<MyCommand>(
                new SubscriptionName(_queueName),
                new ChannelName(_topicName), routingKey,
                messagePumpType: MessagePumpType.Proactor);

            _producerRegistry = new MsSqlProducerRegistryFactory(
                testHelper.QueueConfiguration,
                [new() { Topic = routingKey }]
            ).CreateAsync().Result;
            _consumer = new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).CreateAsync(sub);
        }

        [Fact]
        public async Task When_a_message_is_sent_keep_order()
        {
            IAmAMessageConsumerAsync consumer = _consumer;
            var messageId = Guid.NewGuid().ToString();
            var messageType = MessageType.MT_COMMAND;
            var source = new Uri("http://testing.example");
            var type = new CloudEventsType("test-type");
            var timestamp = DateTimeOffset.UtcNow;
            var correlationId = Guid.NewGuid().ToString();
            var replyTo = new RoutingKey("reply-queue");
            var contentType = new ContentType(MediaTypeNames.Application.Json);
            var handledCount = 5;
            var dataSchema = new Uri("http://schema.example");
            var subject = "test-subject";
            var delayed = TimeSpan.FromSeconds(30);
            var traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
            var traceState = "congo=t61rcWkgMzE";
            var baggage = new Baggage();
            baggage.LoadBaggage("userId=alice");

            var routingKey = new RoutingKey(_topicName);
            var header = new MessageHeader(
                messageId: messageId,
                topic: routingKey,
                messageType: messageType,
                source: source,
                type: type,
                timeStamp: timestamp,
                correlationId: correlationId,
                replyTo: replyTo,
                contentType: contentType,
                handledCount: handledCount,
                dataSchema: dataSchema,
                subject: subject,
                delayed: delayed,
                traceParent: traceParent,
                traceState: traceState,
                baggage: baggage);

            var body = new MessageBody($"test content [{_queueName}]");
            
            await ((IAmAMessageProducerAsync)_producerRegistry.LookupAsyncBy(routingKey)).SendAsync(new Message(
                header,
                body));
            var msgId = messageId;

            //Now read those messages in order

            var firstMessage = await ConsumeMessagesAsync(consumer);
            var message = firstMessage.First();
            Assert.False(message.IsEmpty);
            Assert.Equal(msgId, message.Id);
        }

        private async Task<IEnumerable<Message>> ConsumeMessagesAsync(IAmAMessageConsumerAsync consumer)
        {
            var messages = Array.Empty<Message>();
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    messages = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        break;
                }
                catch (ChannelFailureException)
                {
                }
            } while (maxTries <= 3);

            return messages;
        }

        public void Dispose()
        {
            _producerRegistry.Dispose();
            ((IAmAMessageConsumerSync)_consumer).Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            _producerRegistry.Dispose();
            await _consumer.DisposeAsync();
        }
    }
}

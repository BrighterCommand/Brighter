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
    public class PostMessageTest : IAsyncDisposable, IDisposable
    {
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topicName = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly IAmAMessageConsumerSync _consumer;

        public PostMessageTest()
        {
            var testHelper = new MsSqlTestHelper();
            testHelper.SetupQueueDb();

            var routingKey = new RoutingKey(_topicName);

            var sub = new Subscription<MyCommand>(
                new SubscriptionName(_queueName),
                new ChannelName(_topicName), routingKey,
                messagePumpType: MessagePumpType.Reactor);

            _producerRegistry = new MsSqlProducerRegistryFactory(
                testHelper.QueueConfiguration,
                [new() { Topic = routingKey }]
            ).Create();
            _consumer = new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).Create(sub);
        }

        [Fact]
        public void When_a_message_is_sent_keep_order()
        {
            IAmAMessageConsumerSync consumer = _consumer;
            
            var messageId = Guid.NewGuid().ToString();
            var messageType = MessageType.MT_COMMAND;
            var source = new Uri("http://testing.example");
            var type = new CloudEventsType("test-type");
            var timestamp = DateTimeOffset.UtcNow;
            var correlationId = Guid.NewGuid().ToString();
            var replyTo = new RoutingKey("reply-queue");
            var contentType = new ContentType(MediaTypeNames.Text.Plain);
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
            
            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey)).Send(new Message(
                header,
                body));

            var firstMessage = ConsumeMessages(consumer);
            var message = firstMessage.First();
            Assert.False(message.IsEmpty);
            Assert.Equal(messageId, message.Id);
        }

        private IEnumerable<Message> ConsumeMessages(IAmAMessageConsumerSync consumer)
        {
            var messages = Array.Empty<Message>();
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        break;
                }
                catch (ChannelFailureException)
                {
                }
            } while (maxTries <= 3);

            return messages;
        }

        public async ValueTask DisposeAsync()
        {
            await ((IAmAMessageConsumerAsync)_consumer).DisposeAsync();
            _producerRegistry.Dispose();
        }

        public void Dispose()
        {
            _consumer?.Dispose();
            _producerRegistry?.Dispose();
        }
    }
}

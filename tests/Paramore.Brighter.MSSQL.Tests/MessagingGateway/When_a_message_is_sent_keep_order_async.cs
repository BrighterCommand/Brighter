using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway
{
    [Trait("Category", "MSSQL")]
    public class OrderTestAsync : IAsyncDisposable, IDisposable
    {
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topicName = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly IAmAMessageConsumerAsync _consumer;

        public OrderTestAsync()
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
            //Send a sequence of messages to Kafka
            var msgId = await SendMessageAsync();
            var msgId2 = await SendMessageAsync();
            var msgId3 = await SendMessageAsync();
            var msgId4 = await SendMessageAsync();

            //Now read those messages in order

            var firstMessage = await ConsumeMessagesAsync(consumer);
            var message = firstMessage.First();
            Assert.False(message.Empty);
            Assert.Equal(msgId, message.Id);

            var secondMessage = await ConsumeMessagesAsync(consumer);
            message = secondMessage.First();
            Assert.False(message.Empty);
            Assert.Equal(msgId2, message.Id);

            var thirdMessages = await ConsumeMessagesAsync(consumer);
            message = thirdMessages.First();
            Assert.False(message.Empty);
            Assert.Equal(msgId3, message.Id);

            var fourthMessage = await ConsumeMessagesAsync(consumer);
            message = fourthMessage.First();
            Assert.False(message.Empty);
            Assert.Equal(msgId4, message.Id);
        }

        private async Task<string> SendMessageAsync()
        {
            var messageId = Guid.NewGuid().ToString();

            var routingKey = new RoutingKey(_topicName);
            await ((IAmAMessageProducerAsync)_producerRegistry.LookupAsyncBy(routingKey)).SendAsync(new Message(
                new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND),
                new MessageBody($"test content [{_queueName}]")));

            return messageId;
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
                    //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                    //_output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
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

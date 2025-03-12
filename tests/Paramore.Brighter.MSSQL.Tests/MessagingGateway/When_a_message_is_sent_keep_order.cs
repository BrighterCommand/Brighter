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
    public class OrderTest : IAsyncDisposable, IDisposable
    {
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topicName = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly IAmAMessageConsumerSync _consumer;

        public OrderTest()
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
            //Send a sequence of messages to Kafka
            var msgId = SendMessage();
            var msgId2 = SendMessage();
            var msgId3 = SendMessage();
            var msgId4 = SendMessage();

            //Now read those messages in order

            var firstMessage = ConsumeMessages(consumer);
            var message = firstMessage.First();
            Assert.False(message.IsEmpty);
            Assert.Equal(msgId, message.Id);

            var secondMessage = ConsumeMessages(consumer);
            message = secondMessage.First();
            Assert.False(message.IsEmpty);
            Assert.Equal(msgId2, message.Id);

            var thirdMessages = ConsumeMessages(consumer);
            message = thirdMessages.First();
            Assert.False(message.IsEmpty);
            Assert.Equal(msgId3, message.Id);

            var fourthMessage = ConsumeMessages(consumer);
            message = fourthMessage.First();
            Assert.False(message.IsEmpty);
            Assert.Equal(msgId4, message.Id);
        }

        private string SendMessage()
        {
            var messageId = Guid.NewGuid().ToString();

            var routingKey = new RoutingKey(_topicName);
            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey)).Send(new Message(
                new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND),
                new MessageBody($"test content [{_queueName}]")));

            return messageId;
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
                    //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                    //_output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
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

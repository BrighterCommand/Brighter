using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway
{
    [Trait("Category", "MSSQL")]
    public class OrderTest
    {
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry; 
        private readonly IAmAMessageConsumer _consumer;

        public OrderTest()
        {
            var testHelper = new MsSqlTestHelper();
            testHelper.SetupQueueDb();

            var sub = new Subscription<MyCommand>(new SubscriptionName(_queueName),
                new ChannelName(_topic), new RoutingKey(_topic));
            _producerRegistry = new MsSqlProducerRegistryFactory(
                testHelper.QueueConfiguration, 
                new Publication[] {new Publication {Topic = new RoutingKey(_topic)}}
            ).Create();
            _consumer = new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).Create(sub);
        }

        [Fact]
        public void When_a_message_is_sent_keep_order()
        {
            IAmAMessageConsumer consumer = _consumer;
            try
            {
                //Send a sequence of messages to Kafka
                var msgId = SendMessage();
                var msgId2 = SendMessage();
                var msgId3 = SendMessage();
                var msgId4 = SendMessage();
                
                //Now read those messages in order

                var firstMessage = ConsumeMessages(consumer);
                var message = firstMessage.First();
                message.Id.Should().Be(msgId);

                var secondMessage = ConsumeMessages(consumer);
                message = secondMessage.First();
                message.Id.Should().Be(msgId2);

                var thirdMessages = ConsumeMessages(consumer);
                message = thirdMessages.First();
                message.Id.Should().Be(msgId3);

                var fourthMessage = ConsumeMessages(consumer);
                message = fourthMessage.First();
                message.Id.Should().Be(msgId4);

            }
            finally
            {
                consumer?.Dispose();
            }
        }

        private Guid SendMessage()
        {
            var messageId = Guid.NewGuid();

            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_topic)).Send(new Message(
                new MessageHeader(messageId, _topic, MessageType.MT_COMMAND),
                new MessageBody($"test content [{_queueName}]")));

            return messageId;
        }
        private IEnumerable<Message> ConsumeMessages(IAmAMessageConsumer consumer)
        {
            var messages = new Message[0];
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    messages = consumer.Receive(1000);

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        break;
                }
                catch (ChannelFailureException)
                {
                    //Lots of reasons to be here as Kafka propogates a topic, or the test cluster is still initializing
                    //_output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                }
            } while (maxTries <= 3);

            return messages;
        }

    }
}

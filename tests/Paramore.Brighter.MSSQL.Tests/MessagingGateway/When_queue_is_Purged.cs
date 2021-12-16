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
    public class PurgeTest
    {
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly IAmAMessageProducerSync _producer;
        private readonly IAmAMessageConsumer _consumer;

        public PurgeTest()
        {
            var testHelper = new MsSqlTestHelper();
            testHelper.SetupQueueDb();

            var sub = new Subscription<MyCommand>(new SubscriptionName(_queueName),
                new ChannelName(_topic), new RoutingKey(_topic));
            _producer = new MsSqlProducerRegistryFactory(testHelper.QueueConfiguration).Create();
            _consumer = new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).Create(sub);
        }

        [Fact]
        public void When_queue_is_Purged()
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

                _consumer.Purge();

                //Next Message should not exists (default will be returned)

                var nextMessage = ConsumeMessages(consumer);
                message = nextMessage.First();
                
                Assert.Equal(new Message(), message);
            }
            finally
            {
                consumer?.Dispose();
            }
        }

        private Guid SendMessage()
        {
            var messageId = Guid.NewGuid();

            _producer.Send(new Message(
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

    public class ExampleCommand : ICommand
    {

        public Guid Id { get; set; }

        public ExampleCommand()
        {
            Id = new Guid();
        }
    }
}

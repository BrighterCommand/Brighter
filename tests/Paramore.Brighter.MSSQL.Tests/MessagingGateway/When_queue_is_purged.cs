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
    public class PurgeTest :  IAsyncDisposable, IDisposable
    {
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry; 
        private readonly IAmAMessageConsumerSync _consumer;
        private readonly RoutingKey _routingKey;

        public PurgeTest()
        {
            var testHelper = new MsSqlTestHelper();
            testHelper.SetupQueueDb();

            _routingKey = new RoutingKey(Guid.NewGuid().ToString());
            
            var sub = new Subscription<MyCommand>(
                new SubscriptionName(_queueName),
                new ChannelName(_routingKey.Value), _routingKey,
                messagePumpType: MessagePumpType.Reactor);
            
            _producerRegistry = new MsSqlProducerRegistryFactory(
                testHelper.QueueConfiguration,
                [new() {Topic = _routingKey}]
            ).Create();
            _consumer = new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).Create(sub);
        }

        [Fact]
        public void When_queue_is_Purged()
        {
            IAmAMessageConsumerSync consumer = _consumer;
                //Send a sequence of messages to Kafka
                var msgId = SendMessage();
                
                //Now read those messages in order

                var firstMessage = ConsumeMessages(consumer);
                var message = firstMessage.First();
                Assert.Equal(msgId, message.Id);

                _consumer.Purge();

                //Next Message should not exists (default will be returned)

                var nextMessage = ConsumeMessages(consumer);
                message = nextMessage.First();
                
                Assert.Equal(new Message(), message);
        }

        private string SendMessage()
        {
            var messageId = Guid.NewGuid().ToString();
            
            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_routingKey)).Send(new Message(
                new MessageHeader(messageId, _routingKey, MessageType.MT_COMMAND),
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
            _consumer.Dispose();
            _producerRegistry.Dispose();
        }
    }
}

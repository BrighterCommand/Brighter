using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    public class KafkaMessageConsumerUpdateOffset : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAMessageProducer _producer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();
        private readonly string _kafkaGroupId = Guid.NewGuid().ToString();


        public KafkaMessageConsumerUpdateOffset(ITestOutputHelper output)
        {
            _output = output;
            _producer = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test", BootStrapServers = new[] {"localhost:9092"}
                }).Create();
        }

        [Fact]
        public void When_a_message_is_acknowldgede_update_offset()
        {
            IAmAMessageConsumer consumer = null;
            IAmAMessageConsumer newConsumer = null;
            try
            {
                var msgId = SendMessage();
                consumer = CreateConsumer();
                var messages = ConsumeMessages(consumer);

                var message = messages.First();
                message.Id.Should().Be(msgId);
                consumer.Acknowledge(message);

                consumer.Dispose();

                var msgId2 = SendMessage();
                newConsumer = CreateConsumer();
                var newMessages = ConsumeMessages(newConsumer);

                var secondMessage = newMessages.First();
                secondMessage.Id.Should().Be(msgId2);
                consumer.Acknowledge(secondMessage);
            }
            finally
            {
                newConsumer?.Dispose();
            }
        }

        private Guid SendMessage()
        {
            var messageId = Guid.NewGuid();

            _producer.Send(new Message(
                new MessageHeader(messageId, _topic, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
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
                    Task.Delay(500).Wait(); //Let topic propogate in the broker
                    messages = consumer.Receive(1000);

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        break;
                }
                catch (ChannelFailureException cfx)
                {
                    //Lots of reasons to be here as Kafka propogates a topic, or the test cluster is still initializing
                    _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                }
            } while (maxTries <= 3);

            return messages;
        }

        private IAmAMessageConsumer CreateConsumer()
        {
            return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = new[] { "localhost:9092" }
                },
                new KafkaConsumerConfiguration
                {
                    GroupId = _kafkaGroupId,
                    OffsetDefault = AutoOffsetReset.Earliest,
                    CommitBatchSize = 1
                }
                ).Create(new Connection<MyCommand>(
                    channelName: new ChannelName(_queueName), 
                    routingKey: new RoutingKey(_topic)
                )
            );
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}

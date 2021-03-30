using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
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
                    Name = "Kafka Producer Send Test", 
                    BootStrapServers = new[] {"localhost:9092"}
                },
                new KafkaPublication()
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }).Create();
        }

        //[Fact (Skip = "Due to requirement to yield for offsets, don't run in CI. Manually enable") ]
        //[Fact]
        public async Task When_a_message_is_acknowldgede_update_offset()
        {
            var groupId = Guid.NewGuid().ToString();
            
            //send x messages to Kafka
            var sentMessages = new Guid[10];
            for (int i = 0; i < 10; i++)
            {
                var msgId = Guid.NewGuid();
                SendMessage(msgId);
                sentMessages[i] = msgId;
            }

            //This will create, then delete the consumer
            Message[] messages = ConsumeMessages(groupId: groupId, batchLimit: 5);

            //check we read the first 5 messages
            messages.Length.Should().Be(5);
            for (int i = 0; i < 5; i++)
            {
                messages[i].Id.Should().Be(sentMessages[i]);
            }

            //yield to broker to catch up
            await Task.Delay(TimeSpan.FromSeconds(5));

            //This will create a new consumer
            Message[] newMessages = ConsumeMessages(groupId, batchLimit: 5);
            //check we read the first 5 messages
            messages.Length.Should().Be(5);
            for (int i = 0; i < 5; i++)
            {
                newMessages[i].Id.Should().Be(sentMessages[i+5]);
            }
        }

        private void SendMessage(Guid messageId)
        {
            _producer.Send(new Message(
                new MessageHeader(messageId, _topic, MessageType.MT_COMMAND) {PartitionKey = _partitionKey},
                new MessageBody($"test content [{_queueName}]")));
        }

        private Message[] ConsumeMessages(string groupId, int batchLimit)
        {
            var consumedMessages = new List<Message>();
            using (IAmAMessageConsumer consumer = CreateConsumer(groupId))
            {
                for (int i = 0; i < batchLimit; i++)
                {
                    consumedMessages.Add(ConsumeMessage(consumer));
                }
            }

            return consumedMessages.ToArray();

            Message ConsumeMessage(IAmAMessageConsumer consumer)
            {
                Message[] messages = new []{new Message()};
                int maxTries = 0;
                do
                {
                    try
                    {
                        maxTries++;
                        Task.Delay(500).Wait(); //Let topic propogate in the broker
                        messages = consumer.Receive(1000);

                        if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        {
                            consumer.Acknowledge(messages[0]);
                            return messages[0];
                        }

                    }
                    catch (ChannelFailureException cfx)
                    {
                        //Lots of reasons to be here as Kafka propogates a topic, or the test cluster is still initializing
                        _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                    }
                } while (maxTries <= 3);

                return messages[0];
            }
        }

        private IAmAMessageConsumer CreateConsumer(string groupId)
        {
            return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test", 
                    BootStrapServers = new[] {"localhost:9092"}
                })
                .Create(new KafkaSubscription<MyCommand>
                (
                    name: new SubscriptionName("Paramore.Brighter.Tests"),
                    channelName: new ChannelName(_queueName),
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    offsetDefault: AutoOffsetReset.Earliest,
                    commitBatchSize:5,
                    numOfPartitions: 1,
                    replicationFactor: 1,
                    makeChannels: OnMissingChannel.Create
                ));
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}

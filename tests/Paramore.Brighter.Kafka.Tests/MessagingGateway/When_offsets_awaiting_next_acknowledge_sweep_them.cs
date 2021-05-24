using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Trait("Category", "Kafka")]
    [Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
    public class KafkaMessageConsumerSweepOffsets : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAMessageProducer _producer;
        private readonly KafkaMessageConsumer _consumer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();
        private readonly string _kafkaGroupId = Guid.NewGuid().ToString();

        public KafkaMessageConsumerSweepOffsets(ITestOutputHelper output)
        {
            const string groupId = "Kafka Message Producer Sweep Test";
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
                    RequestTimeoutMs = 2000
                }).Create();
            
            _consumer = (KafkaMessageConsumer)new KafkaMessageConsumerFactory(
                    new KafkaMessagingGatewayConfiguration
                    {
                        Name = "Kafka Consumer Test",
                        BootStrapServers = new[] { "localhost:9092" }
                    })
                .Create(new KafkaSubscription<MyCommand>(
                        channelName: new ChannelName(_queueName), 
                        routingKey: new RoutingKey(_topic),
                        groupId: groupId,
                        commitBatchSize: 20,  //This large commit batch size may never be sent
                        sweepUncommittedOffsetsIntervalMs: 10000,
                        numOfPartitions: 1,
                        replicationFactor: 1,
                        makeChannels: OnMissingChannel.Create
                    )
                );
        }

        [Fact]
        public void When_a_message_is_acknowldeged_but_no_batch_sent_sweep_offsets()
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

            var consumedMessages = new List<Message>();
            for (int j = 0; j < 9; j++)
            {
                consumedMessages.Add(ReadMessage());
            }

            consumedMessages.Count.Should().Be(9);
            _consumer.StoredOffsets().Should().Be(9);

            //Let time elapse with no activity
            Task.Delay(10000).Wait();
            
            //This should trigger a sweeper run (can be fragile when non scheduled in containers etc)
            consumedMessages.Add(ReadMessage());
            
            //Let the sweeper run, can be slow in CI environments to run the thread
            //Let the sweeper run, can be slow in CI environments to run the thread
            Task.Delay(10000).Wait();
            

            //Sweeper will commit these
            _consumer.StoredOffsets().Should().Be(0);
            
            Message ReadMessage()
            {
                Message[] messages = new []{new Message()};
                int maxTries = 0;
                do
                {
                    try
                    {
                        maxTries++;
                        Task.Delay(500).Wait(); //Let topic propogate in the broker
                        messages = _consumer.Receive(1000);

                        if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        {
                            _consumer.Acknowledge(messages[0]);
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

        private void SendMessage(Guid messageId)
        {
            _producer.Send(new Message(
                new MessageHeader(messageId, _topic, MessageType.MT_COMMAND) {PartitionKey = _partitionKey},
                new MessageBody($"test content [{_queueName}]")));
        }

  

    

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}

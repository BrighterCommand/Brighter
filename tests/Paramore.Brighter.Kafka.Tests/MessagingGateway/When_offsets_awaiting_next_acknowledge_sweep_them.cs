using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerSweepOffsets : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly KafkaMessageConsumer _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageConsumerSweepOffsets(ITestOutputHelper output)
    {
        const string groupId = "Kafka Message Producer Sweep Test";
        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test", 
                BootStrapServers = new[] {"localhost:9092"}
            },
            new[] {new KafkaPublication
            {
                Topic = new RoutingKey(_topic),
                NumPartitions = 1,
                ReplicationFactor = 1,
                //These timeouts support running on a container using the same host as the tests, 
                //your production values ought to be lower
                MessageTimeoutMs = 2000,
                RequestTimeoutMs = 2000,
                MakeChannels = OnMissingChannel.Create
            }}).Create();
            
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
                    sweepUncommittedOffsetsInterval: TimeSpan.FromMilliseconds(10000),
                    numOfPartitions: 1,
                    replicationFactor: 1,
                    messagePumpType:  MessagePumpType.Reactor,
                    makeChannels: OnMissingChannel.Create
                )
            );
    }

    [Fact]
    public async Task When_a_message_is_acknowldeged_but_no_batch_sent_sweep_offsets()
    {
        //send x messages to Kafka
        var sentMessages = new string[10];
        for (int i = 0; i < 10; i++)
        {
            var msgId = Guid.NewGuid().ToString();
            SendMessage(msgId);
            sentMessages[i] = msgId;
        }

        var consumedMessages = new List<Message>();
        for (int j = 0; j < 9; j++)
        {
            consumedMessages.Add(await ReadMessageAsync());
        }

        consumedMessages.Count.Should().Be(9);
        _consumer.StoredOffsets().Should().Be(9);

        //Let time elapse with no activity
        await Task.Delay(10000);
            
        //This should trigger a sweeper run (can be fragile when non scheduled in containers etc)
        consumedMessages.Add(await ReadMessageAsync());
            
        //Let the sweeper run, can be slow in CI environments to run the thread
        //Let the sweeper run, can be slow in CI environments to run the thread
        await Task.Delay(10000);

        //Sweeper will commit these
        _consumer.StoredOffsets().Should().Be(0);
            
        async Task<Message> ReadMessageAsync()
        {
            Message[] messages = new []{new Message()};
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    await Task.Delay(500); //Let topic propagate in the broker
                    messages = _consumer.Receive(TimeSpan.FromMilliseconds(1000));

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    {
                        _consumer.Acknowledge(messages[0]);
                        return messages[0];
                    }

                }
                catch (ChannelFailureException cfx)
                {
                    //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                    _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                }
            } while (maxTries <= 3);

            return messages[0];
        }
    }

    private void SendMessage(string messageId)
    {
        var routingKey = new RoutingKey(_topic);
            
        ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey)).Send(new Message(
            new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND) {PartitionKey = _partitionKey},
            new MessageBody($"test content [{_queueName}]")));
    }


    public void Dispose()
    {
        _producerRegistry?.Dispose();
        _consumer.Dispose();
    }
}

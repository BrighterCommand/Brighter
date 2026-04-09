using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Trait("Fragile", "CI")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerCommitRevokeConcurrency : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _groupId = Guid.NewGuid().ToString();

    public KafkaMessageConsumerCommitRevokeConcurrency(ITestOutputHelper output)
    {
        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test",
                BootStrapServers = new[] { "localhost:9092" }
            },
            [
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 3,
                    ReplicationFactor = 1,
                    //These timeouts support running on a container using the same host as the tests,
                    //your production values ought to be lower
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
            ]).Create();
    }

    /// <summary>
    /// Regression test for a race condition where CommitOffsetsFor (called during partition revoke)
    /// could run concurrently with a background batch commit, causing librdkafka Local_BadMsg errors.
    ///
    /// With commitBatchSize: 1, every acknowledge triggers a background commit via Task.Factory.StartNew.
    /// When a rebalance fires during this window, the revoke handler must wait for the in-flight commit
    /// to complete before committing revoked offsets.
    ///
    /// This test is inherently timing-dependent — it maximises the chance of overlap between background
    /// commits and the revoke handler by using commitBatchSize: 1 and rapid message acknowledgement.
    /// </summary>
    [Theory]
    [InlineData(PartitionAssignmentStrategy.RoundRobin)]
    [InlineData(PartitionAssignmentStrategy.CooperativeSticky)]
    public async Task When_committing_offsets_during_revoke_should_not_race_with_background_commit(
        PartitionAssignmentStrategy partitionAssignmentStrategy)
    {
        //allow topic to propagate on the broker
        await Task.Delay(500);

        var routingKey = new RoutingKey(_topic);
        var producer = (IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey);

        //send a burst of messages across partitions
        for (int i = 0; i < 30; i++)
        {
            producer.Send(new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND)
                {
                    PartitionKey = $"key-{i}" //spread across partitions
                },
                new MessageBody($"test content [{_queueName}]")));
        }

        ((KafkaMessageProducer)producer).Flush();

        //commitBatchSize: 1 means every acknowledge fires a background commit thread
        //this maximises the chance of a commit being in-flight when the revoke handler fires
        using var consumerA = CreateConsumer(commitBatchSize: 1, partitionAssignmentStrategy: partitionAssignmentStrategy);

        //consume a few messages to get consumer A established
        for (int j = 0; j < 5; j++)
        {
            var msg = ReadMessage(consumerA);
            if (msg.Header.MessageType != MessageType.MT_NONE)
            {
                consumerA.Acknowledge(msg);
            }
        }

        _output.WriteLine("Consumer A established, starting rapid consume + acknowledge");

        //now start a rapid consume-acknowledge cycle while simultaneously triggering a rebalance
        //by adding consumer B to the group
        Exception? caughtException = null;

        var consumeTask = Task.Run(() =>
        {
            try
            {
                for (int j = 0; j < 20; j++)
                {
                    var msg = ReadMessage(consumerA);
                    if (msg.Header.MessageType != MessageType.MT_NONE)
                    {
                        //each acknowledge fires a background commit (batch size = 1)
                        consumerA.Acknowledge(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                caughtException = ex;
                _output.WriteLine($"Consumer A exception during consume: {ex.Message}");
            }
        });

        //give consumer A a moment to start consuming, then trigger rebalance
        await Task.Delay(500);

        _output.WriteLine("Adding consumer B to trigger rebalance");
        using var consumerB = CreateConsumer(commitBatchSize: 10, partitionAssignmentStrategy: partitionAssignmentStrategy);

        //consumer B polls to join the group and trigger rebalance
        try
        {
            _ = consumerB.Receive(TimeSpan.FromMilliseconds(5000));
        }
        catch (ChannelFailureException)
        {
            //consumer B may get errors during rebalance, that's fine
        }

        await consumeTask;

        //the key assertion: consumer A should not have thrown during the rebalance
        Assert.Null(caughtException);

        //consumer A should still be functional after the rebalance
        _ = consumerA.Receive(TimeSpan.FromMilliseconds(2000));

        _output.WriteLine("Test completed - no race condition errors");
    }

    private KafkaMessageConsumer CreateConsumer(int commitBatchSize,
        PartitionAssignmentStrategy partitionAssignmentStrategy = PartitionAssignmentStrategy.RoundRobin)
    {
        return (KafkaMessageConsumer)new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .Create(new KafkaSubscription<MyCommand>(
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: _groupId,
                commitBatchSize: commitBatchSize,
                sweepUncommittedOffsetsInterval: TimeSpan.FromMinutes(5), //disable sweeper for this test
                numOfPartitions: 3,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create,
                partitionAssignmentStrategy: partitionAssignmentStrategy
            ));
    }

    private Message ReadMessage(KafkaMessageConsumer consumer)
    {
        Message[] messages = [new Message()];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    return messages[0];
                }
            }
            catch (ChannelFailureException cfx)
            {
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                Task.Delay(500).GetAwaiter().GetResult();
            }
        } while (maxTries <= 10);

        return messages[0];
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
    }
}

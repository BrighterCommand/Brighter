using System;
using System.Collections.Generic;
using System.Linq;
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
public class KafkaMessageConsumerCommitOnRevoke : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _groupId = Guid.NewGuid().ToString();

    public KafkaMessageConsumerCommitOnRevoke(ITestOutputHelper output)
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
    /// Verifies that when a partition rebalance revokes partitions from a consumer, the revoke handler
    /// commits outstanding offsets so that no messages are replayed when a new consumer takes over.
    ///
    /// Flow:
    /// 1. Consumer A consumes and acknowledges messages in two batches:
    ///    - First batch hits commitBatchSize and is committed via the normal batch path
    ///    - Second batch is stored but not yet committed (below batch threshold)
    /// 2. Consumer B joins the group, triggering a rebalance and partition revocation on A
    /// 3. The revoke handler on A should commit the outstanding offsets from the second batch
    /// 4. A new consumer C verifies it reads from the fully committed position (no replay)
    /// </summary>
    [Theory]
    [InlineData(PartitionAssignmentStrategy.RoundRobin)]
    [InlineData(PartitionAssignmentStrategy.CooperativeSticky)]
    public async Task When_a_partition_is_revoked_offsets_are_committed(
        PartitionAssignmentStrategy partitionAssignmentStrategy)
    {
        //allow topic to propagate on the broker
        await Task.Delay(500);

        var routingKey = new RoutingKey(_topic);
        var producer = (IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey);

        //send messages to Kafka across partitions
        var sentMessageIds = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            var msgId = Guid.NewGuid().ToString();
            producer.Send(new Message(
                new MessageHeader(msgId, routingKey, MessageType.MT_COMMAND)
                {
                    PartitionKey = $"key-{i}" //spread across partitions
                },
                new MessageBody($"test content [{_queueName}]")));
            sentMessageIds.Add(msgId);
        }

        ((KafkaMessageProducer)producer).Flush();

        //Phase 1: Consumer A owns all 3 partitions
        //commitBatchSize: 5 so the first 5 acks trigger a batch commit to Kafka
        using var consumerA = CreateConsumer(commitBatchSize: 5, partitionAssignmentStrategy: partitionAssignmentStrategy);

        //consume and acknowledge first batch — triggers batch commit
        var firstBatchIds = new List<string>();
        for (int j = 0; j < 5; j++)
        {
            var msg = ReadMessage(consumerA);
            if (msg.Header.MessageType != MessageType.MT_NONE)
            {
                consumerA.Acknowledge(msg);
                firstBatchIds.Add(msg.Id);
            }
        }

        _output.WriteLine($"Consumer A first batch: {firstBatchIds.Count} messages acknowledged and batch-committed");

        //wait for the background batch commit to complete
        await Task.Delay(3000);

        //Phase 2: Consume more messages — stored but NOT committed (below batch threshold)
        var secondBatchIds = new List<string>();
        for (int j = 0; j < 5; j++)
        {
            var msg = ReadMessage(consumerA);
            if (msg.Header.MessageType != MessageType.MT_NONE)
            {
                consumerA.Acknowledge(msg);
                secondBatchIds.Add(msg.Id);
            }
        }

        _output.WriteLine($"Consumer A second batch: {secondBatchIds.Count} messages acknowledged but not batch-committed");
        _output.WriteLine($"Consumer A stored offsets: {consumerA.StoredOffsets()}");

        var allConsumedIds = firstBatchIds.Concat(secondBatchIds).ToHashSet();
        _output.WriteLine($"Total unique messages consumed by A: {allConsumedIds.Count}");

        //Phase 3: Consumer B joins the group — triggers rebalance and revoke on A
        //The revoke handler on A should commit the outstanding offsets from the second batch
        using var consumerB = CreateConsumer(commitBatchSize: 100, partitionAssignmentStrategy: partitionAssignmentStrategy);

        //consumer B polls to join the group
        _ = consumerB.Receive(TimeSpan.FromMilliseconds(5000));

        //consumer A polls to process the revoke callback
        _ = consumerA.Receive(TimeSpan.FromMilliseconds(5000));

        //allow rebalance to settle
        await Task.Delay(5000);

        //poll both once more to ensure rebalance completes
        _ = consumerA.Receive(TimeSpan.FromMilliseconds(2000));
        _ = consumerB.Receive(TimeSpan.FromMilliseconds(2000));

        //close both consumers to release group membership
        consumerA.Close();
        consumerB.Close();

        //Phase 4: Consumer C joins the same group — should start from committed position
        //If revoke committed offsets correctly, C should NOT replay any messages that A consumed
        await Task.Delay(2000);

        using var consumerC = CreateConsumer(commitBatchSize: 100, partitionAssignmentStrategy: partitionAssignmentStrategy);
        var replayedIds = new List<string>();

        //try to read messages — any we get that A already consumed are replays
        for (int j = 0; j < 5; j++)
        {
            var messages = consumerC.Receive(TimeSpan.FromMilliseconds(2000));
            foreach (var msg in messages)
            {
                if (msg.Header.MessageType != MessageType.MT_NONE)
                {
                    if (allConsumedIds.Contains(msg.Id))
                    {
                        replayedIds.Add(msg.Id);
                        _output.WriteLine($"Consumer C replayed message: {msg.Id}");
                    }
                    else
                    {
                        _output.WriteLine($"Consumer C read new message: {msg.Id}");
                    }
                }
            }
        }

        //the second batch offsets should have been committed during revoke — no replays
        var secondBatchReplays = replayedIds.Where(id => secondBatchIds.Contains(id)).ToList();
        _output.WriteLine($"Second batch messages replayed: {secondBatchReplays.Count} of {secondBatchIds.Count}");

        Assert.Empty(secondBatchReplays);
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
                Task.Delay(1000).GetAwaiter().GetResult();
            }
        } while (maxTries <= 10);

        return messages[0];
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
    }
}

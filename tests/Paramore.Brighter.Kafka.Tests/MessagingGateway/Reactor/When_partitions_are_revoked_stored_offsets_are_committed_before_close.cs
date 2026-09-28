#region Licence
/* The MIT License (MIT)
Copyright © 2026 Brighter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

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
public class KafkaMessageConsumerCommitsRevokedOffsetsBeforeClose : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly string _groupId = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;

    public KafkaMessageConsumerCommitsRevokedOffsetsBeforeClose(ITestOutputHelper output)
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
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
            ]).Create();
    }

    /// <summary>
    /// Isolates the revoke handler's own commit behaviour from Close(), which also flushes the
    /// offset bag and would otherwise mask a revoke handler that commits nothing (#4281 Scope
    /// Notes). commitBatchSize and sweepUncommittedOffsetsInterval are both set so neither the
    /// batch path nor the sweeper can fire, so any committed offset observed before Close() can
    /// only have come from the revoke handler.
    /// </summary>
    [Fact]
    public async Task When_partitions_are_revoked_stored_offsets_are_committed_before_close()
    {
        //allow topic to propagate on the broker
        await Task.Delay(500);

        var routingKey = new RoutingKey(_topic);
        var producer = (IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey);

        //Evident Data: 15 messages spread across partitions by distinct keys, never reaching the
        //commitBatchSize of 100, and the sweeper is disabled - only a revoke can commit anything
        const int messageCount = 15;
        for (int i = 0; i < messageCount; i++)
        {
            var msgId = Guid.NewGuid().ToString();
            producer.Send(new Message(
                new MessageHeader(msgId, routingKey, MessageType.MT_COMMAND)
                {
                    PartitionKey = $"key-{i}"
                },
                new MessageBody($"test content [{_queueName}]")));
        }

        ((KafkaMessageProducer)producer).Flush();

        //Consumer A owns all 3 partitions to begin with
        using var consumerA = CreateConsumer();

        var ackedCount = 0;
        for (int j = 0; j < messageCount; j++)
        {
            var msg = ReadMessage(consumerA);
            if (msg.Header.MessageType != MessageType.MT_NONE)
            {
                consumerA.Acknowledge(msg);
                ackedCount++;
            }
        }

        _output.WriteLine($"Consumer A acknowledged {ackedCount} messages, none committed (below batch threshold, sweeper disabled)");
        Assert.True(ackedCount > 0);

        //Consumer B joins the group - triggers rebalance and revoke on A for some partitions
        using var consumerB = CreateConsumer();

        _ = consumerB.Receive(TimeSpan.FromMilliseconds(5000));
        _ = consumerA.Receive(TimeSpan.FromMilliseconds(5000)); //A polls to process the revoke callback

        //allow rebalance to settle
        await Task.Delay(5000);

        _ = consumerA.Receive(TimeSpan.FromMilliseconds(2000));
        _ = consumerB.Receive(TimeSpan.FromMilliseconds(2000));

        //Assert - BEFORE closing either consumer (Close() also flushes the bag, which would mask
        //a revoke handler that commits nothing), the sum of committed offsets across all
        //partitions must account for every message A acknowledged
        var totalCommitted = GetTotalCommittedOffset();
        _output.WriteLine($"Total committed offset across partitions before Close(): {totalCommitted}");

        Assert.Equal(ackedCount, totalCommitted);

        consumerA.Close();
        consumerB.Close();
    }

    private long GetTotalCommittedOffset()
    {
        using var rawConsumer = new ConsumerBuilder<Ignore, Ignore>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = _groupId
        }).Build();

        var partitions = new List<TopicPartition>
        {
            new(_topic, new Partition(0)),
            new(_topic, new Partition(1)),
            new(_topic, new Partition(2))
        };

        var committed = rawConsumer.Committed(partitions, TimeSpan.FromSeconds(10));

        return committed.Sum(tpo => tpo.Offset.Value == Offset.Unset.Value ? 0 : tpo.Offset.Value);
    }

    private KafkaMessageConsumer CreateConsumer()
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
                //Large enough that 15 acks across 3 partitions never trigger a batch commit
                commitBatchSize: 100,
                //Long enough that the sweeper cannot fire during the test
                sweepUncommittedOffsetsInterval: TimeSpan.FromMinutes(5),
                numOfPartitions: 3,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create
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

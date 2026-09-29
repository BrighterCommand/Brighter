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

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerSweepCommitsHighestOffsetAsync : IAsyncDisposable, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly string _groupId = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly KafkaMessageConsumer _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();
    private readonly FakeTimeProvider _fakeTimeProvider;

    public KafkaMessageConsumerSweepCommitsHighestOffsetAsync(ITestOutputHelper output)
    {
        _output = output;

        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test",
                BootStrapServers = ["localhost:9092"]
            },
            [
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    //These timeouts support running on a container using the same host as the tests,
                    //your production values ought to be lower
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
            ], loggerFactory: NullLoggerFactory.Instance).CreateAsync().Result;

        //Fake time lets us fire the sweeper deterministically, instead of waiting on a real clock
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        var subscription = new KafkaSubscription<MyCommand>(
            channelName: new ChannelName(_queueName),
            routingKey: new RoutingKey(_topic),
            groupId: _groupId,
            //Evident Data: a batch size larger than the number of acks means the batch path never
            //fires, so only the sweeper commits - this is the exact shape reported in #4281
            commitBatchSize: 10,
            sweepUncommittedOffsetsInterval: TimeSpan.FromSeconds(30),
            messagePumpType: MessagePumpType.Proactor,
            numOfPartitions: 1,
            replicationFactor: 1,
            makeChannels: OnMissingChannel.Create) { TimeProvider = _fakeTimeProvider };

        _consumer = (KafkaMessageConsumer)new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = ["localhost:9092"]
                }, loggerFactory: NullLoggerFactory.Instance)
            .CreateAsync(subscription);
    }

    [Fact]
    public async Task When_multiple_acks_on_one_partition_sweep_commits_highest_offset_async()
    {
        //Arrange
        //allow time for topic to propagate
        await Task.Delay(1000);

        var routingKey = new RoutingKey(_topic);
        var producerAsync = _producerRegistry.LookupAsyncBy(routingKey);

        //Evident Data: three messages on a single partition, all acked before any flush - this
        //is what leaves the offset bag holding more than one entry for the same TopicPartition
        var sentMessages = new string[3];
        for (int i = 0; i < 3; i++)
        {
            var msgId = Guid.NewGuid().ToString();

            await producerAsync.SendAsync(new Message(
                new MessageHeader(msgId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
                new MessageBody($"test content [{_queueName}]")));
            sentMessages[i] = msgId;
        }

        //We should not need to flush, as the async does not queue work - but in case this changes
        ((KafkaMessageProducer)producerAsync).Flush();

        //allow messages to propagate on the broker
        await Task.Delay(3000);

        var consumedMessages = new List<Message>();
        for (int j = 0; j < 3; j++)
        {
            consumedMessages.Add(await ReadMessageAsync());
        }

        //all three acknowledged, none flushed by a batch commit
        Assert.Equal(3, consumedMessages.Count);
        Assert.Equal(3, _consumer.StoredOffsets());

        //Act - advance time beyond the sweeper interval so the sweep commits everything it holds
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(31));

        //poll for the sweeper to run and commit offsets
        int sweepRetries = 0;
        while (_consumer.StoredOffsets() > 0 && sweepRetries < 20)
        {
            await Task.Delay(500);
            sweepRetries++;
        }

        Assert.Equal(0, _consumer.StoredOffsets());

        //Assert - the committed offset for the partition must be the highest offset acknowledged
        //(3), not an arbitrary lower one from the duplicate entries the sweep handed to Commit
        var committedOffset = GetCommittedOffset();
        Assert.Equal(3, committedOffset.Value);

        _consumer.Close();
    }

    private Offset GetCommittedOffset()
    {
        using var rawConsumer = new ConsumerBuilder<Ignore, Ignore>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = _groupId
        }).Build();

        var committed = rawConsumer.Committed(
            new List<TopicPartition> { new(_topic, new Partition(0)) },
            TimeSpan.FromSeconds(10));

        return committed[0].Offset;
    }

    private async Task<Message> ReadMessageAsync()
    {
        Message[] messages = new[] { new Message() };
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                await Task.Delay(500); //Let topic propagate in the broker
                messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    await _consumer.AcknowledgeAsync(messages[0]);
                    return messages[0];
                }

                //wait before retry
                await Task.Delay(1000);

            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                await Task.Delay(1000);
            }
        } while (maxTries <= 10);

        return messages[0];
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
        _consumer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _producerRegistry.Dispose();
        await _consumer.DisposeAsync();
    }
}

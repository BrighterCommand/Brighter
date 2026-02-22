#region Licence
/* The MIT License (MIT)
Copyright © 2025 Rafael Andrade

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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class WhenSweeperTimeoutReachedShouldCommitUncommittedOffsets : IAsyncDisposable, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly KafkaMessageConsumer _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();
    private readonly FakeTimeProvider _fakeTimeProvider;

    public WhenSweeperTimeoutReachedShouldCommitUncommittedOffsets(ITestOutputHelper output)
    {
        var groupId = Uuid.New().ToString("N");
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
            ]).Create();

        // Create a fake time provider to control time in the test
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        var subscription = new KafkaSubscription<MyCommand>(
            channelName: new ChannelName(_queueName),
            routingKey: new RoutingKey(_topic),
            groupId: groupId,
            commitBatchSize: 20,  //Large commit batch size to ensure sweeper is triggered
            sweepUncommittedOffsetsInterval: TimeSpan.FromSeconds(30),
            messagePumpType: MessagePumpType.Proactor,
            numOfPartitions: 1, 
            replicationFactor: 1, 
            makeChannels: OnMissingChannel.Create) { TimeProvider = _fakeTimeProvider };

        _consumer = (KafkaMessageConsumer) new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = ["localhost:9092"]
                })
            .Create(subscription);
    }

    [Fact]
    public void When_sweeper_timeout_reached_should_commit_uncommitted_offsets()
    {
        //Arrange
        //allow time for topic to propagate
        Task.Delay(1000).GetAwaiter().GetResult();
        
        var routingKey = new RoutingKey(_topic);
        var producerAsync = _producerRegistry.LookupSyncBy(routingKey);
            
        //send 5 messages to Kafka (less than the batch size of 20)
        var sentMessages = new string[5];
        for (int i = 0; i < 5; i++)
        {
            var msgId = Guid.NewGuid().ToString();

            producerAsync.Send(new Message(
                new MessageHeader(msgId, routingKey, MessageType.MT_COMMAND) {PartitionKey = _partitionKey},
                new MessageBody($"test content [{_queueName}]")));
            sentMessages[i] = msgId;
        }
        
        //We should not need to flush, as the async does not queue work - but in case this changes
        ((KafkaMessageProducer)producerAsync).Flush();

        //allow messages to propagate on the broker
        Task.Delay(3000).GetAwaiter().GetResult();

        var consumedMessages = new List<Message>();
        for (int j = 0; j < 5; j++)
        {
            consumedMessages.Add(ReadMessage());
        }

        //Assert - messages consumed and acknowledged but not yet committed
        Assert.Equal(5, consumedMessages.Count);
        Assert.Equal(5, _consumer.StoredOffsets());

        //Act - Advance time beyond the sweeper interval (30 seconds)
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(31));

        //Allow the timer callback to execute
        Task.Delay(2000).GetAwaiter().GetResult();

        //Assert - Sweeper should have committed the offsets
        Assert.Equal(0, _consumer.StoredOffsets());

        _consumer.Close();
    }

    private Message ReadMessage()
    {
        Message[] messages = [new Message()];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                Task.Delay(500).GetAwaiter().GetResult(); //Let topic propagate in the broker
                messages = _consumer.Receive(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    _consumer.Acknowledge(messages[0]);
                    return messages[0];
                }
                
                //wait before retry
                Task.Delay(1000).GetAwaiter().GetResult();
            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                Task.Delay(1000).GetAwaiter().GetResult();
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

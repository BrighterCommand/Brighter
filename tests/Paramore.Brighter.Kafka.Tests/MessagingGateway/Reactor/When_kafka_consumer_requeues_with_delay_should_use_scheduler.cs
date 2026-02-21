#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading.Tasks;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

/// <summary>
/// When the Kafka consumer requeues a message with a non-zero delay, the lazily-created producer
/// should delegate to the scheduler. This test verifies that the scheduler passed to the consumer
/// constructor is wired through to the producer's Scheduler property.
/// </summary>
[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaConsumerRequeueSchedulerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly KafkaMessageConsumer _consumer;
    private readonly SpySchedulerSync _scheduler;
    private readonly Message _message;

    public KafkaConsumerRequeueSchedulerTests(ITestOutputHelper output)
    {
        string groupId = Guid.NewGuid().ToString();
        _output = output;

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(_topic), MessageType.MT_COMMAND),
            new MessageBody("test content for scheduler"));

        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Scheduler Test",
                BootStrapServers = new[] { "localhost:9092" }
            },
            [
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
            ]).Create();

        _scheduler = new SpySchedulerSync();

        // Create consumer directly WITH scheduler (not via factory)
        _consumer = new KafkaMessageConsumer(
            configuration: new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Consumer Scheduler Test",
                BootStrapServers = new[] { "localhost:9092" }
            },
            routingKey: new RoutingKey(_topic),
            groupId: groupId,
            numPartitions: 1,
            replicationFactor: 1,
            makeChannels: OnMissingChannel.Create,
            scheduler: _scheduler);
    }

    [Fact]
    public void When_requeuing_with_delay_should_use_scheduler()
    {
        // Arrange - send a message and receive it
        var producer = (IAmAMessageProducerSync)_producerRegistry.LookupBy(new RoutingKey(_topic));
        producer.Send(_message);
        ((KafkaMessageProducer)producer).Flush();

        var received = GetMessage();
        Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

        // Act - requeue with non-zero delay (should use scheduler via producer)
        _consumer.Requeue(received, TimeSpan.FromSeconds(5));

        // Assert - scheduler should have been called (proves producer has scheduler configured)
        Assert.True(_scheduler.ScheduleCalled,
            "Scheduler.Schedule should have been called via the lazily created producer");
        Assert.Equal(TimeSpan.FromSeconds(5), _scheduler.ScheduledDelay);
    }

    private Message GetMessage()
    {
        Message[] messages = [];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                messages = _consumer.Receive(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    _consumer.Acknowledge(messages[0]);
                    break;
                }
            }
            catch (ChannelFailureException cfx)
            {
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                Task.Delay(1000).GetAwaiter().GetResult();
            }
        } while (maxTries <= 10);

        if (messages[0].Header.MessageType == MessageType.MT_NONE)
            throw new Exception($"Failed to read from topic:{_topic} after {maxTries} attempts");

        return messages[0];
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
        _consumer?.Dispose();
    }

    /// <summary>
    /// A spy sync scheduler that records calls to Schedule for verification.
    /// </summary>
    private sealed class SpySchedulerSync : IAmAMessageSchedulerSync
    {
        public bool ScheduleCalled { get; private set; }
        public Message? ScheduledMessage { get; private set; }
        public TimeSpan? ScheduledDelay { get; private set; }

        public string Schedule(Message message, DateTimeOffset at)
        {
            ScheduleCalled = true;
            ScheduledMessage = message;
            return Guid.NewGuid().ToString();
        }

        public string Schedule(Message message, TimeSpan delay)
        {
            ScheduleCalled = true;
            ScheduledMessage = message;
            ScheduledDelay = delay;
            return Guid.NewGuid().ToString();
        }

        public bool ReScheduler(string schedulerId, DateTimeOffset at) => true;

        public bool ReScheduler(string schedulerId, TimeSpan delay) => true;

        public void Cancel(string id) { }
    }
}

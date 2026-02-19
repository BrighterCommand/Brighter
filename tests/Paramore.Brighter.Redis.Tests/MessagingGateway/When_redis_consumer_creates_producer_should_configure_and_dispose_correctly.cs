#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

/// <summary>
/// Verifies that the Redis consumer correctly wires the scheduler through to the
/// lazily-created requeue producer, and that disposal works in all cases.
/// Uses a non-zero delay to exercise the full scheduler path (lesson from Kafka/MQTT).
/// </summary>
[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class When_redis_consumer_creates_producer_should_configure_and_dispose_correctly
{
    [Fact]
    public void When_requeuing_with_delay_should_wire_scheduler_to_producer()
    {
        // Arrange
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();
        var topicName = $"Producer-Config-Tests-{Guid.NewGuid()}";
        var topic = new RoutingKey(topicName);
        var queueName = new ChannelName($"Producer-Config-Queue-{Guid.NewGuid()}");

        var scheduler = new SpySchedulerSync();
        var consumer = new RedisMessageConsumer(configuration, queueName, topic, scheduler);

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test scheduler wiring"));

        // Act - requeue with non-zero delay to exercise the scheduler path
        consumer.Requeue(message, TimeSpan.FromSeconds(5));

        // Assert - scheduler was called, proving it was wired through to the producer
        Assert.True(scheduler.ScheduleCalled);
        Assert.Equal(message.Body.Value, scheduler.ScheduledMessage?.Body.Value);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.ScheduledDelay);

        // Cleanup
        consumer.Dispose();
    }

    [Fact]
    public void When_disposing_after_requeue_should_not_throw()
    {
        // Arrange
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();
        var topicName = $"Producer-Dispose-Tests-{Guid.NewGuid()}";
        var topic = new RoutingKey(topicName);
        var queueName = new ChannelName($"Producer-Dispose-Queue-{Guid.NewGuid()}");

        var scheduler = new SpySchedulerSync();
        var consumer = new RedisMessageConsumer(configuration, queueName, topic, scheduler);

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test dispose after requeue"));

        consumer.Requeue(message, TimeSpan.FromSeconds(5));

        // Act & Assert - dispose after producer was created should not throw
        var exception = Record.Exception(() => consumer.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void When_disposing_without_requeue_should_not_throw()
    {
        // Arrange - create consumer but never requeue (producer never created)
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();
        var topicName = $"Producer-NoRequeue-Tests-{Guid.NewGuid()}";
        var topic = new RoutingKey(topicName);
        var queueName = new ChannelName($"Producer-NoRequeue-Queue-{Guid.NewGuid()}");

        var scheduler = new SpySchedulerSync();
        var consumer = new RedisMessageConsumer(configuration, queueName, topic, scheduler);

        // Act & Assert - dispose without producer creation should not throw
        var exception = Record.Exception(() => consumer.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public async Task When_disposing_async_after_requeue_should_not_throw()
    {
        // Arrange
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();
        var topicName = $"Producer-AsyncDispose-Tests-{Guid.NewGuid()}";
        var topic = new RoutingKey(topicName);
        var queueName = new ChannelName($"Producer-AsyncDispose-Queue-{Guid.NewGuid()}");

        var scheduler = new SpySchedulerSync();
        var consumer = new RedisMessageConsumer(configuration, queueName, topic, scheduler);

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test async dispose after requeue"));

        consumer.Requeue(message, TimeSpan.FromSeconds(5));

        // Act & Assert - async dispose after producer was created should not throw
        var exception = await Record.ExceptionAsync(async () => await consumer.DisposeAsync());
        Assert.Null(exception);
    }

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

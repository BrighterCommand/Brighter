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
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

/// <summary>
/// When the Redis consumer requeues a message with a non-zero delay, it should delegate
/// to a lazily-created producer's SendWithDelay rather than adding it back to the list immediately.
/// This ensures the delay is respected via the scheduler instead of being ignored.
/// </summary>
[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class When_redis_consumer_requeues_with_delay_should_use_producer : IDisposable
{
    private readonly RedisMessageConsumer _consumer;
    private readonly SpySchedulerSync _scheduler;
    private readonly Message _message;

    public When_redis_consumer_requeues_with_delay_should_use_producer()
    {
        // Arrange
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();
        var topicName = $"Requeue-Delay-Tests-{Guid.NewGuid()}";
        var topic = new RoutingKey(topicName);
        var queueName = new ChannelName($"Requeue-Delay-Queue-{Guid.NewGuid()}");

        _scheduler = new SpySchedulerSync();
        _consumer = new RedisMessageConsumer(configuration, queueName, topic, _scheduler);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content for delayed requeue"));
    }

    [Fact]
    public void When_requeuing_with_delay_should_use_producer()
    {
        // Act - requeue with non-zero delay
        var result = _consumer.Requeue(_message, TimeSpan.FromSeconds(5));

        // Assert - should return true
        Assert.True(result);

        // Assert - scheduler should have been called via the producer path
        Assert.True(_scheduler.ScheduleCalled);
        Assert.Equal(_message.Body.Value, _scheduler.ScheduledMessage?.Body.Value);
        Assert.Equal(TimeSpan.FromSeconds(5), _scheduler.ScheduledDelay);
    }

    public void Dispose()
    {
        _consumer.Dispose();
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

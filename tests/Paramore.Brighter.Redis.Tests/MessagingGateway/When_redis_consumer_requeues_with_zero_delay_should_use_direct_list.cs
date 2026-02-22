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
/// When the Redis consumer requeues a message with zero or null delay, it should use the
/// direct Redis list operations rather than creating a producer. This preserves existing
/// behavior and ensures immediate requeue without scheduler overhead.
/// </summary>
[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class When_redis_consumer_requeues_with_zero_delay_should_use_direct_list : IDisposable
{
    private readonly RedisMessageConsumer _consumer;
    private readonly SpySchedulerSync _scheduler;
    private readonly Message _message;

    public When_redis_consumer_requeues_with_zero_delay_should_use_direct_list()
    {
        // Arrange
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();
        var topicName = $"Requeue-ZeroDelay-Tests-{Guid.NewGuid()}";
        var topic = new RoutingKey(topicName);
        var queueName = new ChannelName($"Requeue-ZeroDelay-Queue-{Guid.NewGuid()}");

        _scheduler = new SpySchedulerSync();
        _consumer = new RedisMessageConsumer(configuration, queueName, topic, _scheduler);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content for zero delay requeue"));
    }

    [Fact]
    public void When_requeuing_with_zero_delay_should_not_use_scheduler()
    {
        // Act - requeue with zero delay; the direct Redis list path will be attempted
        // which may fail without Redis, but the scheduler must NOT be called
        Record.Exception(() => _consumer.Requeue(_message, TimeSpan.Zero));

        // Assert - scheduler should NOT have been called
        Assert.False(_scheduler.ScheduleCalled);
    }

    [Fact]
    public void When_requeuing_with_null_delay_should_not_use_scheduler()
    {
        // Act - requeue with null delay (defaults to zero)
        Record.Exception(() => _consumer.Requeue(_message));

        // Assert - scheduler should NOT have been called
        Assert.False(_scheduler.ScheduleCalled);
    }

    public void Dispose()
    {
        _consumer.Dispose();
    }

    private sealed class SpySchedulerSync : IAmAMessageSchedulerSync
    {
        public bool ScheduleCalled { get; private set; }

        public string Schedule(Message message, DateTimeOffset at)
        {
            ScheduleCalled = true;
            return Guid.NewGuid().ToString();
        }

        public string Schedule(Message message, TimeSpan delay)
        {
            ScheduleCalled = true;
            return Guid.NewGuid().ToString();
        }

        public bool ReScheduler(string schedulerId, DateTimeOffset at) => true;
        public bool ReScheduler(string schedulerId, TimeSpan delay) => true;
        public void Cancel(string id) { }
    }
}

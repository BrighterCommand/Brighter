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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

/// <summary>
/// When the Redis consumer requeues a message asynchronously with a non-zero delay,
/// it should delegate to the lazily-created producer's SendWithDelayAsync rather than
/// adding it back to the list immediately.
/// </summary>
[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class When_redis_consumer_requeues_async_with_delay_should_use_producer : IAsyncDisposable
{
    private readonly RedisMessageConsumer _consumer;
    private readonly SpySchedulerAsync _scheduler;
    private readonly Message _message;

    public When_redis_consumer_requeues_async_with_delay_should_use_producer()
    {
        // Arrange
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();
        var topicName = $"Requeue-Async-Delay-Tests-{Guid.NewGuid()}";
        var topic = new RoutingKey(topicName);
        var queueName = new ChannelName($"Requeue-Async-Delay-Queue-{Guid.NewGuid()}");

        _scheduler = new SpySchedulerAsync();
        _consumer = new RedisMessageConsumer(configuration, queueName, topic, _scheduler);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content for async delayed requeue"));
    }

    [Fact]
    public async Task When_requeuing_async_with_delay_should_use_producer()
    {
        // Act - requeue with non-zero delay
        var result = await _consumer.RequeueAsync(_message, TimeSpan.FromSeconds(5));

        // Assert - should return true
        Assert.True(result);

        // Assert - async scheduler should have been called via the producer path
        Assert.True(_scheduler.ScheduleAsyncCalled);
        Assert.Equal(_message.Body.Value, _scheduler.ScheduledMessage?.Body.Value);
        Assert.Equal(TimeSpan.FromSeconds(5), _scheduler.ScheduledDelay);
    }

    public ValueTask DisposeAsync()
    {
        return _consumer.DisposeAsync();
    }

    private sealed class SpySchedulerAsync : IAmAMessageSchedulerAsync
    {
        public bool ScheduleAsyncCalled { get; private set; }
        public Message? ScheduledMessage { get; private set; }
        public TimeSpan? ScheduledDelay { get; private set; }

        public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            ScheduleAsyncCalled = true;
            ScheduledMessage = message;
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            ScheduleAsyncCalled = true;
            ScheduledMessage = message;
            ScheduledDelay = delay;
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task CancelAsync(string id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

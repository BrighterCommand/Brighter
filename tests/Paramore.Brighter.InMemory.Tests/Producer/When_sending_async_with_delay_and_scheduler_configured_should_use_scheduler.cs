#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Producer;

/// <summary>
/// Tests that InMemoryMessageProducer.SendWithDelayAsync uses the configured scheduler
/// when a delay greater than zero is specified and an async scheduler is configured.
/// </summary>
public class When_sending_async_with_delay_and_scheduler_configured_should_use_scheduler
{
    private readonly InternalBus _bus;
    private readonly InMemoryMessageProducer _producer;
    private readonly SpySchedulerAsync _scheduler;
    private readonly Message _message;
    private readonly TimeSpan _delay;

    public When_sending_async_with_delay_and_scheduler_configured_should_use_scheduler()
    {
        // Arrange
        _bus = new InternalBus();
        var timeProvider = new FakeTimeProvider();
        _producer = new InMemoryMessageProducer(_bus, timeProvider);
        _scheduler = new SpySchedulerAsync();
        _producer.Scheduler = _scheduler;

        var routingKey = new RoutingKey("test.topic");
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));
        _delay = TimeSpan.FromSeconds(30);
    }

    [Fact]
    public async Task Should_call_scheduler_schedule_async_method()
    {
        // Act
        await _producer.SendWithDelayAsync(_message, _delay);

        // Assert
        Assert.True(_scheduler.ScheduleAsyncCalled, "Scheduler.ScheduleAsync should have been called");
        Assert.Equal(_message, _scheduler.ScheduledMessage);
        Assert.Equal(_delay, _scheduler.ScheduledDelay);
    }

    [Fact]
    public async Task Should_not_send_message_immediately_to_bus()
    {
        // Act
        await _producer.SendWithDelayAsync(_message, _delay);

        // Assert
        var messagesOnBus = _bus.Stream(new RoutingKey("test.topic"));
        Assert.Empty(messagesOnBus);
    }

    /// <summary>
    /// A spy async scheduler that records calls to ScheduleAsync for verification.
    /// </summary>
    private sealed class SpySchedulerAsync : IAmAMessageSchedulerAsync
    {
        public bool ScheduleAsyncCalled { get; private set; }
        public Message? ScheduledMessage { get; private set; }
        public TimeSpan? ScheduledDelay { get; private set; }
        public DateTimeOffset? ScheduledAt { get; private set; }

        public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            ScheduleAsyncCalled = true;
            ScheduledMessage = message;
            ScheduledAt = at;
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

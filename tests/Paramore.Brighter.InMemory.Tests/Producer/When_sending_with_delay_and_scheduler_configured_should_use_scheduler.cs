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
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Producer;

/// <summary>
/// Tests that InMemoryMessageProducer.SendWithDelay uses the configured scheduler
/// when a delay greater than zero is specified and a scheduler is configured.
/// </summary>
public class When_sending_with_delay_and_scheduler_configured_should_use_scheduler
{
    private readonly InternalBus _bus;
    private readonly InMemoryMessageProducer _producer;
    private readonly SpyScheduler _scheduler;
    private readonly Message _message;
    private readonly TimeSpan _delay;

    public When_sending_with_delay_and_scheduler_configured_should_use_scheduler()
    {
        // Arrange
        _bus = new InternalBus();
        var timeProvider = new FakeTimeProvider();
        _producer = new InMemoryMessageProducer(_bus, timeProvider);
        _scheduler = new SpyScheduler();
        _producer.Scheduler = _scheduler;

        var routingKey = new RoutingKey("test.topic");
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));
        _delay = TimeSpan.FromSeconds(30);
    }

    [Fact]
    public void Should_call_scheduler_schedule_method()
    {
        // Act
        _producer.SendWithDelay(_message, _delay);

        // Assert
        Assert.True(_scheduler.ScheduleCalled, "Scheduler.Schedule should have been called");
        Assert.Equal(_message, _scheduler.ScheduledMessage);
        Assert.Equal(_delay, _scheduler.ScheduledDelay);
    }

    [Fact]
    public void Should_not_send_message_immediately_to_bus()
    {
        // Act
        _producer.SendWithDelay(_message, _delay);

        // Assert
        var messagesOnBus = _bus.Stream(new RoutingKey("test.topic"));
        Assert.Empty(messagesOnBus);
    }

    /// <summary>
    /// A spy scheduler that records calls to Schedule for verification.
    /// </summary>
    private sealed class SpyScheduler : IAmAMessageSchedulerSync
    {
        public bool ScheduleCalled { get; private set; }
        public Message? ScheduledMessage { get; private set; }
        public TimeSpan? ScheduledDelay { get; private set; }
        public DateTimeOffset? ScheduledAt { get; private set; }

        public string Schedule(Message message, DateTimeOffset at)
        {
            ScheduleCalled = true;
            ScheduledMessage = message;
            ScheduledAt = at;
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

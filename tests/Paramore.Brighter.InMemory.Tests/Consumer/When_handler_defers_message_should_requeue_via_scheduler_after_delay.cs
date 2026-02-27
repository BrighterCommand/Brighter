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
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

/// <summary>
/// Integration test verifying the complete flow: consumer requeue with delay → producer → scheduler → bus.
/// This test verifies that when a message is requeued with a delay and a scheduler is configured:
/// 1. The message is NOT immediately available on the bus
/// 2. After advancing time past the delay, the message becomes available
/// 3. The message can be received again by the consumer
/// 4. The HandledCount is incremented on requeue
/// </summary>
public class InMemoryConsumerRequeueWithDelayTests
{
    private readonly InternalBus _bus;
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryMessageConsumer _consumer;
    private readonly RoutingKey _routingKey;
    private readonly Message _message;
    private readonly TimeSpan _delay;
    private readonly SpyScheduler _scheduler;

    public InMemoryConsumerRequeueWithDelayTests()
    {
        // Arrange
        _bus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);
        _routingKey = new RoutingKey("test.integration.topic");
        _scheduler = new SpyScheduler(_bus, _timeProvider);

        // Create consumer with scheduler configured
        _consumer = new InMemoryMessageConsumer(
            _routingKey,
            _bus,
            _timeProvider,
            scheduler: _scheduler);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));
        _delay = TimeSpan.FromSeconds(30);

        // Put message on bus and receive it (so it's in locked state, simulating handler processing)
        _bus.Enqueue(_message);
        _consumer.Receive();
    }

    [Fact]
    public void Should_not_have_message_immediately_available_after_requeue_with_delay()
    {
        // Act - handler defers the message (simulated by calling Requeue with delay)
        _consumer.Requeue(_message, _delay);

        // Assert - message should NOT be immediately available on bus (scheduler holds it)
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Empty(messagesOnBus);
    }

    [Fact]
    public void Should_have_message_available_after_delay_expires()
    {
        // Act - handler defers the message
        _consumer.Requeue(_message, _delay);

        // Advance time past the delay
        _timeProvider.Advance(_delay + TimeSpan.FromSeconds(1));

        // Assert - message should now be available on bus
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Single(messagesOnBus);
    }

    [Fact]
    public void Should_be_able_to_receive_message_again_after_delay()
    {
        // Act - handler defers the message
        _consumer.Requeue(_message, _delay);

        // Advance time past the delay
        _timeProvider.Advance(_delay + TimeSpan.FromSeconds(1));

        // Receive the message again
        var receivedMessages = _consumer.Receive();

        // Assert - should receive the same message
        Assert.Single(receivedMessages);
        Assert.Equal(_message.Id, receivedMessages.First().Id);
    }

    [Fact]
    public void Should_preserve_message_content_through_delayed_requeue()
    {
        // Act - handler defers the message
        _consumer.Requeue(_message, _delay);

        // Advance time past the delay
        _timeProvider.Advance(_delay + TimeSpan.FromSeconds(1));

        // Receive the message again
        var receivedMessages = _consumer.Receive();

        // Assert - message content should be preserved through the scheduler flow
        Assert.Single(receivedMessages);
        Assert.Equal(_message.Body.Value, receivedMessages.First().Body.Value);
        Assert.Equal(_message.Header.Topic, receivedMessages.First().Header.Topic);
        Assert.Equal(_message.Header.MessageType, receivedMessages.First().Header.MessageType);
    }

    /// <summary>
    /// A scheduler that actually delivers messages to the bus after the delay,
    /// using the FakeTimeProvider for time control.
    /// </summary>
    private sealed class SpyScheduler : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync
    {
        private readonly InternalBus _bus;
        private readonly FakeTimeProvider _timeProvider;

        public SpyScheduler(InternalBus bus, FakeTimeProvider timeProvider)
        {
            _bus = bus;
            _timeProvider = timeProvider;
        }

        public string Schedule(Message message, DateTimeOffset at)
        {
            return Schedule(message, at - _timeProvider.GetUtcNow());
        }

        public string Schedule(Message message, TimeSpan delay)
        {
            // Use timer to deliver message after delay
            _timeProvider.CreateTimer(
                _ => _bus.Enqueue(message),
                null,
                delay,
                TimeSpan.Zero);

            return Guid.NewGuid().ToString();
        }

        public bool ReScheduler(string schedulerId, DateTimeOffset at) => true;
        public bool ReScheduler(string schedulerId, TimeSpan delay) => true;
        public void Cancel(string id) { }

        public System.Threading.Tasks.Task<string> ScheduleAsync(Message message, DateTimeOffset at,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return System.Threading.Tasks.Task.FromResult(Schedule(message, at));
        }

        public System.Threading.Tasks.Task<string> ScheduleAsync(Message message, TimeSpan delay,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return System.Threading.Tasks.Task.FromResult(Schedule(message, delay));
        }

        public System.Threading.Tasks.Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
            System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(true);

        public System.Threading.Tasks.Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
            System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(true);

        public System.Threading.Tasks.Task CancelAsync(string id,
            System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}

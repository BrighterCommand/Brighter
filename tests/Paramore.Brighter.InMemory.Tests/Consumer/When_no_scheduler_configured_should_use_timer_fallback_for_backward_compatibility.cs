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
/// Backward compatibility test: verifies that when NO scheduler is configured,
/// the consumer's delayed requeue still works using the timer fallback mechanism.
/// This ensures existing code that doesn't use schedulers continues to function.
/// </summary>
public class When_no_scheduler_configured_should_use_timer_fallback_for_backward_compatibility
{
    private readonly InternalBus _bus;
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryMessageConsumer _consumer;
    private readonly RoutingKey _routingKey;
    private readonly Message _message;
    private readonly TimeSpan _delay;

    public When_no_scheduler_configured_should_use_timer_fallback_for_backward_compatibility()
    {
        // Arrange - NO scheduler configured (backward compatibility scenario)
        _bus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);
        _routingKey = new RoutingKey("test.backward.compat.topic");

        // Create consumer WITHOUT scheduler - testing backward compatibility
        _consumer = new InMemoryMessageConsumer(
            _routingKey,
            _bus,
            _timeProvider);
        // Note: scheduler parameter is NOT passed - this is the backward compatibility test

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("test content for backward compatibility"));
        _delay = TimeSpan.FromSeconds(30);

        // Put message on bus and receive it (so it's in locked state)
        _bus.Enqueue(_message);
        _consumer.Receive();
    }

    [Fact]
    public void Should_not_have_message_immediately_available_after_requeue_with_delay()
    {
        // Act - requeue with delay (no scheduler configured, should use timer fallback)
        _consumer.Requeue(_message, _delay);

        // Assert - message should NOT be immediately available (timer holds it)
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Empty(messagesOnBus);
    }

    [Fact]
    public void Should_have_message_available_after_delay_expires_via_timer()
    {
        // Act - requeue with delay
        _consumer.Requeue(_message, _delay);

        // Advance time past the delay (timer should fire)
        _timeProvider.Advance(_delay + TimeSpan.FromSeconds(1));

        // Assert - message should now be available on bus (delivered by timer)
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Single(messagesOnBus);
    }

    [Fact]
    public void Should_be_able_to_receive_message_again_after_timer_delay()
    {
        // Act - requeue with delay
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
    public void Should_not_have_message_before_timer_delay_expires()
    {
        // Act - requeue with delay
        _consumer.Requeue(_message, _delay);

        // Advance time but NOT past the delay
        _timeProvider.Advance(_delay - TimeSpan.FromSeconds(5));

        // Assert - message should NOT be available yet
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Empty(messagesOnBus);
    }

    [Fact]
    public void Should_preserve_message_content_through_timer_fallback()
    {
        // Act - requeue with delay
        _consumer.Requeue(_message, _delay);

        // Advance time past the delay
        _timeProvider.Advance(_delay + TimeSpan.FromSeconds(1));

        // Receive the message again
        var receivedMessages = _consumer.Receive();

        // Assert - message content should be preserved
        Assert.Single(receivedMessages);
        Assert.Equal(_message.Body.Value, receivedMessages.First().Body.Value);
        Assert.Equal(_message.Header.Topic, receivedMessages.First().Header.Topic);
        Assert.Equal(_message.Header.MessageType, receivedMessages.First().Header.MessageType);
    }
}

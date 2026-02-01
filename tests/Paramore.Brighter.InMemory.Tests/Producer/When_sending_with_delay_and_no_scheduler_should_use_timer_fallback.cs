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

namespace Paramore.Brighter.InMemory.Tests.Producer;

/// <summary>
/// Tests that InMemoryMessageProducer.SendWithDelay falls back to timer-based delivery
/// when no scheduler is configured, ensuring backward compatibility.
/// </summary>
public class When_sending_with_delay_and_no_scheduler_should_use_timer_fallback
{
    private readonly InternalBus _bus;
    private readonly InMemoryMessageProducer _producer;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RoutingKey _routingKey;
    private readonly Message _message;
    private readonly TimeSpan _delay;

    public When_sending_with_delay_and_no_scheduler_should_use_timer_fallback()
    {
        // Arrange - no scheduler configured
        _bus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        _producer = new InMemoryMessageProducer(_bus, _timeProvider);
        // Note: Scheduler is NOT set - testing fallback behavior

        _routingKey = new RoutingKey("test.topic");
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));
        _delay = TimeSpan.FromSeconds(30);
    }

    [Fact]
    public void Should_not_send_message_immediately()
    {
        // Act
        _producer.SendWithDelay(_message, _delay);

        // Assert - message should NOT be on the bus yet
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Empty(messagesOnBus);
    }

    [Fact]
    public void Should_send_message_after_delay_expires()
    {
        // Act
        _producer.SendWithDelay(_message, _delay);

        // Advance time past the delay
        _timeProvider.Advance(_delay + TimeSpan.FromSeconds(1));

        // Assert - message should now be on the bus
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Single(messagesOnBus);
        Assert.Equal(_message.Id, messagesOnBus.First().Id);
    }

    [Fact]
    public void Should_not_send_message_before_delay_expires()
    {
        // Act
        _producer.SendWithDelay(_message, _delay);

        // Advance time but not past the delay
        _timeProvider.Advance(_delay - TimeSpan.FromSeconds(1));

        // Assert - message should NOT be on the bus yet
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Empty(messagesOnBus);
    }
}

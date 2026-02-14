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

namespace Paramore.Brighter.InMemory.Tests.Consumer;

/// <summary>
/// Verifies that when NO scheduler is configured, the consumer throws a
/// <see cref="ConfigurationException"/> on delayed requeue, guiding users
/// to configure a scheduler via MessageSchedulerFactory.
/// </summary>
public class AsyncInMemoryConsumerMissingSchedulerTests
{
    private readonly InternalBus _bus;
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryMessageConsumer _consumer;
    private readonly RoutingKey _routingKey;
    private readonly Message _message;

    public AsyncInMemoryConsumerMissingSchedulerTests()
    {
        _bus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);
        _routingKey = new RoutingKey("test.no.scheduler.topic");

        // Create consumer WITHOUT scheduler
        _consumer = new InMemoryMessageConsumer(
            _routingKey,
            _bus,
            _timeProvider);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));

        // Put message on bus and receive it (so it's in locked state)
        _bus.Enqueue(_message);
        _consumer.Receive();
    }

    [Fact]
    public void Should_throw_configuration_exception_on_delayed_requeue()
    {
        // Act & Assert - requeue with delay should throw when no scheduler configured
        var exception = Assert.Throws<ConfigurationException>(
            () => _consumer.Requeue(_message, TimeSpan.FromSeconds(30)));

        Assert.Contains("no scheduler is configured", exception.Message);
    }

    [Fact]
    public void Should_succeed_when_requeue_has_no_delay()
    {
        // Act - requeue without delay should still work (no scheduler needed)
        var result = _consumer.Requeue(_message);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Should_succeed_when_requeue_has_zero_delay()
    {
        // Act - requeue with zero delay should still work (no scheduler needed)
        var result = _consumer.Requeue(_message, TimeSpan.Zero);

        // Assert
        Assert.True(result);
    }
}

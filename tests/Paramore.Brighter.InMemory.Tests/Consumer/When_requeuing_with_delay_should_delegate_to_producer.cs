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

namespace Paramore.Brighter.InMemory.Tests.Consumer;

/// <summary>
/// Tests that InMemoryMessageConsumer.Requeue with delay delegates to producer's SendWithDelay,
/// which enables the use of a configured scheduler for delayed requeue.
/// </summary>
public class InMemoryConsumerRequeueWithDelayProducerTests
{
    private readonly InternalBus _bus;
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryMessageConsumer _consumer;
    private readonly SpyScheduler _scheduler;
    private readonly RoutingKey _routingKey;
    private readonly Message _message;
    private readonly TimeSpan _delay;

    public InMemoryConsumerRequeueWithDelayProducerTests()
    {
        // Arrange
        _bus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        _routingKey = new RoutingKey("test.topic");
        _scheduler = new SpyScheduler();

        // Create consumer with scheduler configured
        _consumer = new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, scheduler: _scheduler);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));
        _delay = TimeSpan.FromSeconds(30);

        // Put message on bus and receive it (so it's in locked state)
        _bus.Enqueue(_message);
        _consumer.Receive();
    }

    [Test]
    public async Task Should_use_scheduler_when_requeuing_with_delay()
    {
        // Act
        await _consumer.RequeueAsync(_message, _delay);

        // Assert
        await Assert.That(_scheduler.ScheduleCalled).IsTrue();
    }

    [Test]
    public async Task Should_not_have_message_immediately_available_on_bus()
    {
        // Act
        await _consumer.RequeueAsync(_message, _delay);

        // Assert - message should not be immediately available (scheduler holds it)
        var messagesOnBus = _bus.Stream(_routingKey);
        await Assert.That(messagesOnBus).IsEmpty();
    }

    [Test]
    public async Task Should_remove_message_from_locked_messages()
    {
        // Act
        var result = await _consumer.RequeueAsync(_message, _delay);

        // Assert
        await Assert.That(result).IsTrue();
    }

}

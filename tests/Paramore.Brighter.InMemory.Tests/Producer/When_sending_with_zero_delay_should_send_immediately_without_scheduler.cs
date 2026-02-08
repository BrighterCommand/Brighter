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
/// Tests that InMemoryMessageProducer.SendWithDelay sends immediately when delay is zero,
/// bypassing the scheduler even when one is configured.
/// </summary>
public class When_sending_with_zero_delay_should_send_immediately_without_scheduler
{
    private readonly InternalBus _bus;
    private readonly InMemoryMessageProducer _producer;
    private readonly SpyScheduler _scheduler;
    private readonly RoutingKey _routingKey;
    private readonly Message _message;

    public When_sending_with_zero_delay_should_send_immediately_without_scheduler()
    {
        // Arrange
        _bus = new InternalBus();
        _producer = new InMemoryMessageProducer(_bus);
        _scheduler = new SpyScheduler();
        _producer.Scheduler = _scheduler;

        _routingKey = new RoutingKey("test.topic");
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));
    }

    [Fact]
    public void Should_send_message_directly_to_bus_when_delay_is_zero()
    {
        // Act
        _producer.SendWithDelay(_message, TimeSpan.Zero);

        // Assert
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Single(messagesOnBus);
        Assert.Equal(_message.Id, messagesOnBus.First().Id);
    }

    [Fact]
    public void Should_send_message_directly_to_bus_when_delay_is_null()
    {
        // Act
        _producer.SendWithDelay(_message, null);

        // Assert
        var messagesOnBus = _bus.Stream(_routingKey);
        Assert.Single(messagesOnBus);
        Assert.Equal(_message.Id, messagesOnBus.First().Id);
    }

    [Fact]
    public void Should_not_invoke_scheduler_when_delay_is_zero()
    {
        // Act
        _producer.SendWithDelay(_message, TimeSpan.Zero);

        // Assert
        Assert.False(_scheduler.ScheduleCalled, "Scheduler should NOT have been called for zero delay");
    }

    /// <summary>
    /// A spy scheduler that records calls to Schedule for verification.
    /// </summary>
    private sealed class SpyScheduler : IAmAMessageSchedulerSync
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

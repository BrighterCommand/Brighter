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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

/// <summary>
/// Tests that InMemoryMessageConsumer properly disposes the lazily-created producer
/// when the consumer is disposed.
/// </summary>
public class AsyncInMemoryMessageConsumerDisposeTests
{
    [Test]
    public async Task Should_dispose_without_error_when_producer_was_never_created()
    {
        // Arrange
        var bus = new InternalBus();
        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("test.topic");
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider);

        // Act & Assert - should not throw
        await Assert.That(() => consumer.Dispose()).ThrowsNothing();
    }

    [Test]
    public async Task Should_dispose_producer_when_it_was_created_during_requeue()
    {
        // Arrange
        var bus = new InternalBus();
        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("test.topic");
        var scheduler = new SpyScheduler();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, scheduler: scheduler);

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));

        // Put message on bus, receive it, then requeue with delay (creates producer)
        bus.Enqueue(message);
        await consumer.ReceiveAsync();
        await consumer.RequeueAsync(message, TimeSpan.FromSeconds(30));

        // Act & Assert - should not throw
        await Assert.That(() => consumer.Dispose()).ThrowsNothing();
    }

    [Test]
    public async Task Should_dispose_async_without_error_when_producer_was_never_created()
    {
        // Arrange
        var bus = new InternalBus();
        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("test.topic");
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider);

        // Act & Assert - should not throw
        await Assert.That(async () => await consumer.DisposeAsync()).ThrowsNothing();
    }

    [Test]
    public async Task Should_dispose_async_producer_when_it_was_created_during_requeue()
    {
        // Arrange
        var bus = new InternalBus();
        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("test.topic");
        var scheduler = new SpyScheduler();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, scheduler: scheduler);

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));

        // Put message on bus, receive it, then requeue with delay (creates producer)
        bus.Enqueue(message);
        await consumer.ReceiveAsync();
        await consumer.RequeueAsync(message, TimeSpan.FromSeconds(30));

        // Act & Assert - should not throw
        await Assert.That(async () => await consumer.DisposeAsync()).ThrowsNothing();
    }

}

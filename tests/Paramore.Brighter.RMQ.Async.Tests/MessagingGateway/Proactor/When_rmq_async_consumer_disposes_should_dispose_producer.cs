#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

/// <summary>
/// When the RMQ async consumer is disposed, it should also dispose any lazily created producer.
/// If no producer was created, disposal should still succeed without error.
/// </summary>
[Trait("Category", "RMQ")]
public class When_rmq_async_consumer_disposes_should_dispose_producer
{
    private readonly RmqMessagingGatewayConnection _rmqConnection;

    public When_rmq_async_consumer_disposes_should_dispose_producer()
    {
        _rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
    }

    [Fact]
    public void When_disposing_without_producer_created_should_not_throw()
    {
        // Arrange - consumer that never requeued with delay (no producer created)
        var consumer = new RmqMessageConsumer(
            _rmqConnection,
            new ChannelName(Guid.NewGuid().ToString()),
            new RoutingKey(Guid.NewGuid().ToString()),
            isDurable: false);

        // Act & Assert - should not throw
        var exception = Record.Exception(() => consumer.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public async Task When_disposing_after_delayed_requeue_should_not_throw()
    {
        // Arrange
        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var scheduler = new SpySchedulerAsync();

        var consumer = new RmqMessageConsumer(
            _rmqConnection,
            queueName,
            topic,
            isDurable: false,
            scheduler: scheduler);

        var sendProducer = new RmqMessageProducer(_rmqConnection);

        new QueueFactory(_rmqConnection, queueName, new RoutingKeys(topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("disposal test"));

        // Send, receive, and requeue with delay to trigger producer creation
        await sendProducer.SendAsync(message);
        var received = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
        Assert.NotEmpty(received);
        await consumer.RequeueAsync(received[0], TimeSpan.FromSeconds(5));

        // Act & Assert - disposing consumer should also dispose the lazily created producer
        var exception = Record.Exception(() => consumer.Dispose());
        Assert.Null(exception);

        sendProducer.Dispose();
    }

    [Fact]
    public async Task When_disposing_async_after_delayed_requeue_should_not_throw()
    {
        // Arrange
        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var scheduler = new SpySchedulerAsync();

        var consumer = new RmqMessageConsumer(
            _rmqConnection,
            queueName,
            topic,
            isDurable: false,
            scheduler: scheduler);

        var sendProducer = new RmqMessageProducer(_rmqConnection);

        new QueueFactory(_rmqConnection, queueName, new RoutingKeys(topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("async disposal test"));

        // Send, receive, and requeue with delay to trigger producer creation
        await sendProducer.SendAsync(message);
        var received = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
        Assert.NotEmpty(received);
        await consumer.RequeueAsync(received[0], TimeSpan.FromSeconds(5));

        // Act & Assert - async disposing consumer should also dispose the lazily created producer
        var exception = await Record.ExceptionAsync(async () => await consumer.DisposeAsync());
        Assert.Null(exception);

        await sendProducer.DisposeAsync();
    }

    /// <summary>
    /// A spy async scheduler that records calls for verification.
    /// </summary>
    private sealed class SpySchedulerAsync : IAmAMessageSchedulerAsync
    {
        public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid().ToString());

        public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid().ToString());

        public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task CancelAsync(string id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

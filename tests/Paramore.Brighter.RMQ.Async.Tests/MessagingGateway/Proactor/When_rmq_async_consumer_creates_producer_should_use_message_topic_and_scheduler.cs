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
/// When the RMQ async consumer creates a producer lazily for delayed requeue,
/// the producer should have the scheduler configured so that delayed sends use the scheduler
/// rather than falling back to native publish without delay.
/// </summary>
[Trait("Category", "RMQ")]
public class RMQMessageConsumerProducerTopicSchedulerTestsAsync : IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly RmqMessageConsumer _consumer;
    private readonly SpySchedulerAsync _scheduler;
    private readonly Message _message;

    public RMQMessageConsumerProducerTopicSchedulerTestsAsync()
    {
        var rmqConnection =
            // Arrange - Exchange without native delay support
            new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var queueName = new ChannelName(Guid.NewGuid().ToString());

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content for scheduler injection"));

        _messageProducer = new RmqMessageProducer(rmqConnection);

        _scheduler = new SpySchedulerAsync();

        // Create consumer WITH scheduler
        _consumer = new RmqMessageConsumer(
            rmqConnection,
            queueName,
            topic,
            isDurable: false,
            scheduler: _scheduler);

        new QueueFactory(rmqConnection, queueName, new RoutingKeys(topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public async Task When_requeuing_with_delay_should_use_scheduler()
    {
        // Arrange - send and receive a message
        await _messageProducer.SendAsync(_message);
        var received = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
        Assert.NotEmpty(received);
        Assert.NotEqual(MessageType.MT_NONE, received[0].Header.MessageType);

        // Act - requeue with delay
        await _consumer.RequeueAsync(received[0], TimeSpan.FromSeconds(5));

        // Assert - scheduler should have been called (proving producer has scheduler configured)
        Assert.True(_scheduler.ScheduleAsyncCalled,
            "Scheduler.ScheduleAsync should have been called via the lazily created producer");
        Assert.Equal(_message.Body.Value, _scheduler.ScheduledMessage?.Body.Value);
    }

    [Fact]
    public async Task When_requeuing_with_zero_delay_should_not_create_producer()
    {
        // Arrange - send and receive a message
        await _messageProducer.SendAsync(_message);
        var received = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
        Assert.NotEmpty(received);
        Assert.NotEqual(MessageType.MT_NONE, received[0].Header.MessageType);

        // Act - requeue with zero delay (uses direct requeue, not producer)
        await _consumer.RequeueAsync(received[0], TimeSpan.Zero);

        // Assert - scheduler should NOT have been called
        Assert.False(_scheduler.ScheduleAsyncCalled,
            "Scheduler should not be called for zero-delay requeue");
    }

    public async ValueTask DisposeAsync()
    {
        _consumer.Dispose();
        await _messageProducer.DisposeAsync();
    }

    /// <summary>
    /// A spy async scheduler that records calls to ScheduleAsync for verification.
    /// </summary>
    private sealed class SpySchedulerAsync : IAmAMessageSchedulerAsync
    {
        public bool ScheduleAsyncCalled { get; private set; }
        public Message? ScheduledMessage { get; private set; }
        public TimeSpan? ScheduledDelay { get; private set; }

        public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            ScheduleAsyncCalled = true;
            ScheduledMessage = message;
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

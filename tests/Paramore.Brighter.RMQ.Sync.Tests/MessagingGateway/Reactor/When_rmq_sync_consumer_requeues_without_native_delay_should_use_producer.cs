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
using System.Diagnostics;
using System.Threading;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

/// <summary>
/// When the RMQ sync consumer requeues a message with a delay but native delay is not supported,
/// it should delegate to the producer's SendWithDelay instead of blocking the message pump
/// with Task.Delay().Wait(). This also verifies lazy producer creation and disposal.
/// </summary>
[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqSyncConsumerDelayTests : IDisposable
{
    private readonly IAmAMessageProducerSync _sendProducer;
    private readonly IAmAChannelSync _channel;
    private readonly Message _message;

    public RmqSyncConsumerDelayTests()
    {
        // Arrange - Exchange without native delay support (supportDelay defaults to false)
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var queueName = new ChannelName(Guid.NewGuid().ToString());

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content for sync delay requeue"));

        _sendProducer = new RmqMessageProducer(rmqConnection);

        var subscription = new RmqSubscription(
            subscriptionName: new SubscriptionName("rmq-sync-delay-producer-test"),
            channelName: queueName,
            routingKey: topic,
            requestType: typeof(MyCommand),
            messagePumpType: MessagePumpType.Reactor);

        _channel = new ChannelFactory(new RmqMessageConsumerFactory(rmqConnection))
            .CreateSyncChannel(subscription);

        new QueueFactory(rmqConnection, queueName, new RoutingKeys(topic))
            .Create(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void When_requeuing_with_delay_should_not_block_pump()
    {
        // Arrange - send and receive a message
        _sendProducer.Send(_message);
        var received = _channel.Receive(TimeSpan.FromMilliseconds(10000));
        Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

        // Act - requeue with a significant delay (5 seconds)
        var stopwatch = Stopwatch.StartNew();
        var result = _channel.Requeue(received, TimeSpan.FromSeconds(5));
        stopwatch.Stop();

        // Assert - requeue should return true
        Assert.True(result, "Requeue should succeed");

        // Assert - requeue should complete quickly, proving Task.Delay().Wait() is NOT used
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Requeue should not block with Task.Delay().Wait(); took {stopwatch.Elapsed.TotalSeconds:F1}s");

        // Assert - message should be available on the queue (published via producer through exchange)
        var requeued = _channel.Receive(TimeSpan.FromMilliseconds(10000));
        Assert.Equal(_message.Body.Value, requeued.Body.Value);
    }

    [Fact]
    public void When_disposing_should_dispose_lazily_created_producer()
    {
        // Arrange - send, receive, and requeue with delay to trigger producer creation
        _sendProducer.Send(_message);
        var received = _channel.Receive(TimeSpan.FromMilliseconds(10000));
        Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);
        
        //delay before requeue for test connection pool conflicts
        Thread.Sleep(TimeSpan.FromSeconds(2));
        
        _channel.Requeue(received, TimeSpan.FromSeconds(5));

        // Act & Assert - disposing channel (and its consumer) should not throw
        var exception = Record.Exception(() => _channel.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void When_disposing_without_producer_created_should_not_throw()
    {
        // Arrange - consumer that never requeued with delay (no producer created)
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var consumer = new RmqMessageConsumer(
            rmqConnection,
            new ChannelName(Guid.NewGuid().ToString()),
            new RoutingKey(Guid.NewGuid().ToString()),
            isDurable: false);

        // Act & Assert - should not throw
        var exception = Record.Exception(() => consumer.Dispose());
        Assert.Null(exception);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _sendProducer.Dispose();
    }
}

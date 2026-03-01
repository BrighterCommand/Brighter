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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

/// <summary>
/// When the RMQ async consumer requeues a message with a delay but native delay is not supported,
/// it should delegate to the producer's SendWithDelayAsync instead of blocking the message pump
/// with Task.Delay. This ensures the pump thread is freed immediately while the scheduler handles
/// the delayed redelivery.
/// </summary>
[Trait("Category", "RMQ")]
public class RmqMesageConsumerDelayTestsAsync : IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAChannelAsync _channel;
    private readonly Message _message;

    public RmqMesageConsumerDelayTestsAsync ()
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
            new MessageBody("test content for delay requeue"));

        _messageProducer = new RmqMessageProducer(rmqConnection);

        var subscription = new RmqSubscription(
            subscriptionName: new SubscriptionName("rmq-delay-producer-test"),
            channelName: queueName,
            routingKey: topic,
            requestType: typeof(MyCommand),
            messagePumpType: MessagePumpType.Proactor);

        _channel = new ChannelFactory(new RmqMessageConsumerFactory(rmqConnection))
            .CreateAsyncChannel(subscription);

        new QueueFactory(rmqConnection, queueName, new RoutingKeys(topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public async Task When_requeuing_with_delay_should_not_block_pump()
    {
        // Arrange - send and receive a message
        await _messageProducer.SendAsync(_message);
        var received = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
        Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

        // Act - requeue with a significant delay (5 seconds)
        var stopwatch = Stopwatch.StartNew();
        var result = await _channel.RequeueAsync(received, TimeSpan.FromSeconds(5));
        stopwatch.Stop();

        // Assert - requeue should return true
        Assert.True(result, "Requeue should succeed");

        // Assert - requeue should complete quickly, proving Task.Delay is NOT used to block the pump
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Requeue should not block with Task.Delay; took {stopwatch.Elapsed.TotalSeconds:F1}s");

        // Assert - message should be available on the queue (published via producer through exchange)
        var requeued = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
        Assert.Equal(_message.Body.Value, requeued.Body.Value);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Dispose();
        await _messageProducer.DisposeAsync();
    }
}

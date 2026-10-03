#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

/// <summary>
/// Bugfix 0025 (#4353): <c>RequeueAsync</c> and <c>NackAsync</c> call the broker. If that call fails, the
/// message still holds its receive lease and comes back when the lease lapses, so the consumer must not
/// throw: an exception from inside the pump's <c>DeferMessageAction</c> or <c>DontAckAction</c> handler
/// stops the pump. <c>RequeueAsync</c> must also return <c>true</c>, because <c>false</c> makes the pump
/// acknowledge, and so lose, the message.
/// </summary>
/// <remarks>
/// The failure is the client's own: once the consumer is disposed, the RocketMQ <c>SimpleConsumer</c>
/// refuses the call because it is not running.
/// </remarks>
[Trait("Category", "RocketMQ")]
public class RocketMqBrokerCallFailureTestsAsync : IAsyncLifetime
{
    private const string Topic = "rmq_broker_call_fails_p";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_receiveCeiling = TimeSpan.FromSeconds(30);

    private RocketMqMessageProducer? _producer;
    private IAmAMessageConsumerAsync? _consumer;
    private bool _consumerDisposed;

    public async Task InitializeAsync()
    {
        var connection = GatewayFactory.CreateConnection();
        var routingKey = new RoutingKey(Topic);
        var publication = new RocketMqPublication { Topic = routingKey };

        _producer = new RocketMqMessageProducer(
            connection,
            await GatewayFactory.CreateProducer(connection, publication),
            publication);

        // Fresh consumer group per run: a fresh group can still be handed stale messages left by
        // an earlier run on this topic, so the test picks its own message out by id.
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"broker-call-fails-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"broker-call-fails-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Proactor,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout);

        _consumer = new RocketMessageConsumerFactory(connection).CreateAsync(subscription);
    }

    public async Task DisposeAsync()
    {
        await DisposeConsumerAsync();

        if (_producer != null)
            await _producer.DisposeAsync();
    }

    [Fact]
    public async Task When_the_rocketmq_broker_call_behind_requeue_fails_should_return_true_and_leave_the_lease_async()
    {
        // Arrange - a received message, then a consumer that can no longer reach the broker
        var received = await SendAndReceiveAsync();
        await DisposeConsumerAsync();

        // Act
        var requeued = false;
        var exception = await Record.ExceptionAsync(async () => requeued = await _consumer!.RequeueAsync(received, TimeSpan.Zero));

        // Assert
        Assert.Null(exception);
        Assert.True(requeued);
    }

    [Fact]
    public async Task When_the_rocketmq_broker_call_behind_nack_fails_should_not_throw_async()
    {
        // Arrange - a received message, then a consumer that can no longer reach the broker
        var received = await SendAndReceiveAsync();
        await DisposeConsumerAsync();

        // Act
        var exception = await Record.ExceptionAsync(() => _consumer!.NackAsync(received));

        // Assert
        Assert.Null(exception);
    }

    private async Task<Message> SendAndReceiveAsync()
    {
        var routingKey = new RoutingKey(Topic);
        var message = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("rocketmq broker call failure test"));
        await _producer!.SendAsync(message);

        var received = await ReceiveOursAsync(message.Id);
        Assert.NotNull(received);
        return received!;
    }

    private async Task DisposeConsumerAsync()
    {
        if (_consumerDisposed || _consumer == null) return;
        await _consumer.DisposeAsync();
        _consumerDisposed = true;
    }

    /// <summary>
    /// Receives in a bounded loop, acknowledging anything that is not ours, since a fresh consumer
    /// group can be handed messages left behind by an earlier run on this topic.
    /// </summary>
    private async Task<Message?> ReceiveOursAsync(Id messageId)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < s_receiveCeiling)
        {
            foreach (var received in await _consumer!.ReceiveAsync(s_receiveMessageTimeout))
            {
                if (received.Header.MessageType == MessageType.MT_NONE)
                    continue;
                if (received.Id == messageId)
                    return received;

                await _consumer.AcknowledgeAsync(received);
            }
        }

        return null;
    }
}

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
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

/// <summary>
/// Bugfix 0025 (#4353): <c>Requeue</c> and <c>Nack</c> call the broker. If that call fails, the message
/// still holds its receive lease and comes back when the lease lapses, so the consumer must not throw:
/// an exception from inside the pump's <c>DeferMessageAction</c> or <c>DontAckAction</c> handler stops
/// the pump. <c>Requeue</c> must also return <c>true</c>, because <c>false</c> makes the pump acknowledge,
/// and so lose, the message.
/// </summary>
/// <remarks>
/// The failure is the client's own: once the consumer is disposed, the RocketMQ <c>SimpleConsumer</c>
/// refuses the call because it is not running.
/// </remarks>
[Trait("Category", "RocketMQ")]
public class RocketMqBrokerCallFailureTests : IDisposable
{
    private const string Topic = "rmq_broker_call_fails_r";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_receiveCeiling = TimeSpan.FromSeconds(30);

    private readonly RocketMqMessageProducer _producer;
    private readonly IAmAMessageConsumerSync _consumer;
    private bool _consumerDisposed;

    public RocketMqBrokerCallFailureTests()
    {
        var connection = GatewayFactory.CreateConnection();
        var routingKey = new RoutingKey(Topic);
        var publication = new RocketMqPublication { Topic = routingKey };

        _producer = new RocketMqMessageProducer(
            connection,
            GatewayFactory.CreateProducer(connection, publication).GetAwaiter().GetResult(),
            publication);

        // Fresh consumer group per run: a fresh group can still be handed stale messages left by
        // an earlier run on this topic, so the test picks its own message out by id.
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"broker-call-fails-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"broker-call-fails-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Reactor,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout);

        _consumer = new RocketMessageConsumerFactory(connection).Create(subscription);
    }

    public void Dispose()
    {
        DisposeConsumer();
        _producer.Dispose();
    }

    [Fact]
    public void When_the_rocketmq_broker_call_behind_requeue_fails_should_return_true_and_leave_the_lease()
    {
        // Arrange - a received message, then a consumer that can no longer reach the broker
        var received = SendAndReceive();
        DisposeConsumer();

        // Act
        var requeued = false;
        var exception = Record.Exception(() => requeued = _consumer.Requeue(received, TimeSpan.Zero));

        // Assert
        Assert.Null(exception);
        Assert.True(requeued);
    }

    [Fact]
    public void When_the_rocketmq_broker_call_behind_nack_fails_should_not_throw()
    {
        // Arrange - a received message, then a consumer that can no longer reach the broker
        var received = SendAndReceive();
        DisposeConsumer();

        // Act
        var exception = Record.Exception(() => _consumer.Nack(received));

        // Assert
        Assert.Null(exception);
    }

    private Message SendAndReceive()
    {
        var routingKey = new RoutingKey(Topic);
        var message = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("rocketmq broker call failure test"));
        _producer.Send(message);

        var received = ReceiveOurs(message.Id);
        Assert.NotNull(received);
        return received!;
    }

    private void DisposeConsumer()
    {
        if (_consumerDisposed) return;
        _consumer.Dispose();
        _consumerDisposed = true;
    }

    /// <summary>
    /// Receives in a bounded loop, acknowledging anything that is not ours, since a fresh consumer
    /// group can be handed messages left behind by an earlier run on this topic.
    /// </summary>
    private Message? ReceiveOurs(Id messageId)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < s_receiveCeiling)
        {
            foreach (var received in _consumer.Receive(s_receiveMessageTimeout))
            {
                if (received.Header.MessageType == MessageType.MT_NONE)
                    continue;
                if (received.Id == messageId)
                    return received;

                _consumer.Acknowledge(received);
            }
        }

        return null;
    }
}

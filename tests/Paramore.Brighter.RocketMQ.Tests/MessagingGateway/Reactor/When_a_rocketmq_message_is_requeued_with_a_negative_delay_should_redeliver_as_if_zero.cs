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
/// Bugfix 0025 (#4353): nothing stops a caller passing <c>Requeue</c> a negative delay. It must be
/// treated as zero, so the message comes straight back, rather than fail and fall back to the lease.
/// </summary>
[Trait("Category", "RocketMQ")]
public class RocketMqRequeueNegativeDelayTests : IDisposable
{
    private const string Topic = "rmq_requeue_negative_r";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_requeueDelay = TimeSpan.FromSeconds(-5);
    // Well inside the lease: a redelivery any later can only have come from the lease lapsing
    private static readonly TimeSpan s_redeliveryBound = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_receiveCeiling = TimeSpan.FromSeconds(30);

    private readonly RocketMqMessageProducer _producer;
    private readonly IAmAMessageConsumerSync _consumer;

    public RocketMqRequeueNegativeDelayTests()
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
            subscriptionName: new SubscriptionName($"requeue-negative-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"requeue-negative-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Reactor,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout);

        _consumer = new RocketMessageConsumerFactory(connection).Create(subscription);
    }

    public void Dispose()
    {
        _consumer.Dispose();
        _producer.Dispose();
    }

    [Fact]
    public void When_a_rocketmq_message_is_requeued_with_a_negative_delay_should_redeliver_as_if_zero()
    {
        // Arrange
        var routingKey = new RoutingKey(Topic);
        var message = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("rocketmq requeue negative delay test"));
        _producer.Send(message);

        var received = ReceiveOurs(message.Id);
        Assert.NotNull(received);

        // Act
        var requeued = _consumer.Requeue(received!, s_requeueDelay);
        var sinceRequeue = Stopwatch.StartNew();

        // Assert - accepted, and redelivered well before the lease would have lapsed
        Assert.True(requeued);

        var redelivered = ReceiveOurs(message.Id);
        var elapsed = sinceRequeue.Elapsed;

        Assert.NotNull(redelivered);
        Assert.True(elapsed < s_redeliveryBound,
            $"Requeued with a {s_requeueDelay.TotalSeconds:F0} s delay but redelivered after {elapsed.TotalSeconds:F1} s; " +
            $"expected under {s_redeliveryBound.TotalSeconds:F0} s, inside the {s_invisibilityTimeout.TotalSeconds:F0} s lease");

        _consumer.Acknowledge(redelivered!);
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

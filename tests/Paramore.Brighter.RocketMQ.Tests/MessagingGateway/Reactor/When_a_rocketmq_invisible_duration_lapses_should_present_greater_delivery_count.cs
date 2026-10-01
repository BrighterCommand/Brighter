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
using System.Threading;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

/// <summary>
/// Task 7.11 CHARACTERISE — R-1, AC-42 (ADR 0077). <c>RocketMessageConsumer</c> never extends a
/// received message's invisibility lease, so once the subscription's <c>invisibilityTimeout</c>
/// lapses on an unsettled message, RocketMQ redelivers it with its own <c>DeliveryAttempt</c>
/// advanced. This test does not drive the production pump and never calls <c>Requeue</c>: it
/// receives m1 directly off the consumer, holds it (no Acknowledge, Reject or Requeue), waits
/// past the invisibility timeout, and receives again to observe m2.
/// </summary>
/// <remarks>
/// Task 7.10 made <c>RocketMessageConsumer.CreateMessage</c> set
/// <c>header.HandledCount = DeliveryCount.Resolve(header.HandledCount, message.DeliveryAttempt, header.Bag)</c>,
/// so the lease-lapse redelivery's <c>DeliveryAttempt</c> (classified APPROXIMATE, task 7.1) flows
/// through into a strictly greater <see cref="MessageHeader.HandledCount"/> on m2. No production
/// change is expected from this test.
/// </remarks>
[Trait("Category", "RocketMQ")]
public class RocketMqLeaseLapseDeliveryCountTests : IDisposable
{
    private const string Topic = "rmq_lease_lapse_r";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_receiveCeiling = TimeSpan.FromSeconds(30);

    private readonly RocketMessagingGatewayConnection _connection;
    private readonly RocketMqMessageProducer _producer;
    private readonly IAmAMessageConsumerSync _consumer;

    public RocketMqLeaseLapseDeliveryCountTests()
    {
        _connection = GatewayFactory.CreateConnection();
        var routingKey = new RoutingKey(Topic);
        var publication = new RocketMqPublication { Topic = routingKey };

        _producer = new RocketMqMessageProducer(
            _connection,
            GatewayFactory.CreateProducer(_connection, publication).GetAwaiter().GetResult(),
            publication);

        // Fresh consumer group per run: a fresh group can still be handed stale messages left by
        // an earlier run on this topic, so the test picks its own message out by id.
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"lease-lapse-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"lease-lapse-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Reactor,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout);

        _consumer = new RocketMessageConsumerFactory(_connection).Create(subscription);
    }

    public void Dispose()
    {
        _consumer.Dispose();
        _producer.Dispose();
    }

    [Fact]
    public void When_a_rocketmq_invisible_duration_lapses_should_present_greater_delivery_count()
    {
        // Arrange - a message published with HandledCount = 0 (evident data, before it is ever sent)
        var routingKey = new RoutingKey(Topic);
        var header = new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND, handledCount: 0);
        var message = new Message(header, new MessageBody("rocketmq lease lapse delivery count test"));

        _producer.Send(message);

        // Act - receive m1 and hold it: no Acknowledge, Reject or Requeue. Wait past the
        // invisibility timeout so RocketMQ's own lease expiry — not a Requeue call — makes it
        // redeliverable, then receive again to observe the redelivery (no pump involved).
        var m1 = ReceiveOurs(_consumer, message.Id, s_receiveCeiling);
        Assert.NotNull(m1);
        Assert.Equal(message.Id, m1!.Id);

        var stopwatch = Stopwatch.StartNew();
        Thread.Sleep(s_invisibilityTimeout + TimeSpan.FromSeconds(2));

        var m2 = ReceiveOurs(_consumer, message.Id, s_receiveCeiling);
        var elapsed = stopwatch.Elapsed;

        // Assert - the expiry redelivery presents a strictly greater count than m1's (AC-42).
        Assert.NotNull(m2);
        Assert.Equal(message.Id, m2!.Id);
        Assert.True(m2.Header.HandledCount > m1.Header.HandledCount,
            $"R-1: m2 count {m2.Header.HandledCount} must be greater than m1 count {m1.Header.HandledCount}"
            + $" (elapsed from m1 to m2: {elapsed})");

        // Settle m2 so nothing lingers on the topic for a later run. m1 was never settled.
        _consumer.Acknowledge(m2);
    }

    /// <summary>
    /// Receives in a bounded loop, acknowledging anything that is not ours, since a fresh consumer
    /// group can be handed messages left behind by an earlier run on this topic.
    /// </summary>
    private static Message? ReceiveOurs(IAmAMessageConsumerSync consumer, Id messageId, TimeSpan ceiling)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < ceiling)
        {
            foreach (var received in consumer.Receive(s_receiveMessageTimeout))
            {
                if (received.Header.MessageType == MessageType.MT_NONE)
                    continue;
                if (received.Id == messageId)
                    return received;

                consumer.Acknowledge(received);
            }
        }

        return null;
    }
}

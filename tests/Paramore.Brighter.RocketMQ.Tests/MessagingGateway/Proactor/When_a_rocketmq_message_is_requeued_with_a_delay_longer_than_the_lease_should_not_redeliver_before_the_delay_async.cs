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
/// Bugfix 0025 (#4353): <c>RequeueAsync(message, delay)</c> must honour the delay on the broker. The
/// delay here is longer than the subscription's invisibility lease, so a redelivery that arrives inside
/// the quiet window can only have come from the receive lease lapsing, which means the delay was ignored.
/// </summary>
[Trait("Category", "RocketMQ")]
public class RocketMqRequeueDelayBeyondLeaseTestsAsync : IAsyncLifetime
{
    private const string Topic = "rmq_requeue_beyond_lease_p";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_requeueDelay = TimeSpan.FromSeconds(20);
    // Longer than the lease, shorter than the delay: only an ignored delay lets the message in here
    private static readonly TimeSpan s_quietWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_receiveCeiling = TimeSpan.FromSeconds(30);

    private RocketMqMessageProducer? _producer;
    private IAmAMessageConsumerAsync? _consumer;

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
            subscriptionName: new SubscriptionName($"requeue-beyond-lease-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"requeue-beyond-lease-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Proactor,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout);

        _consumer = new RocketMessageConsumerFactory(connection).CreateAsync(subscription);
    }

    public async Task DisposeAsync()
    {
        if (_consumer != null)
            await _consumer.DisposeAsync();

        if (_producer != null)
            await _producer.DisposeAsync();
    }

    [Fact]
    public async Task When_a_rocketmq_message_is_requeued_with_a_delay_longer_than_the_lease_should_not_redeliver_before_the_delay_async()
    {
        // Arrange
        var routingKey = new RoutingKey(Topic);
        var message = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("rocketmq requeue delay beyond lease test"));
        await _producer!.SendAsync(message);

        var received = await ReceiveOursAsync(message.Id, s_receiveCeiling);
        Assert.NotNull(received);

        // Act
        var requeued = await _consumer!.RequeueAsync(received!, s_requeueDelay);
        var sinceRequeue = Stopwatch.StartNew();

        // Assert - nothing within the quiet window, then the message comes back
        Assert.True(requeued);

        var early = await ReceiveOursAsync(message.Id, s_quietWindow);
        Assert.True(early == null,
            $"Requeued with a {s_requeueDelay.TotalSeconds:F0} s delay but redelivered after " +
            $"{sinceRequeue.Elapsed.TotalSeconds:F1} s; the {s_invisibilityTimeout.TotalSeconds:F0} s lease, not the delay, decided it");

        var redelivered = await ReceiveOursAsync(message.Id, s_receiveCeiling);
        Assert.NotNull(redelivered);

        await _consumer.AcknowledgeAsync(redelivered!);
    }

    /// <summary>
    /// Receives in a bounded loop, acknowledging anything that is not ours, since a fresh consumer
    /// group can be handed messages left behind by an earlier run on this topic.
    /// </summary>
    private async Task<Message?> ReceiveOursAsync(Id messageId, TimeSpan ceiling)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < ceiling)
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

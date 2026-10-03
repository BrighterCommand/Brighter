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
/// Bugfix 0025 (#4353): RocketMQ rejects an invisible duration above 12 h. <c>RequeueAsync</c> with a
/// longer delay must hold the message for the 12 h maximum, the closest the broker allows, rather than
/// let it come back on the receive lease, which is far earlier than asked.
/// </summary>
/// <remarks>
/// The message cannot be awaited: on success it stays hidden for 12 h. A later run picks its own
/// message out by id, so the one left behind does no harm.
/// </remarks>
[Trait("Category", "RocketMQ")]
public class RocketMqRequeueDelayAboveMaximumTestsAsync : IAsyncLifetime
{
    private const string Topic = "rmq_requeue_over_max_p";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    // Above the broker's 12 h maximum invisible duration
    private static readonly TimeSpan s_requeueDelay = TimeSpan.FromHours(13);
    // Longer than the lease: a message that comes back in here fell back to the lease
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
            subscriptionName: new SubscriptionName($"requeue-over-max-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"requeue-over-max-{Guid.NewGuid():N}",
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
    public async Task When_a_rocketmq_message_is_requeued_with_a_delay_above_the_broker_maximum_should_hold_it_for_the_maximum_async()
    {
        // Arrange
        var routingKey = new RoutingKey(Topic);
        var message = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("rocketmq requeue delay above maximum test"));
        await _producer!.SendAsync(message);

        var received = await ReceiveOursAsync(message.Id, s_receiveCeiling);
        Assert.NotNull(received);

        // Act
        var requeued = await _consumer!.RequeueAsync(received!, s_requeueDelay);
        var sinceRequeue = Stopwatch.StartNew();

        // Assert - accepted, and still hidden after the lease would have lapsed
        Assert.True(requeued);

        var early = await ReceiveOursAsync(message.Id, s_quietWindow);
        Assert.True(early == null,
            $"Requeued with a {s_requeueDelay.TotalHours:F0} h delay but redelivered after " +
            $"{sinceRequeue.Elapsed.TotalSeconds:F1} s; it fell back to the {s_invisibilityTimeout.TotalSeconds:F0} s lease");
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

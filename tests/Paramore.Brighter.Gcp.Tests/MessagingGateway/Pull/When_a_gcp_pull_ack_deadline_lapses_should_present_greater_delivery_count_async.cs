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

#nullable enable

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// Async (Proactor) twin of <see cref="GcpPullAckDeadlineLapseDeliveryCountTests"/> and
/// <see cref="GcpPullOrderingAckDeadlineLapseDeliveryCountTests"/> (NFR-8). Characterises R-1 /
/// AC-42 (ADR 0077) on <c>GcpPullMessageConsumer</c>'s expiry path through <c>ReceiveAsync</c>. No
/// pump and no <c>RequeueAsync</c> call is involved.
/// </summary>
/// <summary>Pull (unordered), async (Proactor) path.</summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullAckDeadlineLapseDeliveryCountAsyncTests
{
    private const int AckDeadlineSeconds = 10;
    private const int RedeliveryWindowSeconds = 45;

    /// <summary>
    /// AC-42, async path: a subscription with <c>AckDeadlineSeconds: 10</c> and a native
    /// <c>DeadLetterPolicy</c> (<c>MaxDeliveryAttempts: 5</c>, the emulator's minimum), and a message
    /// published with <c>HandledCount = 0</c>. Receiving m1, holding it, letting the ack deadline
    /// lapse and receiving again presents a strictly greater <c>HandledCount</c> on m2.
    /// </summary>
    [Fact]
    public async Task When_a_gcp_pull_ack_deadline_lapses_should_present_greater_delivery_count_async()
    {
        // Arrange — AckDeadlineSeconds: 10 is the knob that actually governs the lease for
        // GcpPullMessageConsumer (it does a raw Pull and never extends the deadline); the native
        // DeadLetterPolicy's MaxDeliveryAttempts: 5 is the emulator's minimum and is what makes
        // Pub/Sub populate ReceivedMessage.DeliveryAttempt across redeliveries (6.7's measurement).
        var provider = new GcpPullMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com",
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            subscriberMember: "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com");

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(subscription);

            var message = new DefaultMessageBuilder().SetTopic(routingKey).Build();
            // Evident data: a freshly-built Message presents HandledCount 0 before it is ever sent.
            Assert.Equal(0, message.Header.HandledCount);
            await producer.SendAsync(message);

            // Act — receive m1 and hold it: no AcknowledgeAsync, RejectAsync or RequeueAsync. Wait
            // past the 10s ack deadline so Pub/Sub's own lease expiry — not a RequeueAsync call —
            // makes it redeliverable, then receive again directly off the channel (no pump involved).
            var m1 = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, m1.Header.MessageType);

            await Task.Delay(TimeSpan.FromSeconds(AckDeadlineSeconds + 2));

            var m2 = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(RedeliveryWindowSeconds))
            {
                m2 = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
                if (m2.Header.MessageType != MessageType.MT_NONE && m2.Id == m1.Id)
                    break;
                await Task.Delay(250);
            }

            // Assert — the expiry redelivery presents a strictly greater count than m1's (AC-42).
            Assert.NotEqual(MessageType.MT_NONE, m2.Header.MessageType);
            Assert.Equal(m1.Id, m2.Id);
            Assert.True(m2.Header.HandledCount > m1.Header.HandledCount,
                $"AC-42: the expiry redelivery's count {m2.Header.HandledCount} must be strictly "
                + $"greater than the first delivery's count {m1.Header.HandledCount}");

            // Settle m2 so it does not keep redelivering into later tests. m1's ack id is stale by
            // now (Pub/Sub does not honour it once the lease has lapsed and been redelivered), so
            // we do not rely on acknowledging it.
            await channel.AcknowledgeAsync(m2);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

/// <summary>PullOrdering, async (Proactor) path — same clauses as the unordered Pull configuration.</summary>
[Trait("Category", "GcpPubSubPullOrdering")]
[Collection("PullOrdering")]
public class GcpPullOrderingAckDeadlineLapseDeliveryCountAsyncTests
{
    private const int AckDeadlineSeconds = 10;
    private const int RedeliveryWindowSeconds = 45;

    /// <summary>
    /// AC-42, async path, on an ordering-enabled subscription: same clauses as
    /// <see cref="GcpPullAckDeadlineLapseDeliveryCountAsyncTests"/>.
    /// </summary>
    [Fact]
    public async Task When_a_gcp_pull_ordering_ack_deadline_lapses_should_present_greater_delivery_count_async()
    {
        // Arrange
        var provider = new GcpPullOrderingMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com",
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            subscriberMember: "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com");

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(subscription);

            var message = new DefaultMessageBuilder().SetTopic(routingKey).Build();
            Assert.Equal(0, message.Header.HandledCount);
            await producer.SendAsync(message);

            // Act
            var m1 = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, m1.Header.MessageType);

            await Task.Delay(TimeSpan.FromSeconds(AckDeadlineSeconds + 2));

            var m2 = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(RedeliveryWindowSeconds))
            {
                m2 = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
                if (m2.Header.MessageType != MessageType.MT_NONE && m2.Id == m1.Id)
                    break;
                await Task.Delay(250);
            }

            // Assert
            Assert.NotEqual(MessageType.MT_NONE, m2.Header.MessageType);
            Assert.Equal(m1.Id, m2.Id);
            Assert.True(m2.Header.HandledCount > m1.Header.HandledCount,
                $"AC-42: the expiry redelivery's count {m2.Header.HandledCount} must be strictly "
                + $"greater than the first delivery's count {m1.Header.HandledCount}");

            await channel.AcknowledgeAsync(m2);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

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
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// Async (Proactor) twin of <see cref="GcpStreamLeaseLapseDeliveryCountTests"/> and
/// <see cref="GcpStreamOrderingLeaseLapseDeliveryCountTests"/> (NFR-8). Characterises R-1 / R-13 /
/// AC-42 (ADR 0077, "GCP stream consumer lease-lapse procedure for AC-42") on
/// <c>GcpPubSubStreamMessageConsumer</c> through <c>ReceiveAsync</c>.
/// </summary>
/// <summary>Stream (unordered), async (Proactor) path.</summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamLeaseLapseDeliveryCountAsyncTests
{
    private const int AckDeadlineSeconds = 10;
    private const int RedeliveryWindowSeconds = 45;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// AC-42, async path: same configuration and sequence as
    /// <see cref="GcpStreamLeaseLapseDeliveryCountTests"/>, through <c>ReceiveAsync</c>.
    /// </summary>
    [Fact]
    public async Task When_a_gcp_stream_lease_lapses_should_present_greater_delivery_count_async()
    {
        // Arrange
        var provider = new GcpStreamMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var subscription = new GcpPubSubSubscription(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            requestType: typeof(MyCommand),
            bufferSize: 2,
            noOfPerformers: 1,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            deadLetter: new DeadLetterPolicy(
                new RoutingKey($"{routingKey.Value}.native"),
                new ChannelName($"{routingKey.Value}.native"))
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = GcpEmulatorIamMember.Value,
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream,
            // See the sync twin: repeating the connection's emulator detection is redundant since
            // #4516, and the client's own stream AckDeadline (not just MaxTotalAckExtension) must be
            // capped at 10 s (6.13).
            streamingConfiguration: builder =>
            {
                builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
                builder.Settings = new SubscriberClient.Settings
                {
                    MaxTotalAckExtension = TimeSpan.FromSeconds(10),
                    AckDeadline = TimeSpan.FromSeconds(10),
                };
            },
            subscriberMember: GcpEmulatorIamMember.Value);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(subscription);

            var message = new DefaultMessageBuilder().SetTopic(routingKey).Build();
            Assert.Equal(0, message.Header.HandledCount);
            await producer.SendAsync(message);

            // Act — receive m1 and hold it: no AcknowledgeAsync, RejectAsync or RequeueAsync. Poll
            // every 500 ms, up to 45 s, for the lease-lapse redelivery.
            var m1 = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, m1.Header.MessageType);

            var m2 = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(RedeliveryWindowSeconds))
            {
                m2 = await channel.ReceiveAsync(PollInterval);
                if (m2.Header.MessageType != MessageType.MT_NONE && m2.Id == m1.Id)
                    break;
            }

            // Assert — the lease-lapse redelivery presents a strictly greater count than m1's (AC-42).
            Assert.NotEqual(MessageType.MT_NONE, m2.Header.MessageType);
            Assert.Equal(m1.Id, m2.Id);
            Assert.True(m2.Header.HandledCount > m1.Header.HandledCount,
                $"AC-42: the lease-lapse redelivery's count {m2.Header.HandledCount} must be "
                + $"strictly greater than the first delivery's count {m1.Header.HandledCount}");

            // Settle m2 then m1, so the stream consumer's WaitForProcessing shutdown completes.
            await channel.AcknowledgeAsync(m2);
            await channel.AcknowledgeAsync(m1);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

/// <summary>
/// StreamOrdering, async (Proactor) path — the keyless alternative (6.13/ADR 0077), same
/// reasoning as <see cref="GcpStreamOrderingLeaseLapseDeliveryCountTests"/>.
/// </summary>
[Trait("Category", "GcpPubSubStreamOrdering")]
[Collection("StreamOrdering")]
public class GcpStreamOrderingLeaseLapseDeliveryCountAsyncTests
{
    private const int AckDeadlineSeconds = 10;
    private const int RedeliveryWindowSeconds = 45;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// AC-42, async path, on an ordering-enabled subscription: same clauses as
    /// <see cref="GcpStreamLeaseLapseDeliveryCountAsyncTests"/>, but the message is published
    /// without a partition key (the keyless alternative, 6.13).
    /// </summary>
    [Fact]
    public async Task When_a_gcp_stream_ordering_lease_lapses_should_present_greater_delivery_count_async()
    {
        // Arrange
        var provider = new GcpStreamOrderingMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var subscription = new GcpPubSubSubscription(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            requestType: typeof(MyCommand),
            bufferSize: 2,
            noOfPerformers: 1,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            enableMessageOrdering: true,
            deadLetter: new DeadLetterPolicy(
                new RoutingKey($"{routingKey.Value}.native"),
                new ChannelName($"{routingKey.Value}.native"))
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = GcpEmulatorIamMember.Value,
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream,
            streamingConfiguration: builder =>
            {
                builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
                builder.Settings = new SubscriberClient.Settings
                {
                    MaxTotalAckExtension = TimeSpan.FromSeconds(10),
                    AckDeadline = TimeSpan.FromSeconds(10),
                };
            },
            subscriberMember: GcpEmulatorIamMember.Value);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(subscription);

            // Keyless: no SetPartitionKey call (6.13 — a keyed message would not be redelivered
            // while m1 is held on this ordering-enabled subscription).
            var message = new DefaultMessageBuilder().SetTopic(routingKey).Build();
            Assert.Equal(0, message.Header.HandledCount);
            await producer.SendAsync(message);

            // Act
            var m1 = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, m1.Header.MessageType);

            var m2 = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(RedeliveryWindowSeconds))
            {
                m2 = await channel.ReceiveAsync(PollInterval);
                if (m2.Header.MessageType != MessageType.MT_NONE && m2.Id == m1.Id)
                    break;
            }

            // Assert
            Assert.NotEqual(MessageType.MT_NONE, m2.Header.MessageType);
            Assert.Equal(m1.Id, m2.Id);
            Assert.True(m2.Header.HandledCount > m1.Header.HandledCount,
                $"AC-42: the lease-lapse redelivery's count {m2.Header.HandledCount} must be "
                + $"strictly greater than the first delivery's count {m1.Header.HandledCount}");

            await channel.AcknowledgeAsync(m2);
            await channel.AcknowledgeAsync(m1);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

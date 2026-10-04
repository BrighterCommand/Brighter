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
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// Characterises R-1 / R-13 / AC-42 (ADR 0077, "GCP stream consumer lease-lapse procedure for
/// AC-42") on <c>GcpPubSubStreamMessageConsumer</c>: with <c>bufferSize: 2</c>,
/// <c>noOfPerformers: 1</c> (a second flow-control slot exists while the first delivery is held),
/// subscription <c>AckDeadlineSeconds: 10</c>, a native <c>DeadLetterPolicy</c> (M = 5) so
/// Pub/Sub populates the delivery attempt, and a <c>StreamingConfiguration</c> hook capping both
/// <c>MaxTotalAckExtension</c> and the client's own stream <c>AckDeadline</c> at 10 s (6.13's
/// measurement: without the client <c>AckDeadline</c> the lapse lands at ~60 s, outside the 45 s
/// poll — with both set it lands at ~15 s), a message whose lease lapses without
/// <c>Acknowledge</c> or <c>Requeue</c> presents a strictly greater <c>HandledCount</c> on the
/// redelivery.
/// </summary>
/// <summary>Stream (unordered), sync (Reactor) path.</summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamLeaseLapseDeliveryCountTests
{
    private const int AckDeadlineSeconds = 10;
    private const int RedeliveryWindowSeconds = 45;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// AC-42, sync path: a subscription with <c>AckDeadlineSeconds: 10</c>, a native
    /// <c>DeadLetterPolicy</c> (<c>MaxDeliveryAttempts: 5</c>), <c>bufferSize: 2</c>,
    /// <c>noOfPerformers: 1</c> and a <c>StreamingConfiguration</c> capping
    /// <c>MaxTotalAckExtension</c> and the client's stream <c>AckDeadline</c> at 10 s, and a
    /// message published with <c>HandledCount = 0</c>. Receiving m1, holding it (no Acknowledge,
    /// Reject or Requeue) and polling every 500 ms for up to 45 s presents a redelivery m2 with a
    /// strictly greater <c>HandledCount</c> than m1's.
    /// </summary>
    [Fact]
    public void When_a_gcp_stream_lease_lapses_should_present_greater_delivery_count()
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
            messagePumpType: MessagePumpType.Reactor,
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
            // The connection's StreamConfiguration runs before this one (#4516), so repeating its
            // emulator detection here is redundant but harmless.
            // MaxTotalAckExtension alone is not enough (6.13): the client leases each message for
            // its own stream AckDeadline (default 60 s) regardless of the subscription's
            // AckDeadlineSeconds, so Settings.AckDeadline must also be set to 10 s.
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

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(subscription);

            var message = new DefaultMessageBuilder().SetTopic(routingKey).Build();
            // Evident data: a freshly-built Message presents HandledCount 0 before it is ever sent.
            Assert.Equal(0, message.Header.HandledCount);
            producer.Send(message);

            // Act — receive m1 and hold it: no Acknowledge, Reject or Requeue. Poll every 500 ms,
            // up to 45 s, for the lease-lapse redelivery (6.13: ~15 s with the client AckDeadline
            // also set to 10 s).
            var m1 = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, m1.Header.MessageType);

            var m2 = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(RedeliveryWindowSeconds))
            {
                m2 = channel.Receive(PollInterval);
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
            channel.Acknowledge(m2);
            channel.Acknowledge(m1);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}

/// <summary>
/// StreamOrdering, sync (Reactor) path — the keyless alternative (6.13/ADR 0077): ordered
/// delivery withholds a same-key redelivery while its predecessor is outstanding, so the primary
/// procedure is not drivable with an ordering key on this subscription. A message published
/// WITHOUT a partition key on the same ordering-enabled subscription still exercises the stream
/// consumer's lease-lapse path.
/// </summary>
[Trait("Category", "GcpPubSubStreamOrdering")]
[Collection("StreamOrdering")]
public class GcpStreamOrderingLeaseLapseDeliveryCountTests
{
    private const int AckDeadlineSeconds = 10;
    private const int RedeliveryWindowSeconds = 45;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// AC-42, sync path, on an ordering-enabled subscription: same clauses as
    /// <see cref="GcpStreamLeaseLapseDeliveryCountTests"/>, but the message is published without a
    /// partition key (the keyless alternative), since a keyed message's redelivery is withheld
    /// while m1 is outstanding (6.13).
    /// </summary>
    [Fact]
    public void When_a_gcp_stream_ordering_lease_lapses_should_present_greater_delivery_count()
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
            messagePumpType: MessagePumpType.Reactor,
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

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(subscription);

            // Keyless: no SetPartitionKey call. A keyed message on this subscription would not be
            // redelivered while m1 is held (6.13); the keyless message still exercises the stream
            // consumer's lease-lapse path on the ordering-enabled configuration.
            var message = new DefaultMessageBuilder().SetTopic(routingKey).Build();
            Assert.Equal(0, message.Header.HandledCount);
            producer.Send(message);

            // Act
            var m1 = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, m1.Header.MessageType);

            var m2 = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(RedeliveryWindowSeconds))
            {
                m2 = channel.Receive(PollInterval);
                if (m2.Header.MessageType != MessageType.MT_NONE && m2.Id == m1.Id)
                    break;
            }

            // Assert
            Assert.NotEqual(MessageType.MT_NONE, m2.Header.MessageType);
            Assert.Equal(m1.Id, m2.Id);
            Assert.True(m2.Header.HandledCount > m1.Header.HandledCount,
                $"AC-42: the lease-lapse redelivery's count {m2.Header.HandledCount} must be "
                + $"strictly greater than the first delivery's count {m1.Header.HandledCount}");

            channel.Acknowledge(m2);
            channel.Acknowledge(m1);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}

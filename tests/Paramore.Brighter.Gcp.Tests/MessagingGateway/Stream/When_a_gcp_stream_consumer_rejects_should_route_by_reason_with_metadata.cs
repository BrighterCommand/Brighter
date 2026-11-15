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
using System.Threading;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using System.Threading.Tasks;
namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// R-16, R-18, AC-15, AC-16, NFR-8 — sync (Reactor) path.
/// A GCP stream Reject routes a stamped copy to the dead-letter or invalid-message destination
/// based on <see cref="MessageRejectionReason"/>, accepts the original stream handle, and returns
/// true. Covers GCP/Stream and GCP/StreamOrdering, both rejection reasons for DLQ routing
/// (DeliveryError with description; None with no description), the Unacceptable routing rules
/// (routes to .Invalid, falls back to .DLQ with no invalid key, and no-destination with neither
/// key — ADR 0078:38, AC-16), the missing-handle defensive path (ADR 0078 "Missing receipt handle
/// (both consumers)"), and — ADR 0078:38 / 0077's "0078 must not reintroduce it" — that a DLQ-routed
/// copy carries no <c>googclient_deliveryattempt</c> attribute, asserted on the raw
/// <see cref="PubsubMessage"/> read with a raw <see cref="SubscriberServiceApiClient.Pull"/>, not
/// through <c>Parser</c> or a Brighter channel (5.5d already strips the key from
/// <c>Header.Bag</c> on read, so a channel read could not catch the router re-stamping it; a raw
/// pull cannot inject it either, since <c>delivery_attempt</c> is a separate PubsubMessage field).
/// </summary>
/// <remarks>
/// Nuance: on Pull, "the source is settled" is asserted with a second <c>channel.Receive(...)</c>
/// returning <c>MT_NONE</c>, because acking makes a re-poll return nothing. Stream has no such
/// re-poll semantics — the shared stream consumer's channel would just block/timeout regardless of
/// whether the message was accepted, since Nack/redelivery on Stream only happens after the ack
/// deadline lapses. So, following the existing Stream idiom
/// (<c>When_a_gcp_message_carries_delivery_attempt_attribute_should_not_copy_it_into_bag.cs</c>),
/// each test treats the routed copy's arrival and the <c>Reject</c> return value as the evidence of
/// correct handling, and does not assert a second receive on the source channel.
/// </remarks>
[Property("Category", "GcpPubSubStream")]
[NotInParallel("Stream")]
public class GcpStreamConsumerRejectRoutingTests
{
    private const string DeliveryAttemptAttrKey = "googclient_deliveryattempt";

    // -------------------------------------------------------------------------
    // GCP / Stream — DeliveryError -> .DLQ
    // -------------------------------------------------------------------------

    /// <summary>
    /// DeliveryError with a description: the DLQ copy carries all five metadata keys, and Reject
    /// returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_delivery_error_on_stream_should_publish_stamped_copy_to_dlq_and_accept_original()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            // Arrange
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

            // Assert — Reject returns true
            await Assert.That(result).IsTrue();

            // Assert — DLQ copy appears within 60s
            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Assert — five metadata keys present
            var bag = dlqMessage.Header.Bag;
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString()).IsEqualTo(originalTopic);
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.DeliveryError.ToString());
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString()).IsEqualTo("Test delivery error");
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    /// <summary>
    /// The DLQ copy of a DeliveryError rejection carries no <c>googclient_deliveryattempt</c>
    /// attribute (ADR 0078:38). Asserted on the raw <see cref="PubsubMessage.Attributes"/> of the
    /// copy, fetched with a raw <see cref="SubscriberServiceApiClient.Pull"/> on the DLQ's reading
    /// subscription — not through <c>Parser</c> or a Brighter channel, because 5.5d's fix already
    /// strips the key from <c>Header.Bag</c> on read, so a channel-based read could not
    /// distinguish "never stamped" from "stamped, then stripped again on read back".
    /// </summary>
    [Test]
    public async Task When_rejecting_with_delivery_error_on_stream_should_not_leak_delivery_attempt_attribute_into_dlq_copy()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            // Arrange
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Raw attribute check"));
            await Assert.That(result).IsTrue();

            // Assert — raw pull off the DLQ subscription, bypassing Parser entirely
            var rawReceived = RawPullFromDlq(dlqRoutingKey, TimeSpan.FromSeconds(60));
            await Assert.That(rawReceived).IsNotNull();

            var attributes = rawReceived!.Message.Attributes;
            await Assert.That(attributes.ContainsKey(DeliveryAttemptAttrKey)).IsFalse().Because("The DLQ copy's raw PubsubMessage.Attributes must not carry googclient_deliveryattempt");

            // The copy still carries the rejection metadata as raw attributes (Bag entries are
            // republished verbatim as PubsubMessage attributes by Parser.AddHeaders).
            await Assert.That(attributes.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(attributes[RejectionMetadataKeyNames.OriginalTopic]).IsEqualTo(routingKey.Value);
            await Assert.That(attributes.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(attributes[RejectionMetadataKeyNames.RejectionReason]).IsEqualTo(RejectionReason.DeliveryError.ToString());
            await Assert.That(attributes.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            await Assert.That(attributes.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsTrue();
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — None reason -> .DLQ
    // -------------------------------------------------------------------------

    /// <summary>
    /// None reason (null): the DLQ copy carries exactly four metadata keys (no rejectionMessage),
    /// rejectionReason == "None", and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_none_reason_on_stream_should_publish_copy_with_four_metadata_keys_to_dlq()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act — null reason maps to "None"
            var result = channel.Reject(received, reason: null);
            await Assert.That(result).IsTrue();

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var bag = dlqMessage.Header.Bag;
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo("None");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsFalse().Because("DLQ copy for None reason must NOT carry rejectionMessage");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — Missing receipt handle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Missing receipt handle (ADR 0078): the router still publishes a stamped copy to the DLQ,
    /// and Reject returns true even though the original cannot be settled, which is logged as an
    /// Error.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_missing_receipt_handle_on_stream_should_still_publish_to_dlq_and_return_true()
    {
        var provider = new GcpStreamMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            // Build a message with no ReceiptHandle (test constructs it; never received from the stream)
            var message = new Message(
                new MessageHeader(Id.Create(Guid.NewGuid().ToString()), routingKey, MessageType.MT_COMMAND),
                new MessageBody("missing-handle-body"));
            message.Header.Bag.Remove("ReceiptHandle");

            using var logContext = TestCorrelator.CreateContext();

            // Act
            var result = channel.Reject(message, new MessageRejectionReason(RejectionReason.DeliveryError, "missing handle test"));

            // Assert — Reject returns true
            await Assert.That(result).IsTrue();

            // Assert — an Error naming the message says the original cannot be settled
            var settleError = await Assert.That(System.Linq.Enumerable.Where(TestCorrelator.GetLogEventsFromCurrentContext(), e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(message.Id.Value))).HasSingleItem();
            await Assert.That(settleError.RenderMessage()).Contains("cannot be settled");

            // Assert — DLQ copy still published within 60s
            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — Unacceptable routes to .Invalid
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with both a DLQ and an invalid-message key configured: the copy goes to
    /// .Invalid (not .DLQ), carrying the same rejection metadata as the DLQ copy.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_unacceptable_on_stream_should_route_to_invalid_channel()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var invalidRoutingKey = new RoutingKey($"{routingKey.Value}.Invalid");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey,
            invalidMessageRoutingKey: invalidRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable message"));
            await Assert.That(result).IsTrue();

            Message invalidMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                invalidMessage = provider.GetMessageFromInvalidChannel(subscription);
                if (invalidMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(invalidMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var bag = invalidMessage.Header.Bag;
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString()).IsEqualTo(originalTopic);
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.Unacceptable.ToString());
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString()).IsEqualTo("Test unacceptable message");

            // Assert — nothing was sent to the DLQ instead
            var dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
            await Assert.That(dlqMessage.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — Unacceptable with no invalid key falls back to DLQ (AC-16)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with a DLQ key configured but no invalid key: the copy falls back to .DLQ.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_unacceptable_on_stream_and_no_invalid_key_should_fall_back_to_dlq()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable, DLQ fallback"));
            await Assert.That(result).IsTrue();

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var bag = dlqMessage.Header.Bag;
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.Unacceptable.ToString());
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — Unacceptable with neither key configured: no destination
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with neither a DLQ nor an invalid key configured: nothing is published, and
    /// Reject still returns true (RoutingOutcome.NoDestination; its Warning log is a later task and
    /// is not asserted here).
    /// </summary>
    [Test]
    public async Task When_rejecting_with_unacceptable_on_stream_and_no_destinations_should_accept_without_publishing()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable, no destination"));

            // Assert — Reject still returns true
            await Assert.That(result).IsTrue();
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — DeliveryError -> .DLQ
    // -------------------------------------------------------------------------

    /// <summary>
    /// DeliveryError with a description on an ordering-enabled subscription: the DLQ copy carries
    /// all five metadata keys, and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_delivery_error_on_stream_ordering_should_publish_stamped_copy_to_dlq_and_accept_original()
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Ordering test error"));
            await Assert.That(result).IsTrue();

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var bag = dlqMessage.Header.Bag;
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString()).IsEqualTo(originalTopic);
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.DeliveryError.ToString());
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString()).IsEqualTo("Ordering test error");
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — None reason -> .DLQ
    // -------------------------------------------------------------------------

    /// <summary>
    /// None reason (null) on an ordering-enabled subscription: the DLQ copy carries exactly four
    /// metadata keys, rejectionReason == "None", and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_none_reason_on_stream_ordering_should_publish_copy_with_four_metadata_keys_to_dlq()
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = channel.Reject(received, reason: null);
            await Assert.That(result).IsTrue();

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var bag = dlqMessage.Header.Bag;
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo("None");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsFalse();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — Missing receipt handle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Missing receipt handle on an ordering-enabled subscription: the DLQ copy is still published
    /// and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_missing_receipt_handle_on_stream_ordering_should_still_publish_to_dlq_and_return_true()
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = new Message(
                new MessageHeader(Id.Create(Guid.NewGuid().ToString()), routingKey, MessageType.MT_COMMAND),
                new MessageBody("missing-handle-body-ordering"));
            message.Header.Bag.Remove("ReceiptHandle");

            using var logContext = TestCorrelator.CreateContext();

            // Act
            var result = channel.Reject(message, new MessageRejectionReason(RejectionReason.DeliveryError, "ordering missing handle test"));

            // Assert
            await Assert.That(result).IsTrue();

            var settleError = await Assert.That(System.Linq.Enumerable.Where(TestCorrelator.GetLogEventsFromCurrentContext(), e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(message.Id.Value))).HasSingleItem();
            await Assert.That(settleError.RenderMessage()).Contains("cannot be settled");

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — Unacceptable routes to .Invalid
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with both a DLQ and an invalid-message key
    /// configured: the copy goes to .Invalid (not .DLQ).
    /// </summary>
    [Test]
    public async Task When_rejecting_with_unacceptable_on_stream_ordering_should_route_to_invalid_channel()
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var invalidRoutingKey = new RoutingKey($"{routingKey.Value}.Invalid");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey,
            invalidMessageRoutingKey: invalidRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Ordering test unacceptable"));
            await Assert.That(result).IsTrue();

            Message invalidMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                invalidMessage = provider.GetMessageFromInvalidChannel(subscription);
                if (invalidMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(invalidMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var bag = invalidMessage.Header.Bag;
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString()).IsEqualTo(originalTopic);
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.Unacceptable.ToString());
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString()).IsEqualTo("Ordering test unacceptable");

            var dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
            await Assert.That(dlqMessage.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — Unacceptable with no invalid key falls back to DLQ
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with a DLQ key but no invalid key: the
    /// copy falls back to .DLQ.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_unacceptable_on_stream_ordering_and_no_invalid_key_should_fall_back_to_dlq()
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Ordering unacceptable, DLQ fallback"));
            await Assert.That(result).IsTrue();

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var bag = dlqMessage.Header.Bag;
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.Unacceptable.ToString());
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — Unacceptable with neither key configured: no destination
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with neither a DLQ nor an invalid key
    /// configured: nothing is published, and Reject still returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_unacceptable_on_stream_ordering_and_no_destinations_should_accept_without_publishing()
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Ordering unacceptable, no destination"));

            // Assert
            await Assert.That(result).IsTrue();
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // Raw-pull helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pulls a single message directly off the DLQ's reading subscription using a raw
    /// <see cref="SubscriberServiceApiClient"/>, bypassing Brighter's <c>Parser</c>/channel
    /// machinery entirely, and acknowledges it. Returns <see langword="null"/> if nothing arrives
    /// within <paramref name="timeout"/>.
    /// </summary>
    private static ReceivedMessage? RawPullFromDlq(RoutingKey dlqRoutingKey, TimeSpan timeout)
    {
        var connection = new GcpMessagingGatewayConnection
        {
            Credential = GatewayFactory.GetCredential(),
            ProjectId = GatewayFactory.GetProjectId(),
            SubscriptionManagerConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
        };

        var subscriptionName = Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(connection.ProjectId, dlqRoutingKey.Value);
        var client = connection.GetOrCreateSubscriberServiceApiClient();

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var response = client.Pull(new PullRequest
            {
                SubscriptionAsSubscriptionName = subscriptionName,
                MaxMessages = 1,
            });

            if (response.ReceivedMessages.Count > 0)
            {
                var received = response.ReceivedMessages[0];
                client.Acknowledge(subscriptionName, new[] { received.AckId });
                return received;
            }

            Thread.Sleep(500);
        }

        return null;
    }
}

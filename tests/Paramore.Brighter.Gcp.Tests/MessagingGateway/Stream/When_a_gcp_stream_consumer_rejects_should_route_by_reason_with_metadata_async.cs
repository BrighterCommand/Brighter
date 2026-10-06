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
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// R-16, R-18, AC-15, AC-16, NFR-8 — async (Proactor) path.
/// A GCP stream RejectAsync routes a stamped copy to the dead-letter or invalid-message
/// destination based on <see cref="MessageRejectionReason"/>, accepts the original stream handle,
/// and returns true. Covers GCP/Stream and GCP/StreamOrdering, both rejection reasons for DLQ
/// routing (DeliveryError with description; None with no description), the Unacceptable routing
/// rules (routes to .Invalid, falls back to .DLQ with no invalid key, and no-destination with
/// neither key — ADR 0078:38, AC-16), the missing-handle defensive path (ADR 0078 "Missing receipt
/// handle (both consumers)"), and — ADR 0078:38 / 0077's "0078 must not reintroduce it" — that a
/// DLQ-routed copy carries no <c>googclient_deliveryattempt</c> attribute, asserted on the raw
/// <see cref="PubsubMessage"/> read with a raw <see cref="SubscriberServiceApiClient.Pull"/>, not
/// through <c>Parser</c> or a Brighter channel.
/// </summary>
/// <remarks>
/// See the sync file's class remarks for why this does not assert a second receive on the source
/// channel as evidence of settlement (Stream has no re-poll-returns-nothing semantics the way Pull
/// does).
/// </remarks>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamConsumerRejectRoutingAsyncTests
{
    private const string DeliveryAttemptAttrKey = "googclient_deliveryattempt";

    // -------------------------------------------------------------------------
    // GCP / Stream — DeliveryError -> .DLQ (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// DeliveryError with a description: the DLQ copy carries all five metadata keys, and
    /// RejectAsync returns true.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_delivery_error_on_stream_should_publish_stamped_copy_to_dlq_and_accept_original()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var originalTopic = routingKey.Value;

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Async delivery error test"));
            Assert.True(result);

            Message dlqMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
            Assert.Equal(originalTopic, bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.DeliveryError.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            Assert.True(
                DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _),
                $"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
            Assert.Equal("Async delivery error test", bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString());
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    /// <summary>
    /// The DLQ copy of an async DeliveryError rejection carries no <c>googclient_deliveryattempt</c>
    /// attribute. Asserted on the raw <see cref="PubsubMessage.Attributes"/> fetched with a raw
    /// async <see cref="SubscriberServiceApiClient.PullAsync"/>, not through <c>Parser</c> or a
    /// Brighter channel.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_delivery_error_on_stream_should_not_leak_delivery_attempt_attribute_into_dlq_copy()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Async raw attribute check"));
            Assert.True(result);

            // Assert — raw async pull off the DLQ subscription, bypassing Parser entirely
            var rawReceived = await RawPullFromDlqAsync(dlqRoutingKey, TimeSpan.FromSeconds(60));
            Assert.NotNull(rawReceived);

            var attributes = rawReceived!.Message.Attributes;
            Assert.False(attributes.ContainsKey(DeliveryAttemptAttrKey),
                "The DLQ copy's raw PubsubMessage.Attributes must not carry googclient_deliveryattempt");

            Assert.True(attributes.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
            Assert.Equal(routingKey.Value, attributes[RejectionMetadataKeyNames.OriginalTopic]);
            Assert.True(attributes.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.DeliveryError.ToString(), attributes[RejectionMetadataKeyNames.RejectionReason]);
            Assert.True(attributes.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
            Assert.True(attributes.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — None reason -> .DLQ (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// None reason (null): the DLQ copy carries exactly four metadata keys (no rejectionMessage),
    /// rejectionReason == "None", and RejectAsync returns true.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_none_reason_on_stream_should_publish_copy_with_four_metadata_keys_to_dlq()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = await channel.RejectAsync(received, reason: null);
            Assert.True(result);

            Message dlqMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.Equal("None", bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
            Assert.False(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            Assert.True(
                DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _),
                $"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — Missing receipt handle (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Missing receipt handle: the DLQ copy is still published, an Error says the original cannot
    /// be settled, and RejectAsync returns true.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_missing_receipt_handle_on_stream_should_still_publish_to_dlq_and_return_true()
    {
        var provider = new GcpStreamMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = new Message(
                new MessageHeader(Id.Create(Guid.NewGuid().ToString()), routingKey, MessageType.MT_COMMAND),
                new MessageBody("async-missing-handle-body"));
            message.Header.Bag.Remove("ReceiptHandle");

            using var logContext = TestCorrelator.CreateContext();

            // Act
            var result = await channel.RejectAsync(message, new MessageRejectionReason(RejectionReason.DeliveryError, "async missing handle test"));

            // Assert
            Assert.True(result);

            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(message.Id.Value));
            Assert.Contains("cannot be settled", settleError.RenderMessage());

            Message dlqMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — Unacceptable routes to .Invalid (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with both a DLQ and an invalid-message key configured: the copy goes to
    /// .Invalid (not .DLQ), carrying the same rejection metadata.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_unacceptable_on_stream_should_route_to_invalid_channel()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var originalTopic = routingKey.Value;

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async unacceptable message"));
            Assert.True(result);

            Message invalidMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                invalidMessage = await provider.GetMessageFromInvalidChannelAsync(subscription);
                if (invalidMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, invalidMessage.Header.MessageType);

            var bag = invalidMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
            Assert.Equal(originalTopic, bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.Unacceptable.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            Assert.True(
                DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _),
                $"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
            Assert.Equal("Async unacceptable message", bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString());

            var dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
            Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — Unacceptable with no invalid key falls back to DLQ (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with a DLQ key configured but no invalid key: the copy falls back to .DLQ.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_unacceptable_on_stream_and_no_invalid_key_should_fall_back_to_dlq()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async unacceptable, DLQ fallback"));
            Assert.True(result);

            Message dlqMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.Unacceptable.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Stream — Unacceptable with neither key configured: no destination (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with neither a DLQ nor an invalid key configured: nothing is published, and
    /// RejectAsync still returns true.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_unacceptable_on_stream_and_no_destinations_should_accept_without_publishing()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async unacceptable, no destination"));

            // Assert
            Assert.True(result);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — DeliveryError -> .DLQ (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// DeliveryError with a description on an ordering-enabled subscription: the DLQ copy carries
    /// all five metadata keys, and RejectAsync returns true.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_delivery_error_on_stream_ordering_should_publish_stamped_copy_to_dlq_and_accept_original()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var originalTopic = routingKey.Value;

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Async ordering delivery error"));
            Assert.True(result);

            Message dlqMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
            Assert.Equal(originalTopic, bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.DeliveryError.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            Assert.True(
                DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _),
                $"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
            Assert.Equal("Async ordering delivery error", bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString());
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — None reason -> .DLQ (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// None reason (null) on an ordering-enabled subscription: the DLQ copy carries exactly four
    /// metadata keys, rejectionReason == "None", and RejectAsync returns true.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_none_reason_on_stream_ordering_should_publish_copy_with_four_metadata_keys_to_dlq()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = await channel.RejectAsync(received, reason: null);
            Assert.True(result);

            Message dlqMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.Equal("None", bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
            Assert.False(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            Assert.True(
                DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _),
                $"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — Missing receipt handle (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Missing receipt handle on an ordering-enabled subscription: the DLQ copy is still published
    /// and RejectAsync returns true.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_missing_receipt_handle_on_stream_ordering_should_still_publish_to_dlq_and_return_true()
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = new Message(
                new MessageHeader(Id.Create(Guid.NewGuid().ToString()), routingKey, MessageType.MT_COMMAND),
                new MessageBody("async-ordering-missing-handle-body"));
            message.Header.Bag.Remove("ReceiptHandle");

            using var logContext = TestCorrelator.CreateContext();

            // Act
            var result = await channel.RejectAsync(message, new MessageRejectionReason(RejectionReason.DeliveryError, "async ordering missing handle test"));

            // Assert
            Assert.True(result);

            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(message.Id.Value));
            Assert.Contains("cannot be settled", settleError.RenderMessage());

            Message dlqMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — Unacceptable routes to .Invalid (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with both a DLQ and an invalid-message key
    /// configured: the copy goes to .Invalid (not .DLQ).
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_unacceptable_on_stream_ordering_should_route_to_invalid_channel()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var originalTopic = routingKey.Value;

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async ordering unacceptable"));
            Assert.True(result);

            Message invalidMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                invalidMessage = await provider.GetMessageFromInvalidChannelAsync(subscription);
                if (invalidMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, invalidMessage.Header.MessageType);

            var bag = invalidMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
            Assert.Equal(originalTopic, bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.Unacceptable.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            Assert.True(
                DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _),
                $"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
            Assert.Equal("Async ordering unacceptable", bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString());

            var dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
            Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — Unacceptable with no invalid key falls back to DLQ (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with a DLQ key but no invalid key: the
    /// copy falls back to .DLQ.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_unacceptable_on_stream_ordering_and_no_invalid_key_should_fall_back_to_dlq()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async ordering unacceptable, DLQ fallback"));
            Assert.True(result);

            Message dlqMessage = new Message();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.Unacceptable.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / StreamOrdering — Unacceptable with neither key configured: no destination (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with neither a DLQ nor an invalid key
    /// configured: nothing is published, and RejectAsync still returns true.
    /// </summary>
    [Fact]
    public async Task When_rejecting_async_with_unacceptable_on_stream_ordering_and_no_destinations_should_accept_without_publishing()
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async ordering unacceptable, no destination"));

            // Assert
            Assert.True(result);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // Raw-pull helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asynchronously pulls a single message directly off the DLQ's reading subscription using a
    /// raw <see cref="SubscriberServiceApiClient"/>, bypassing Brighter's <c>Parser</c>/channel
    /// machinery entirely, and acknowledges it. Returns <see langword="null"/> if nothing arrives
    /// within <paramref name="timeout"/>.
    /// </summary>
    private static async Task<ReceivedMessage?> RawPullFromDlqAsync(RoutingKey dlqRoutingKey, TimeSpan timeout)
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
        var client = await connection.CreateSubscriberServiceApiClientAsync();

        var deadline = DateTime.UtcNow.AddSeconds(timeout.TotalSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.PullAsync(new PullRequest
            {
                SubscriptionAsSubscriptionName = subscriptionName,
                MaxMessages = 1,
            });

            if (response.ReceivedMessages.Count > 0)
            {
                var received = response.ReceivedMessages[0];
                await client.AcknowledgeAsync(subscriptionName, new[] { received.AckId });
                return received;
            }

            await Task.Delay(500);
        }

        return null;
    }
}

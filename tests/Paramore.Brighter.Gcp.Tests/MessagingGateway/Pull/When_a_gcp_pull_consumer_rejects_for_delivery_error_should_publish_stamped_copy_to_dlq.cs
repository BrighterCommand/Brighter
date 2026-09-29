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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using System.Threading.Tasks;
namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-16, R-18, AC-15, NFR-8 — sync (Reactor) path.
/// A GCP pull Reject routes a stamped copy to the dead-letter queue, acknowledges the original,
/// and returns true. Covers GCP/Pull and GCP/PullOrdering, both rejection reasons
/// (DeliveryError with description; None with no description), and the missing-receipt-handle
/// defensive path (ADR 0078 "Missing receipt handle (both consumers)").
/// </summary>
[Property("Category", "GcpPubSubPull")]
[NotInParallel("Pull")]
public class GcpPullConsumerRejectDlqRoutingTests
{
    // -------------------------------------------------------------------------
    // GCP / Pull — DeliveryError
    // -------------------------------------------------------------------------
    /// <summary>
    /// DeliveryError with a description: the DLQ copy carries all five metadata keys, the source
    /// returns MT_NONE after Reject, and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_delivery_error_on_pull_should_publish_stamped_copy_to_dlq_and_ack_original()
    {
        var provider = new GcpPullMessageGatewayProvider();
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

            var received = channel.Receive(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

            // Assert — Reject returns true
            await Assert.That(result).IsTrue();

            // Assert — source is acked: a second Receive returns MT_NONE
            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

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
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue().Because("DLQ copy must carry originalTopic");
            await Assert.That(bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString()).IsEqualTo(originalTopic);

            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue().Because("DLQ copy must carry originalMessageType");

            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue().Because("DLQ copy must carry rejectionReason");
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.DeliveryError.ToString());

            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue().Because("DLQ copy must carry rejectionTimestamp");
            // Timestamp must be ISO 8601 round-trip
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");

            // DeliveryError with non-empty description → rejectionMessage present
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsTrue().Because("DLQ copy must carry rejectionMessage for DeliveryError with description");
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString()).IsEqualTo("Test delivery error");

            // The source's ReceiptHandle was removed before publishing (ADR 0078 "Bag.Remove(ReceiptHandle)").
            // When the DLQ copy is read back via GetMessageFromDeadLetterQueue the parser assigns a
            // NEW ReceiptHandle for the DLQ subscription, so we cannot assert its absence here.
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Pull — None reason (null)
    // -------------------------------------------------------------------------

    /// <summary>
    /// None reason (null): the DLQ copy carries exactly four metadata keys (no rejectionMessage),
    /// rejectionReason == "None", the source returns MT_NONE, and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_none_reason_on_pull_should_publish_copy_with_four_metadata_keys_to_dlq()
    {
        var provider = new GcpPullMessageGatewayProvider();
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

            var received = channel.Receive(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act — null reason maps to "None"
            var result = channel.Reject(received, reason: null);

            // Assert — Reject returns true
            await Assert.That(result).IsTrue();

            // Assert — source is acked
            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

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

            // Assert — rejectionReason == "None"
            var bag = dlqMessage.Header.Bag;
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo("None");

            // Assert — exactly four keys (no rejectionMessage)
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsFalse().Because("DLQ copy for None reason must NOT carry rejectionMessage");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();

            // Timestamp must be ISO 8601 round-trip
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");

            // The source's ReceiptHandle was removed before publishing. The DLQ reader's parser
            // assigns a NEW ReceiptHandle for the DLQ subscription, so its absence cannot be
            // verified in a round-trip test.
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Pull — Missing receipt handle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Missing receipt handle (ADR 0078): the router still publishes a stamped copy to the DLQ,
    /// and Reject returns true even though the original cannot be settled, which is logged as an Error.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_missing_receipt_handle_on_pull_should_still_publish_to_dlq_and_return_true()
    {
        var provider = new GcpPullMessageGatewayProvider();

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

            // Build a message with no ReceiptHandle (test constructs it; never received from broker)
            var message = new Message(
                new MessageHeader(Id.Create(Guid.NewGuid().ToString()), routingKey, MessageType.MT_COMMAND),
                new MessageBody("missing-handle-body"));
            // Explicitly ensure no ReceiptHandle in bag
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
            // The DLQ reader's parser assigns a new ReceiptHandle, so its absence cannot be verified.
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — DeliveryError
    // -------------------------------------------------------------------------

    /// <summary>
    /// DeliveryError with description on an ordering-enabled subscription: the DLQ copy carries
    /// all five metadata keys, the source returns MT_NONE, and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_delivery_error_on_pull_ordering_should_publish_stamped_copy_to_dlq_and_ack_original()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
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

            var received = channel.Receive(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Ordering test error"));

            // Assert — Reject returns true
            await Assert.That(result).IsTrue();

            // Assert — source is acked
            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

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
            // The DLQ reader's parser assigns a new ReceiptHandle, so its absence cannot be verified.
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — None reason
    // -------------------------------------------------------------------------

    /// <summary>
    /// None reason (null) on an ordering-enabled subscription: the DLQ copy carries exactly four
    /// metadata keys, rejectionReason == "None", the source is acked, and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_none_reason_on_pull_ordering_should_publish_copy_with_four_metadata_keys_to_dlq()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
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

            var received = channel.Receive(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = channel.Reject(received, reason: null);

            // Assert
            await Assert.That(result).IsTrue();

            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

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
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsFalse().Because("None reason must NOT carry rejectionMessage");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsTrue();
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            // The DLQ reader's parser assigns a new ReceiptHandle, so its absence cannot be verified.
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — Missing receipt handle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Missing receipt handle on an ordering-enabled subscription: the DLQ copy is still published
    /// and Reject returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_with_missing_receipt_handle_on_pull_ordering_should_still_publish_to_dlq_and_return_true()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();

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

            var message = new Message(
                new MessageHeader(Id.Create(Guid.NewGuid().ToString()), routingKey, MessageType.MT_COMMAND),
                new MessageBody("missing-handle-body-ordering"));
            message.Header.Bag.Remove("ReceiptHandle");

            using var logContext = TestCorrelator.CreateContext();

            // Act
            var result = channel.Reject(message, new MessageRejectionReason(RejectionReason.DeliveryError, "ordering missing handle test"));

            // Assert
            await Assert.That(result).IsTrue();

            // Assert — an Error naming the message says the original cannot be settled
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
            // The DLQ reader's parser assigns a new ReceiptHandle, so its absence cannot be verified.
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}

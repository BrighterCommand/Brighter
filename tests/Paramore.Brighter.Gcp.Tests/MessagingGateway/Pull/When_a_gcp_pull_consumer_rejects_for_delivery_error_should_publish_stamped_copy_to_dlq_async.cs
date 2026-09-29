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
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-16, R-18, AC-15, NFR-8 — async (Proactor) path.
/// A GCP pull RejectAsync routes a stamped copy to the dead-letter queue, acknowledges the original,
/// and returns true. Covers GCP/Pull and GCP/PullOrdering, both rejection reasons
/// (DeliveryError with description; None with no description), and the missing-receipt-handle
/// defensive path (ADR 0078 "Missing receipt handle (both consumers)").
/// </summary>
[Property("Category", "GcpPubSubPull")]
[NotInParallel("Pull")]
public class GcpPullConsumerRejectDlqRoutingAsyncTests
{
    // -------------------------------------------------------------------------
    // GCP / Pull — DeliveryError (async)
    // -------------------------------------------------------------------------
    /// <summary>
    /// DeliveryError with a description: the DLQ copy carries all five metadata keys, the source
    /// returns MT_NONE after RejectAsync, and RejectAsync returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_delivery_error_on_pull_should_publish_stamped_copy_to_dlq_and_ack_original()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Async delivery error test"));

            // Assert — RejectAsync returns true
            await Assert.That(result).IsTrue();

            // Assert — source is acked
            var reRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

            // Assert — DLQ copy appears within 60s
            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
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
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString()).IsEqualTo("Async delivery error test");
            // DLQ reader parser assigns new ReceiptHandle; absence cannot be verified in round-trip.
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Pull — None reason (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// None reason (null): the DLQ copy carries exactly four metadata keys (no rejectionMessage),
    /// rejectionReason == "None", the source is acked, and RejectAsync returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_none_reason_on_pull_should_publish_copy_with_four_metadata_keys_to_dlq()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act — null reason
            var result = await channel.RejectAsync(received, reason: null);

            // Assert
            await Assert.That(result).IsTrue();

            var reRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
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
            // DLQ reader parser assigns new ReceiptHandle; absence cannot be verified in round-trip.
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Pull — Missing receipt handle (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Missing receipt handle: the DLQ copy is still published, an Error says the original cannot be
    /// settled, and RejectAsync returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_missing_receipt_handle_on_pull_should_still_publish_to_dlq_and_return_true()
    {
        var provider = new GcpPullMessageGatewayProvider();

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
            // Arrange
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
            await Assert.That(result).IsTrue();

            // Assert — an Error naming the message says the original cannot be settled
            var settleError = await Assert.That(System.Linq.Enumerable.Where(TestCorrelator.GetLogEventsFromCurrentContext(), e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(message.Id.Value))).HasSingleItem();
            await Assert.That(settleError.RenderMessage()).Contains("cannot be settled");

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
            // DLQ reader parser assigns new ReceiptHandle; absence cannot be verified in round-trip.
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — DeliveryError (async)
    // -------------------------------------------------------------------------

    [Test]
    public async Task When_rejecting_async_with_delivery_error_on_pull_ordering_should_publish_stamped_copy_to_dlq()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "Async ordering delivery error"));

            // Assert
            await Assert.That(result).IsTrue();

            var reRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var bag = dlqMessage.Header.Bag;
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString()).IsEqualTo(originalTopic);
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.DeliveryError.ToString());
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsTrue();
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            await Assert.That(DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _)).IsTrue().Because($"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsTrue();
            // DLQ reader parser assigns new ReceiptHandle; absence cannot be verified in round-trip.
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — None reason (async)
    // -------------------------------------------------------------------------

    [Test]
    public async Task When_rejecting_async_with_none_reason_on_pull_ordering_should_publish_copy_with_four_metadata_keys_to_dlq()
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

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = await channel.RejectAsync(received, reason: null);

            // Assert
            await Assert.That(result).IsTrue();

            var reRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
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
            // DLQ reader parser assigns new ReceiptHandle; absence cannot be verified in round-trip.
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — Missing receipt handle (async)
    // -------------------------------------------------------------------------

    [Test]
    public async Task When_rejecting_async_with_missing_receipt_handle_on_pull_ordering_should_still_publish_to_dlq_and_return_true()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();

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
            // Arrange
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
            await Assert.That(result).IsTrue();

            // Assert — an Error naming the message says the original cannot be settled
            var settleError = await Assert.That(System.Linq.Enumerable.Where(TestCorrelator.GetLogEventsFromCurrentContext(), e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(message.Id.Value))).HasSingleItem();
            await Assert.That(settleError.RenderMessage()).Contains("cannot be settled");

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
            // DLQ reader parser assigns new ReceiptHandle; absence cannot be verified in round-trip.
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

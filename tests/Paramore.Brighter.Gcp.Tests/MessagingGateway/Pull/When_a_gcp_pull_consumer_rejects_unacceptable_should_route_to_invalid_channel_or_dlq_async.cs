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

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-16, R-18, AC-15, AC-16, NFR-8 — async (Proactor) path.
/// A GCP pull RejectAsync with reason Unacceptable routes a stamped copy to the invalid-message
/// channel when one is configured, falls back to the DLQ when only a DLQ key is configured
/// (AC-16), and — when neither is configured — publishes nowhere while still acknowledging the
/// original and returning true (RoutingOutcome.NoDestination; its Warning log is added in a
/// later task and is not asserted here). Covers GCP/Pull and GCP/PullOrdering.
/// </summary>
[Property("Category", "GcpPubSubPull")]
[NotInParallel("Pull")]
public class GcpPullConsumerRejectRoutingAsyncTests
{
    // -------------------------------------------------------------------------
    // GCP / Pull — Unacceptable routes to .Invalid (async)
    // -------------------------------------------------------------------------
    /// <summary>
    /// Unacceptable with both a DLQ and an invalid-message key configured: the copy goes to
    /// .Invalid (not .DLQ), carrying the same rejection metadata as the DLQ copy in 5.5a.
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_unacceptable_on_pull_should_route_to_invalid_channel()
    {
        var provider = new GcpPullMessageGatewayProvider();
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
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async test unacceptable message"));

            // Assert — RejectAsync returns true
            await Assert.That(result).IsTrue();

            // Assert — source is acked
            var reRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

            // Assert — Invalid copy appears within 60s
            Message invalidMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                invalidMessage = await provider.GetMessageFromInvalidChannelAsync(subscription);
                if (invalidMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
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
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString()).IsEqualTo("Async test unacceptable message");

            // Assert — nothing was sent to the DLQ instead
            var dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
            await Assert.That(dlqMessage.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Pull — Unacceptable with no invalid key falls back to DLQ (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with a DLQ key configured but no invalid key: the copy falls back to .DLQ.
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_unacceptable_on_pull_and_no_invalid_key_should_fall_back_to_dlq()
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

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async unacceptable, DLQ fallback"));

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
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.Unacceptable.ToString());
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Pull — Unacceptable with neither key configured: no destination (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with neither a DLQ nor an invalid key configured: nothing is published, the
    /// original is still acknowledged, and RejectAsync still returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_unacceptable_on_pull_and_no_destinations_should_ack_without_publishing()
    {
        var provider = new GcpPullMessageGatewayProvider();
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
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async unacceptable, no destination"));

            // Assert
            await Assert.That(result).IsTrue();

            var reRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — Unacceptable routes to .Invalid (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with both a DLQ and an invalid-message key
    /// configured: the copy goes to .Invalid (not .DLQ).
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_unacceptable_on_pull_ordering_should_route_to_invalid_channel()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
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
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            var originalTopic = routingKey.Value;

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async ordering test unacceptable"));

            // Assert
            await Assert.That(result).IsTrue();

            var reRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

            Message invalidMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                invalidMessage = await provider.GetMessageFromInvalidChannelAsync(subscription);
                if (invalidMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
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
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString()).IsEqualTo("Async ordering test unacceptable");

            var dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscription);
            await Assert.That(dlqMessage.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — Unacceptable with no invalid key falls back to DLQ (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with a DLQ key but no invalid key: the
    /// copy falls back to .DLQ.
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_unacceptable_on_pull_ordering_and_no_invalid_key_should_fall_back_to_dlq()
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
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async ordering unacceptable, DLQ fallback"));

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
            await Assert.That(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsTrue();
            await Assert.That(bag[RejectionMetadataKeyNames.RejectionReason]?.ToString()).IsEqualTo(RejectionReason.Unacceptable.ToString());
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — Unacceptable with neither key configured: no destination (async)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with neither a DLQ nor an invalid key
    /// configured: nothing is published, the original is still acknowledged, and RejectAsync
    /// still returns true.
    /// </summary>
    [Test]
    public async Task When_rejecting_async_with_unacceptable_on_pull_ordering_and_no_destinations_should_ack_without_publishing()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
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
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            // Act
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Async ordering unacceptable, no destination"));

            // Assert
            await Assert.That(result).IsTrue();

            var reRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reRead.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

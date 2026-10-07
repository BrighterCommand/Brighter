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
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-16, R-18, AC-15, AC-16, NFR-8 — sync (Reactor) path.
/// A GCP pull Reject with reason Unacceptable routes a stamped copy to the invalid-message
/// channel when one is configured, falls back to the DLQ when only a DLQ key is configured
/// (AC-16), and — when neither is configured — publishes nowhere while still acknowledging the
/// original and returning true (RoutingOutcome.NoDestination; its Warning log is added in a
/// later task and is not asserted here). Covers GCP/Pull and GCP/PullOrdering.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullConsumerRejectRoutingTests
{
    // -------------------------------------------------------------------------
    // GCP / Pull — Unacceptable routes to .Invalid
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with both a DLQ and an invalid-message key configured: the copy goes to
    /// .Invalid (not .DLQ), carrying the same rejection metadata as the DLQ copy in 5.5a.
    /// </summary>
    [Fact]
    public void When_rejecting_with_unacceptable_on_pull_should_route_to_invalid_channel()
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
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var originalTopic = routingKey.Value;

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable message"));

            // Assert — Reject returns true
            Assert.True(result);

            // Assert — source is acked: a second Receive returns MT_NONE
            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(MessageType.MT_NONE, reRead.Header.MessageType);

            // Assert — Invalid copy appears within 60s
            Message invalidMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                invalidMessage = provider.GetMessageFromInvalidChannel(subscription);
                if (invalidMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, invalidMessage.Header.MessageType);

            // Assert — five metadata keys present
            var bag = invalidMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic),
                "Invalid copy must carry originalTopic");
            Assert.Equal(originalTopic, bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString());

            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType),
                "Invalid copy must carry originalMessageType");

            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason),
                "Invalid copy must carry rejectionReason");
            Assert.Equal(RejectionReason.Unacceptable.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());

            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp),
                "Invalid copy must carry rejectionTimestamp");
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            Assert.True(
                DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _),
                $"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");

            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage),
                "Invalid copy must carry rejectionMessage for Unacceptable with description");
            Assert.Equal("Test unacceptable message", bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString());

            // Assert — nothing was sent to the DLQ instead
            var dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
            Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Pull — Unacceptable with no invalid key falls back to DLQ (AC-16)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with a DLQ key configured but no invalid key: the copy falls back to .DLQ.
    /// </summary>
    [Fact]
    public void When_rejecting_with_unacceptable_on_pull_and_no_invalid_key_should_fall_back_to_dlq()
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
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable, DLQ fallback"));

            // Assert — Reject returns true
            Assert.True(result);

            // Assert — source is acked
            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(MessageType.MT_NONE, reRead.Header.MessageType);

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

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.Unacceptable.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / Pull — Unacceptable with neither key configured: no destination
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable with neither a DLQ nor an invalid key configured: nothing is published, the
    /// original is still acknowledged, and Reject still returns true (RoutingOutcome.NoDestination).
    /// </summary>
    [Fact]
    public void When_rejecting_with_unacceptable_on_pull_and_no_destinations_should_ack_without_publishing()
    {
        var provider = new GcpPullMessageGatewayProvider();
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
            // Arrange
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable, no destination"));

            // Assert — Reject still returns true
            Assert.True(result);

            // Assert — source is acked: a second Receive returns MT_NONE
            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(MessageType.MT_NONE, reRead.Header.MessageType);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — Unacceptable routes to .Invalid
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with both a DLQ and an invalid-message key
    /// configured: the copy goes to .Invalid (not .DLQ).
    /// </summary>
    [Fact]
    public void When_rejecting_with_unacceptable_on_pull_ordering_should_route_to_invalid_channel()
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
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var originalTopic = routingKey.Value;

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Ordering test unacceptable"));

            // Assert
            Assert.True(result);

            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(MessageType.MT_NONE, reRead.Header.MessageType);

            Message invalidMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                invalidMessage = provider.GetMessageFromInvalidChannel(subscription);
                if (invalidMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
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
            Assert.Equal("Ordering test unacceptable", bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString());

            var dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
            Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — Unacceptable with no invalid key falls back to DLQ
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with a DLQ key but no invalid key: the
    /// copy falls back to .DLQ.
    /// </summary>
    [Fact]
    public void When_rejecting_with_unacceptable_on_pull_ordering_and_no_invalid_key_should_fall_back_to_dlq()
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
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Ordering unacceptable, DLQ fallback"));

            // Assert
            Assert.True(result);

            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(MessageType.MT_NONE, reRead.Header.MessageType);

            Message dlqMessage = new Message();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.Unacceptable.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering — Unacceptable with neither key configured: no destination
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unacceptable on an ordering-enabled subscription with neither a DLQ nor an invalid key
    /// configured: nothing is published, the original is still acknowledged, and Reject still
    /// returns true.
    /// </summary>
    [Fact]
    public void When_rejecting_with_unacceptable_on_pull_ordering_and_no_destinations_should_ack_without_publishing()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
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
            // Arrange
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "Ordering unacceptable, no destination"));

            // Assert
            Assert.True(result);

            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(MessageType.MT_NONE, reRead.Header.MessageType);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}

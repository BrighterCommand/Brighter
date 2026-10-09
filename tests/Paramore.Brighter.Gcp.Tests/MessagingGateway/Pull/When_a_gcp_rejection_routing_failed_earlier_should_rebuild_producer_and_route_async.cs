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

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-19, AC-18, ADR 0078 "Only success is cached" — async (Proactor) path, PullOrdering.
/// A failed GCP routing publish must not leave a poisoned (failed or ordering-key-paused) producer
/// cached: once the destination topic is created, a later RejectAsync on the redelivered message
/// must rebuild the producer and route successfully.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullOrderingConsumerRejectRebuildsProducerAfterFailedRoutingAsyncTests
{
    private const int WindowSeconds = 10;

    [Fact]
    public async Task When_a_gcp_rejection_routing_failed_earlier_should_rebuild_producer_and_route_async()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");

        // Given — provisioning subscription for the source topic: no dead-letter route, so
        // creating it does not eagerly pre-provision the (not-yet-existing) destination.
        var provisioning = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 60,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true);

        // Given — under-test subscription: Assume against the real, not-yet-existing DLQ topic, so
        // the first routing publish fails.
        var underTest = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            requeueDelay: TimeSpan.Zero,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            deadLetterRoutingKey: deadLetterRoutingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange — stand up the source topic/subscription, then attach the under-test channel.
            (await provider.CreateChannelAsync(provisioning)).Dispose();

            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(underTest);

            var message = builder.SetTopic(routingKey)
                .SetPartitionKey(new PartitionKey("gcp-pull-ordering-rebuild-key"))
                .Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act 1 — the first RejectAsync fails: the DLQ topic does not exist yet (Assume).
            var firstResult = await channel.RejectAsync(received,
                new MessageRejectionReason(RejectionReason.DeliveryError, "first attempt - destination absent"));
            Assert.True(firstResult);

            // Now create the destination topic and a reading subscription for it.
            var dlqProvisioning = new GcpPubSubSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(new ChannelName(deadLetterRoutingKey.Value)),
                channelName: new ChannelName(deadLetterRoutingKey.Value),
                routingKey: deadLetterRoutingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Create,
                subscriptionMode: SubscriptionMode.Pull);
            (await provider.CreateChannelAsync(dlqProvisioning)).Dispose();

            // Assert — the original is released (not acked): it is redelivered within W.
            Message redelivered = new Message();
            var pollStopwatch = Stopwatch.StartNew();
            while (pollStopwatch.Elapsed < TimeSpan.FromSeconds(WindowSeconds))
            {
                redelivered = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
                if (redelivered.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(250);
            }

            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            // Act 2 — the redelivered message's RejectAsync must succeed now that the destination
            // exists: this proves the router rebuilt a fresh, unpaused producer since the first
            // failure rather than reusing a cached failed/paused one.
            var secondResult = await channel.RejectAsync(redelivered,
                new MessageRejectionReason(RejectionReason.DeliveryError, "second attempt - destination now exists"));
            Assert.True(secondResult);

            // Assert — the destination received the routed copy, stamped with rejection metadata.
            Message dlqMessage = new Message();
            var dlqStopwatch = Stopwatch.StartNew();
            while (dlqStopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(underTest);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            var bag = dlqMessage.Header.Bag;
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
            Assert.Equal(routingKey.Value, bag[RejectionMetadataKeyNames.OriginalTopic]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
            Assert.Equal(RejectionReason.DeliveryError.ToString(), bag[RejectionMetadataKeyNames.RejectionReason]?.ToString());
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
            var ts = bag[RejectionMetadataKeyNames.RejectionTimestamp]?.ToString() ?? string.Empty;
            Assert.True(
                DateTimeOffset.TryParseExact(ts, "o", null, System.Globalization.DateTimeStyles.None, out _),
                $"rejectionTimestamp must be ISO 8601 'o' format, got: {ts}");
            Assert.True(bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
            Assert.Equal("second attempt - destination now exists", bag[RejectionMetadataKeyNames.RejectionMessage]?.ToString());

            // Assert — the original is now settled: no further redelivery.
            var finalRead = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(MessageType.MT_NONE, finalRead.Header.MessageType);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

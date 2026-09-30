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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;
using StreamOrderingCommand = Paramore.Brighter.Gcp.Tests.MessagingGateway.StreamOrdering.ConformanceDeferredCommand;
using StreamOrderingPump = Paramore.Brighter.Gcp.Tests.MessagingGateway.StreamOrdering.ConformanceDeferredPump;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// Async (Proactor) twin of <see cref="GcpStreamRedeliveryDeliveryCountReactorTests"/> and
/// <see cref="GcpStreamOrderingRedeliveryDeliveryCountReactorTests"/> (NFR-8). Characterises R-1 /
/// R-2 / R-3 / AC-1 / AC-2 on the GCP stream consumer (AC-19 branch, ADR 0077 "R-13 (GCP): the
/// branch rule" — the 6.7 measurement recorded <c>PubsubExtensions.GetDeliveryAttempt()</c> as
/// <c>1, 2, 3</c> on a DLQ-backed subscription): with <c>requeueCount: -1</c> (Brighter's own
/// budget disabled) and a native <c>DeadLetterPolicy</c> (<c>MaxDeliveryAttempts = 5</c>), a
/// deferring pump presents a strictly increasing <c>Header.HandledCount</c> across redeliveries,
/// starting at <c>0</c>.
/// </summary>
/// <summary>Stream (unordered), Proactor variant.</summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamRedeliveryDeliveryCountProactorTests
{
    /// <summary>
    /// With <c>requeueCount: -1</c> and a native <c>DeadLetterPolicy</c> (M = 5), a pump running
    /// over a deferring handler presents a strictly increasing <c>HandledCount</c> on each
    /// redelivery, and the first delivery presents <c>0</c> (R-1, R-2, R-3, AC-1, AC-2).
    /// </summary>
    [Fact]
    public async Task When_a_gcp_stream_message_is_redelivered_should_present_increasing_delivery_count_async()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new GcpStreamMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        // Subscription: budget = -1 (Brighter's own budget disabled), native DeadLetterPolicy with
        // MaxDeliveryAttempts = 5 (the minimum the emulator accepts), so Pub/Sub populates
        // PubsubExtensions.GetDeliveryAttempt() across redeliveries (6.7's measurement).
        var subscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 10,
            requeueCount: -1,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com",
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream,
            subscriberMember: "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com");

        var publication = provider.CreatePublication(routingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            // CreateChannelAsync provisions the main topic/subscription and, because DeadLetter is
            // set, the native DLQ topic/subscription too (EnsureSubscriptionExistsAsync).
            var realChannel = await provider.CreateChannelAsync(subscription);
            // Wrap the channel the provider returns to record each delivery's HandledCount (see
            // RecordingChannelAsync's remarks). GcpPubSubConsumerFactory caches one stream consumer
            // per subscription instance, so a second, separately-wrapped consumer would race the
            // real one for delivery — wrap the channel, not the consumer.
            channel = new RecordingChannelAsync(realChannel, ConformanceDeferredPump.RecordHandledCounts);

            var cmd = new ConformanceDeferredCommand { Value = "stream redelivery delivery count test async" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            await producer.SendAsync(message);

            // Act — run the production pump with no Brighter-side budget; the stream consumer's
            // RequeueAsync Nacks the message (GcpStreamMessage.Reject()), which completes the local
            // reply and frees the client's flow-control slot immediately, so redelivery is prompt.
            // Quit once the handler has been invoked 3 times.
            var pump = ConformanceDeferredPump.CreateProactor(channel, -1, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = ConformanceDeferredPump.KeyOf(message);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60)
                   && ConformanceDeferredPump.GetDispatchCount(key) < 3)
            {
                await Task.Delay(100);
            }

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            // Assert — read the HandledCount sequence after pump quit-and-await
            var counts = ConformanceDeferredPump.GetHandledCountLog(key);
            Assert.True(counts.Count >= 3,
                $"expected at least 3 deliveries but recorded {counts.Count}");

            // R-2 / AC-2: the first delivery presents 0
            Assert.Equal(0, counts[0]);

            // R-1 / AC-1 / R-3: every subsequent delivery presents a strictly greater count
            for (var i = 1; i < counts.Count; i++)
            {
                Assert.True(counts[i] > counts[i - 1],
                    $"R-1: delivery {i + 1} count {counts[i]} must be strictly greater than "
                    + $"delivery {i} count {counts[i - 1]}");
            }
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

/// <summary>StreamOrdering, Proactor variant — same clauses as the unordered Stream configuration.</summary>
[Trait("Category", "GcpPubSubStreamOrdering")]
[Collection("StreamOrdering")]
public class GcpStreamOrderingRedeliveryDeliveryCountProactorTests
{
    /// <summary>
    /// With <c>requeueCount: -1</c> and a native <c>DeadLetterPolicy</c> (M = 5), a pump running
    /// over a deferring handler presents a strictly increasing <c>HandledCount</c> on each
    /// redelivery, and the first delivery presents <c>0</c> (R-1, R-2, R-3, AC-1, AC-2).
    /// </summary>
    [Fact]
    public async Task When_a_gcp_stream_ordering_message_is_redelivered_should_present_increasing_delivery_count_async()
    {
        // Arrange
        StreamOrderingPump.ResetDispatchCount();

        var provider = new GcpStreamOrderingMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        var subscription = new GcpPubSubSubscription<StreamOrderingCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 10,
            requeueCount: -1,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com",
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream,
            enableMessageOrdering: true,
            subscriberMember: "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com");

        var publication = provider.CreatePublication(routingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            var realChannel = await provider.CreateChannelAsync(subscription);
            channel = new RecordingChannelAsync(realChannel, StreamOrderingPump.RecordHandledCounts);

            var cmd = new StreamOrderingCommand { Value = "stream ordering redelivery delivery count test async" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            await producer.SendAsync(message);

            var pump = StreamOrderingPump.CreateProactor(channel, -1, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = StreamOrderingPump.KeyOf(message);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60)
                   && StreamOrderingPump.GetDispatchCount(key) < 3)
            {
                await Task.Delay(100);
            }

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var counts = StreamOrderingPump.GetHandledCountLog(key);
            Assert.True(counts.Count >= 3,
                $"expected at least 3 deliveries but recorded {counts.Count}");

            Assert.Equal(0, counts[0]);

            for (var i = 1; i < counts.Count; i++)
            {
                Assert.True(counts[i] > counts[i - 1],
                    $"R-1: delivery {i + 1} count {counts[i]} must be strictly greater than "
                    + $"delivery {i} count {counts[i - 1]}");
            }
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

/// <summary>
/// Forwards every call to <paramref name="inner"/> unchanged except <c>ReceiveAsync</c>, which
/// also records the <c>Header.HandledCount</c> of each real (<c>MT_COMMAND</c>) message via
/// <paramref name="recordHandledCounts"/> before returning it — filtering out the channel's
/// internal <c>MT_NONE</c> (empty poll) and <c>MT_QUIT</c> sentinels.
/// </summary>
/// <remarks>
/// Wraps the channel rather than the consumer: for Pub/Sub <see cref="SubscriptionMode.Stream"/>,
/// <c>GcpPubSubConsumerFactory</c> caches one consumer per subscription instance, so building a
/// second, separately-wrapped consumer for the same subscription would race the real one for
/// delivery (the same reasoning as <c>RequeueCountingChannelAsync</c>, task 6.6). This is not a
/// mock (C-10) — every call is forwarded to <paramref name="inner"/>; nothing is intercepted or
/// stood in for. Takes a delegate rather than calling a fixed <c>ConformanceDeferredPump</c>
/// because the Stream and StreamOrdering configurations generate distinct
/// <c>ConformanceDeferredPump</c> classes (separate static state, one per namespace).
/// </remarks>
file sealed class RecordingChannelAsync(IAmAChannelAsync inner, Action<Message[]> recordHandledCounts) : IAmAChannelAsync
{
    public ChannelName Name => inner.Name;
    public RoutingKey RoutingKey => inner.RoutingKey;
    public void Enqueue(params Message[] message) => inner.Enqueue(message);
    public void Stop(RoutingKey topic) => inner.Stop(topic);
    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        => inner.AcknowledgeAsync(message, cancellationToken);
    public Task PurgeAsync(CancellationToken cancellationToken = default) => inner.PurgeAsync(cancellationToken);

    public async Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
    {
        var message = await inner.ReceiveAsync(timeout, cancellationToken);
        if (message.Header.MessageType == MessageType.MT_COMMAND)
        {
            recordHandledCounts([message]);
        }

        return message;
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
        CancellationToken cancellationToken = default) => inner.RejectAsync(message, reason, cancellationToken);
    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        => inner.NackAsync(message, cancellationToken);
    public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null,
        CancellationToken cancellationToken = default) => inner.RequeueAsync(message, timeOut, cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    [Obsolete("Use DisposeAsync instead. Sync dispose may not fully clean up async resources.")]
    public void Dispose() => inner.Dispose();
}

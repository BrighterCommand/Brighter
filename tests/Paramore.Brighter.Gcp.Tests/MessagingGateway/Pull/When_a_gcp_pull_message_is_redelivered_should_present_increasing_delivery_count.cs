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
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;
using PullOrderingCommand = Paramore.Brighter.Gcp.Tests.MessagingGateway.PullOrdering.ConformanceDeferredCommand;
using PullOrderingPump = Paramore.Brighter.Gcp.Tests.MessagingGateway.PullOrdering.ConformanceDeferredPump;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// Characterises R-1 / R-2 / R-3 / AC-1 / AC-2 on the GCP pull consumer (AC-19 branch, ADR 0077
/// "R-13 (GCP): the branch rule" — the 6.7 measurement recorded <c>ReceivedMessage.DeliveryAttempt</c>
/// as <c>1, 2, 3</c> on a DLQ-backed subscription): with <c>requeueCount: -1</c> (Brighter's own
/// budget disabled) and a native <c>DeadLetterPolicy</c> (<c>MaxDeliveryAttempts = 5</c>), a
/// deferring pump presents a strictly increasing <c>Header.HandledCount</c> across redeliveries,
/// starting at <c>0</c>.
/// </summary>
/// <summary>Pull (unordered), Reactor variant.</summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullRedeliveryDeliveryCountReactorTests
{
    /// <summary>
    /// With <c>requeueCount: -1</c> and a native <c>DeadLetterPolicy</c> (M = 5), a pump running
    /// over a deferring handler presents a strictly increasing <c>HandledCount</c> on each
    /// redelivery, and the first delivery presents <c>0</c> (R-1, R-2, R-3, AC-1, AC-2).
    /// </summary>
    [Fact]
    public async Task When_a_gcp_pull_message_is_redelivered_should_present_increasing_delivery_count()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new GcpPullMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        // Subscription: budget = -1 (Brighter's own budget disabled), native DeadLetterPolicy with
        // MaxDeliveryAttempts = 5 (the minimum the emulator accepts), so Pub/Sub populates
        // ReceivedMessage.DeliveryAttempt across redeliveries (6.7's measurement).
        var subscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            requeueCount: -1,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com",
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            subscriberMember: "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com");

        var publication = provider.CreatePublication(routingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            // CreateChannel provisions the main topic/subscription and, because DeadLetter is set,
            // the native DLQ topic/subscription too (EnsureSubscriptionExistsAsync).
            var realChannel = provider.CreateChannel(subscription);
            // Wrap the channel the provider returns to record each delivery's HandledCount (see
            // RecordingChannelSync's remarks).
            channel = new RecordingChannelSync(realChannel, ConformanceDeferredPump.RecordHandledCounts);

            var cmd = new ConformanceDeferredCommand { Value = "pull redelivery delivery count test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            producer.Send(message);

            // Act — run the production pump with no Brighter-side budget; the pull consumer's
            // Requeue issues ModifyAckDeadline(..., 0), making the message immediately
            // redeliverable. Quit once the handler has been invoked 3 times.
            var pump = ConformanceDeferredPump.CreateReactor(channel, -1, TimeSpan.FromMilliseconds(5000));
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
            provider.CleanUp(producer, channel, []);
        }
    }
}

/// <summary>PullOrdering, Reactor variant — same clauses as the unordered Pull configuration.</summary>
[Trait("Category", "GcpPubSubPullOrdering")]
[Collection("PullOrdering")]
public class GcpPullOrderingRedeliveryDeliveryCountReactorTests
{
    /// <summary>
    /// With <c>requeueCount: -1</c> and a native <c>DeadLetterPolicy</c> (M = 5), a pump running
    /// over a deferring handler presents a strictly increasing <c>HandledCount</c> on each
    /// redelivery, and the first delivery presents <c>0</c> (R-1, R-2, R-3, AC-1, AC-2).
    /// </summary>
    [Fact]
    public async Task When_a_gcp_pull_ordering_message_is_redelivered_should_present_increasing_delivery_count()
    {
        // Arrange
        PullOrderingPump.ResetDispatchCount();

        var provider = new GcpPullOrderingMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        var subscription = new GcpPubSubSubscription<PullOrderingCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            requeueCount: -1,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com",
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            subscriberMember: "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com");

        var publication = provider.CreatePublication(routingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            var realChannel = provider.CreateChannel(subscription);
            channel = new RecordingChannelSync(realChannel, PullOrderingPump.RecordHandledCounts);

            var cmd = new PullOrderingCommand { Value = "pull ordering redelivery delivery count test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            producer.Send(message);

            var pump = PullOrderingPump.CreateReactor(channel, -1, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = PullOrderingPump.KeyOf(message);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60)
                   && PullOrderingPump.GetDispatchCount(key) < 3)
            {
                await Task.Delay(100);
            }

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var counts = PullOrderingPump.GetHandledCountLog(key);
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
            provider.CleanUp(producer, channel, []);
        }
    }
}

/// <summary>
/// Forwards every call to <paramref name="inner"/> unchanged except <c>Receive</c>, which also
/// records the <c>Header.HandledCount</c> of each real (<c>MT_COMMAND</c>) message via
/// <paramref name="recordHandledCounts"/> before returning it — filtering out the channel's
/// internal <c>MT_NONE</c> (empty poll) and <c>MT_QUIT</c> sentinels.
/// </summary>
/// <remarks>
/// Wraps the channel rather than the consumer because the provider does not expose its
/// connection, so the generated <c>ConformanceDeferredPump.CreateRecordingChannel</c> (which wraps a
/// consumer) cannot be used here; the same approach as <c>RequeueCountingChannelSync</c>, task 6.6. This is not a mock
/// (C-10) — every call is forwarded to <paramref name="inner"/>; nothing is intercepted or stood in
/// for. Takes a delegate rather than calling a fixed <c>ConformanceDeferredPump</c> because the
/// Pull and PullOrdering configurations generate distinct <c>ConformanceDeferredPump</c> classes
/// (separate static state, one per namespace).
/// </remarks>
file sealed class RecordingChannelSync(IAmAChannelSync inner, Action<Message[]> recordHandledCounts) : IAmAChannelSync
{
    public ChannelName Name => inner.Name;
    public RoutingKey RoutingKey => inner.RoutingKey;
    public void Enqueue(params Message[] message) => inner.Enqueue(message);
    public void Stop(RoutingKey topic) => inner.Stop(topic);
    public void Acknowledge(Message message) => inner.Acknowledge(message);
    public void Purge() => inner.Purge();

    public Message Receive(TimeSpan? timeout)
    {
        var message = inner.Receive(timeout);
        if (message.Header.MessageType == MessageType.MT_COMMAND)
        {
            recordHandledCounts([message]);
        }

        return message;
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null) => inner.Reject(message, reason);
    public void Nack(Message message) => inner.Nack(message);
    public bool Requeue(Message message, TimeSpan? timeOut = null) => inner.Requeue(message, timeOut);
    public void Dispose() => inner.Dispose();
}

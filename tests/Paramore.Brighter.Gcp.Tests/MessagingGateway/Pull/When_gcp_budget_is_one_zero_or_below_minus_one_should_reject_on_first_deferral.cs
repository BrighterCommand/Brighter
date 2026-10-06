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

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// Characterises R-7 / AC-6 / AC-35 on the GCP pull consumer: with a budget of <c>1</c>, <c>0</c>,
/// or any value below <c>-1</c> (e.g. <c>-3</c>), <c>DiscardRequeuedMessagesEnabled()</c>
/// (<c>MessagePump.cs:171-174</c>) returns <c>true</c> and <c>Message.HandledCountReached</c>
/// (<c>Message.cs:161-164</c>) is satisfied on the first deferral, so the message is rejected with
/// a <c>DeliveryError</c> reason without ever being requeued.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullBudgetOneZeroOrBelowMinusOneRejectsOnFirstDeferralReactorTests
{
    /// <summary>
    /// With a budget of <paramref name="requeueCount"/>, the handler is invoked exactly once, the
    /// channel's observed <c>Requeue</c> count is 0, and a <c>DeliveryError</c> rejection is issued
    /// placing the message on the Brighter DLQ (R-7, AC-6, AC-35).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task When_gcp_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral(
        int requeueCount)
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new GcpPullMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");

        // Subscription: budget = requeueCount, no native DeadLetterPolicy, with a Brighter DLQ
        // routing key. DiscardRequeuedMessagesEnabled() returns true for all three values, and
        // HandledCountReached fires on the first deferral.
        var subscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            requeueCount: requeueCount,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            deadLetterRoutingKey: dlqRoutingKey);

        var publication = provider.CreatePublication(routingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            // CreateChannel pre-provisions the Brighter DLQ topic/subscription because
            // DeadLetterRoutingKey is set and MakeChannels == Create (ADR 0078 step 5), so there is
            // somewhere to dead-letter to — and somewhere to read from below — even under the RED
            // mutation.
            var realChannel = provider.CreateChannel(subscription);
            // Wrap the real channel (not the consumer) to observe Requeue calls (R-27(c)(6)).
            // Building a second, separately-wrapped consumer for the same subscription would race
            // the real one for delivery — see RequeueCountingChannelSync's remarks.
            channel = new RequeueCountingChannelSync(realChannel);

            var cmd = new ConformanceDeferredCommand { Value = $"budget {requeueCount} rejects on first deferral" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            producer.Send(message);

            // Act — run the pump; correct behaviour rejects immediately on the first deferral.
            // Under mutation (a) (DiscardRequeuedMessagesEnabled returns RequeueCount > 0 instead of
            // RequeueCount != -1), the 0 and -3 rows requeue forever — we bound the run by polling
            // until the DLQ copy arrives or a 30-second window elapses, then sending Quit.
            var pump = ConformanceDeferredPump.CreateReactor(channel, requeueCount, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = ConformanceDeferredPump.KeyOf(message);

            // Poll until the DLQ copy arrives (correct behaviour) or 30 s passes (mutation
            // scenario). GetMessageFromDeadLetterQueue's own 5 s internal receive wait paces the
            // loop, so this is bounded to roughly six attempts.
            var stopwatch = Stopwatch.StartNew();
            Message dlqMessage;
            do
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
            } while (stopwatch.Elapsed < TimeSpan.FromSeconds(30));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            // Assert — R-7 / AC-6 / AC-35

            // Under mutation (b) HandledCountReached uses > instead of >=, so the 1 row requeues
            // repeatedly instead of rejecting; requeue count is > 0 instead of 0. Checked first so
            // this assertion is the one that surfaces under mutation (b).
            var requeueCountActual = ConformanceDeferredPump.GetRequeueCount(key);
            Assert.Equal(0, requeueCountActual);

            // Under mutation (a) the 0 and -3 rows requeue forever, so dispatch count > 1 and the
            // assertion below fails.
            var dispatchCount = ConformanceDeferredPump.GetDispatchCount(key);
            Assert.Equal(1, dispatchCount);

            // A DeliveryError rejection was issued — the DLQ copy must carry rejectionReason.
            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

            Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionReason"),
                "DLQ bag must carry rejectionReason");
            Assert.Equal(RejectionReason.DeliveryError.ToString(),
                dlqMessage.Header.Bag["rejectionReason"].ToString());
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}

/// <summary>
/// Forwards every call to <paramref name="inner"/> unchanged except <c>Requeue</c>, which it also
/// records via <see cref="ConformanceDeferredPump.IncrementRequeueCount"/> before delegating.
/// </summary>
/// <remarks>
/// Wraps the channel rather than the consumer: for Pub/Sub <see cref="SubscriptionMode.Stream"/>,
/// <c>GcpPubSubConsumerFactory</c> caches one consumer per subscription instance, so building a
/// second, separately-wrapped consumer for the same subscription would race the real one for
/// delivery. This is not a mock (C-10) — every call is forwarded to <paramref name="inner"/>;
/// nothing is intercepted or stood in for.
/// </remarks>
file sealed class RequeueCountingChannelSync(IAmAChannelSync inner) : IAmAChannelSync
{
    public ChannelName Name => inner.Name;
    public RoutingKey RoutingKey => inner.RoutingKey;
    public void Enqueue(params Message[] message) => inner.Enqueue(message);
    public void Stop(RoutingKey topic) => inner.Stop(topic);
    public void Acknowledge(Message message) => inner.Acknowledge(message);
    public void Purge() => inner.Purge();
    public Message Receive(TimeSpan? timeout) => inner.Receive(timeout);
    public bool Reject(Message message, MessageRejectionReason? reason = null) => inner.Reject(message, reason);
    public void Nack(Message message) => inner.Nack(message);

    public bool Requeue(Message message, TimeSpan? timeOut = null)
    {
        ConformanceDeferredPump.IncrementRequeueCount(message);
        return inner.Requeue(message, timeOut);
    }

    public void Dispose() => inner.Dispose();
}

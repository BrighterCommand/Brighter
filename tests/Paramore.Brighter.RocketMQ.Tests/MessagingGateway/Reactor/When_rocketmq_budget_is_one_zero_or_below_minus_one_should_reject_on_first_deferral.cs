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
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

/// <summary>
/// Task 7.6 CHARACTERISE — R-7, AC-6, AC-35. With a budget of <c>1</c>, <c>0</c>, or any value
/// below <c>-1</c> (e.g. <c>-3</c>), the pump's <c>DiscardRequeuedMessagesEnabled()</c> guard
/// (<c>MessagePump.cs:171-174</c>) returns <c>true</c> and <c>Message.HandledCountReached</c>
/// (<c>Message.cs:161-164</c>) is satisfied on the first deferral, so the message is rejected with
/// a <c>DeliveryError</c> reason without ever being requeued. Transport-neutral guard, so it holds
/// on RocketMQ exactly as it does on every other transport (see the GCP twin,
/// <c>GcpPullBudgetOneZeroOrBelowMinusOneRejectsOnFirstDeferralReactorTests</c>, task 6.6). This
/// test drives the production pump (<c>ConformanceDeferredPump</c>), not a harness stand-in for
/// the rule under test.
/// </summary>
/// <remarks>
/// <para>
/// When this test was written, Brighter's RocketMQ <c>Requeue</c> was a broker no-op and redelivery
/// came only when the subscription's invisibility lease lapsed; the timings described here assume that.
/// Since bugfix 0025 <c>Requeue</c> sets the invisible duration on the broker, so redelivery comes
/// sooner. The assertions do not depend on the lease timing.
/// Under mutation (a)
/// (<c>DiscardRequeuedMessagesEnabled</c> returns <c>RequeueCount &gt; 0</c> instead of
/// <c>RequeueCount != -1</c>), the <c>0</c> and <c>-3</c> rows requeue instead of rejecting — but
/// the requeue itself is observed immediately by the recording consumer's <c>Requeue</c> call, so
/// the requeue-count assertion fails without waiting for the 10 s invisibility lease to lapse and
/// redeliver.
/// </para>
/// <para>
/// Uses the pre-created <c>rmq_budget_first_r</c> / <c>rmq_budget_first_r_DLQ</c> topics (the C#
/// client never auto-creates one) with a fresh consumer group per run. Both topics may carry
/// copies left by earlier runs — a fresh consumer group can be handed the oldest message on the
/// topic — so the DLQ check below reads in a bounded loop and matches on this run's message id,
/// acknowledging anything else, rather than trusting the first message a read returns.
/// </para>
/// </remarks>
[Trait("Category", "RocketMQ")]
// Shares ConformanceDeferredPump's static dispatch counter with the generated pump tests, so it runs
// in their collection: a ResetDispatchCount from a parallel test would wipe this test's count.
[Collection("RocketMQMessagingGateway")]
public class RocketMqBudgetOneZeroOrBelowMinusOneRejectsOnFirstDeferralReactorTests
{
    private const string Topic = "rmq_budget_first_r";
    private const string DlqTopic = "rmq_budget_first_r_DLQ";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_dlqReadCeiling = TimeSpan.FromSeconds(30);

    /// <summary>
    /// With a budget of <paramref name="requeueCount"/>, the handler is invoked exactly once, the
    /// recording consumer's observed <c>Requeue</c> count is 0, and a <c>DeliveryError</c>
    /// rejection is issued placing the message on the Brighter DLQ (R-7, AC-6, AC-35).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task When_rocketmq_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral(
        int requeueCount)
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new RocketMqMessageGatewayProvider();
        var routingKey = new RoutingKey(Topic);
        var dlqRoutingKey = new RoutingKey(DlqTopic);

        var publication = provider.CreatePublication(routingKey);

        var subscriptionName = new SubscriptionName($"budget-first-{Guid.NewGuid():N}");
        var consumerGroup = $"budget-first-{Guid.NewGuid():N}";

        // Subscription: budget = requeueCount, with a Brighter DLQ routing key.
        // DiscardRequeuedMessagesEnabled() returns true for all three values, and
        // HandledCountReached fires on the first deferral.
        var subscription = new RocketMqSubscription<ConformanceDeferredCommand>(
            subscriptionName: subscriptionName,
            channelName: provider.GetOrCreateChannelName(),
            routingKey: routingKey,
            consumerGroup: consumerGroup,
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: requeueCount,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);

            // Build the real consumer via the production factory, then wrap it in the recording
            // consumer (task 3.3's harness) to observe Requeue calls without re-implementing the
            // delivery-budget rule under test (that rule lives in Reactor/Proactor, not here).
            var innerConsumer = new RocketMessageConsumerFactory(GatewayFactory.CreateConnection())
                .Create(subscription);
            channel = ConformanceDeferredPump.CreateRecordingChannel(
                subscription.ChannelName, routingKey, innerConsumer);

            var cmd = new ConformanceDeferredCommand { Value = $"budget {requeueCount} rejects on first deferral" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            producer.Send(message);

            // Act — run the pump; correct behaviour rejects immediately on the first deferral.
            // Under mutation (a) (DiscardRequeuedMessagesEnabled returns RequeueCount > 0 instead of
            // RequeueCount != -1), the 0 and -3 rows requeue indefinitely and never reach the DLQ —
            // bound the run by polling until the DLQ copy arrives or a 30 s window elapses, then send Quit.
            var pump = ConformanceDeferredPump.CreateReactor(channel, requeueCount, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = ConformanceDeferredPump.KeyOf(message);

            var dlqMessage = await PollDeadLetterQueueAsync(cmd.Id);

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            // Assert — R-7 / AC-6 / AC-35

            // Under mutation (b) HandledCountReached uses > instead of >=, so the 1 row requeues
            // once instead of rejecting on the first deferral; requeue count is > 0 instead of 0.
            // Checked first so this assertion is the one that surfaces under mutation (b).
            var requeueCountActual = ConformanceDeferredPump.GetRequeueCount(key);
            Assert.Equal(0, requeueCountActual);

            // Under mutation (a) the 0 and -3 rows requeue instead of rejecting, and the requeue
            // redelivers them within the 30 s poll window, so the dispatch count exceeds 1.
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

    /// <summary>
    /// Reads <see cref="DlqTopic"/> with a fresh consumer group, polling for up to
    /// <see cref="s_dlqReadCeiling"/>, acknowledging and skipping any message that is not
    /// <paramref name="messageId"/> (a copy left by an earlier run), and returns the first
    /// matching copy, or <see cref="Message.Empty"/> if the ceiling elapses first.
    /// </summary>
    private static async Task<Message> PollDeadLetterQueueAsync(Id messageId)
    {
        var connection = GatewayFactory.CreateConnection();
        await using var simpleConsumer = await GatewayFactory.CreateSimpleConsumer(connection, DlqTopic);
        await using var dlqConsumer = new RocketMessageConsumer(simpleConsumer, 1, TimeSpan.FromSeconds(30));

        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < s_dlqReadCeiling)
        {
            foreach (var received in await dlqConsumer.ReceiveAsync(s_receiveMessageTimeout))
            {
                if (received.Header.MessageType == MessageType.MT_NONE)
                    continue;

                await dlqConsumer.AcknowledgeAsync(received);

                if (received.Id == messageId)
                    return received;
            }
        }

        return Message.Empty;
    }
}

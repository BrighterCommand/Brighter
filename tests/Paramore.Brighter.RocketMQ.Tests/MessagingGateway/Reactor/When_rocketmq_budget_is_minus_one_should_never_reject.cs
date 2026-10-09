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
/// Task 7.5 CHARACTERISE — R-6, AC-5, the "-1" clause of AC-35. With <c>requeueCount: -1</c> the
/// pump's <c>DiscardRequeuedMessagesEnabled()</c> guard (<c>MessagePump.cs:171</c>) returns
/// <c>false</c>, so <c>HandledCountReached</c> (<c>Message.cs:161</c>) is never evaluated and a
/// deferred message is requeued indefinitely rather than rejected. The guard is transport-neutral,
/// so it holds on RocketMQ exactly as it does on every other transport (see the GCP twin,
/// <c>GcpPullBudgetMinusOneNeverRejectsReactorTests</c>). This test drives the production pump
/// (<c>ConformanceDeferredPump</c>), not a harness stand-in for the rule.
/// </summary>
/// <remarks>
/// <para>
/// When this test was written, Brighter's RocketMQ <c>Requeue</c> was a broker no-op and redelivery
/// came only when the subscription's invisibility lease lapsed; the timings described here assume that.
/// Since bugfix 0025 <c>Requeue</c> sets the invisible duration on the broker, so redelivery comes
/// sooner. The assertions do not depend on the lease timing.
/// The default
/// <c>InvisibilityTimeout</c> (30 s) would give only ~2 deliveries in a 60 s run, so this test uses
/// 10 s (as task 7.1's measurement did) to get ~5-6 deliveries in 60 s. RocketMQ's own consumer-group
/// max-retry (default 16) is nowhere near reached by that many deliveries, so no broker-level
/// dead-lettering to its own <c>%DLQ%</c> topic happens either — that topic is not Brighter's DLQ
/// and is not asserted on here.
/// </para>
/// <para>
/// Uses the pre-created <c>rmq_budget_m1_r</c> / <c>rmq_budget_m1_r_DLQ</c> topics (the C# client
/// never auto-creates one) with a fresh consumer group per run. Both topics may carry copies left by
/// earlier runs — a fresh consumer group can be handed the oldest message on the topic — so the DLQ
/// check below reads in a bounded loop and matches on this run's message id, acknowledging anything
/// else, rather than trusting the first message a read returns.
/// </para>
/// </remarks>
[Trait("Category", "RocketMQ")]
// Shares ConformanceDeferredPump's static dispatch counter with the generated pump tests, so it runs
// in their collection: a ResetDispatchCount from a parallel test would wipe this test's count.
[Collection("RocketMQMessagingGateway")]
public class RocketMqBudgetMinusOneNeverRejectsReactorTests
{
    private const string Topic = "rmq_budget_m1_r";
    private const string DlqTopic = "rmq_budget_m1_r_DLQ";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_dlqReadCeiling = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_rocketmq_budget_is_minus_one_should_never_reject()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new RocketMqMessageGatewayProvider();
        var routingKey = new RoutingKey(Topic);
        var dlqRoutingKey = new RoutingKey(DlqTopic);

        var publication = provider.CreatePublication(routingKey);

        // Subscription: budget = -1 (disabled), with a Brighter DLQ routing key.
        // DiscardRequeuedMessagesEnabled() returns false, so HandledCountReached is never called
        // and the message is requeued indefinitely. A fresh consumer group avoids picking up any
        // message an earlier run left on the topic.
        var subscription = new RocketMqSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"budget-m1-{Guid.NewGuid():N}"),
            channelName: provider.GetOrCreateChannelName(),
            routingKey: routingKey,
            consumerGroup: $"budget-m1-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: -1,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var cmd = new ConformanceDeferredCommand { Value = "budget minus one never rejects test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            producer.Send(message);

            // Act — run the pump with budget -1; pump for 60 s, then quit and await it. The
            // message redelivers within seconds of each requeue, so the dispatch
            // count will exceed 3. Under the RED mutation (DiscardRequeuedMessagesEnabled
            // returns true), HandledCountReached(-1) is true on the first deferral (HandledCount
            // 0 >= -1), so dispatch count stays at 1 and the message is rejected to the DLQ instead
            // — the test then fails on the "dispatch count > 3" assertion.
            var pump = ConformanceDeferredPump.CreateReactor(channel, -1, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            await Task.Delay(TimeSpan.FromSeconds(60));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var key = ConformanceDeferredPump.KeyOf(message);
            var dispatchCount = ConformanceDeferredPump.GetDispatchCount(key);

            // Assert — R-6 / AC-5: handler invoked more than 3 times with budget -1
            Assert.True(dispatchCount > 3,
                $"Expected dispatch count > 3 after 60 s with requeueCount: -1, but was {dispatchCount}");

            // No DeliveryError rejection was issued — our message must not be on the Brighter DLQ.
            Assert.False(await IsOnDeadLetterQueueAsync(cmd.Id),
                "Expected the message not to reach the Brighter DLQ with requeueCount: -1");
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    /// <summary>
    /// Reads <see cref="DlqTopic"/> with a fresh consumer group for up to <see cref="s_dlqReadCeiling"/>,
    /// acknowledging and skipping any message that is not <paramref name="messageId"/> (a copy left
    /// by an earlier run), and returns whether a message with that id arrived.
    /// </summary>
    private static async Task<bool> IsOnDeadLetterQueueAsync(Id messageId)
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
                    return true;
            }
        }

        return false;
    }
}

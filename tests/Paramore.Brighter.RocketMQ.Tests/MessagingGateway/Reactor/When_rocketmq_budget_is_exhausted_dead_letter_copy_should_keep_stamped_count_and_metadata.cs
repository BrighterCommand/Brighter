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
/// Task 7.12 CHARACTERISE — R-5, R-28, AC-4, AC-41. The RocketMQ dead-letter copy of a
/// budget-exhausted message, read through a real channel built by <see cref="RocketMqChannelFactory"/>
/// over its own reading subscription on the DLQ topic (not a raw consumer), carries the stamped
/// <see cref="MessageHeader.HandledCount"/> and the full rejection metadata. Tasks 7.3 (the
/// publisher's bag loop skips header-owned keys so a stale bag entry cannot overwrite the resolved
/// count) and 7.10 (<see cref="DeliveryCount.Resolve"/> keeps the stamped header count for a routed
/// rejection copy, discriminated by the <c>rejectionReason</c> bag key) already deliver this
/// behaviour, so this test is expected green on arrival. This test drives the production pump
/// (<see cref="ConformanceDeferredPump"/>), not a harness stand-in for the rule under test.
/// </summary>
/// <remarks>
/// <para>
/// RocketMQ's <c>MessageView.DeliveryAttempt</c> is classified APPROXIMATE, not EXACT (task 7.1), so
/// the stamped count is asserted as <c>&gt;= 3</c> (and not <c>0</c>), not exactly <c>3</c>.
/// </para>
/// <para>
/// When this test was written, Brighter's RocketMQ <c>Requeue</c> was a broker no-op and redelivery
/// came only when the subscription's invisibility lease lapsed; the timings described here assume that.
/// Since bugfix 0025 <c>Requeue</c> sets the invisible duration on the broker, so redelivery comes
/// sooner. The assertions do not depend on the lease timing.
/// With a 10 s
/// invisibility timeout, exhausting a budget of 3 needs three deliveries, roughly 20-30 s apart; the
/// wait below is bounded at 90 s.
/// </para>
/// <para>
/// Uses the pre-created <c>rmq_dlq_count_r</c> / <c>rmq_dlq_count_r_DLQ</c> topics (the C# client
/// never auto-creates one), with a fresh consumer group per run on both the main subscription and the
/// DLQ reading subscription. Both topics may carry copies left by earlier runs — a fresh consumer
/// group can be handed the oldest message on the topic — so the DLQ read below polls in a bounded
/// loop and matches this run's message by id, acknowledging anything else, rather than trusting the
/// first message a read returns.
/// </para>
/// </remarks>
[Trait("Category", "RocketMQ")]
// Shares ConformanceDeferredPump's static dispatch/requeue/handled-count state with the generated
// pump tests, so it runs in their collection: a ResetDispatchCount from a parallel test would wipe
// this test's recorded state.
[Collection("RocketMQMessagingGateway")]
public class RocketMqBudgetExhaustedDlqHandledCountReactorTests
{
    private const string Topic = "rmq_dlq_count_r";
    private const string DlqTopic = "rmq_dlq_count_r_DLQ";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_dlqReadCeiling = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task When_rocketmq_budget_is_exhausted_dead_letter_copy_should_keep_stamped_count_and_metadata()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new RocketMqMessageGatewayProvider();
        var routingKey = new RoutingKey(Topic);
        var dlqRoutingKey = new RoutingKey(DlqTopic);
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);

        // Main subscription: Brighter budget 3, with a Brighter DLQ routing key so Reject() has a
        // destination once the budget is exhausted.
        var subscription = new RocketMqSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"dlq-count-{Guid.NewGuid():N}"),
            channelName: channelName,
            routingKey: routingKey,
            consumerGroup: $"dlq-count-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: 3,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        IAmAChannelSync? readingChannel = null;

        try
        {
            producer = provider.CreateProducer(publication);

            // Build the real consumer via the production factory, then wrap it in the recording
            // decorator (task 3.3's harness) purely to track dispatch counts via
            // ConformanceDeferredPump's static state — the delivery-budget rule under test lives in
            // Reactor, not here.
            var innerConsumer = new RocketMessageConsumerFactory(GatewayFactory.CreateConnection())
                .Create(subscription);
            channel = ConformanceDeferredPump.CreateRecordingChannel(channelName, routingKey, innerConsumer);

            var cmd = new ConformanceDeferredCommand { Value = "rocketmq dlq budget exhaustion metadata test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            producer.Send(message);

            // Act — run the pump with budget 3; wait until 3 dispatches (budget exhausted, DLQ sent).
            var pump = ConformanceDeferredPump.CreateReactor(channel, 3, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = ConformanceDeferredPump.KeyOf(message);
            var dispatchStopwatch = Stopwatch.StartNew();
            while (dispatchStopwatch.Elapsed < TimeSpan.FromSeconds(90)
                   && ConformanceDeferredPump.GetDispatchCount(key) < 3)
            {
                await Task.Delay(100);
            }

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            // Assert — read the dead-letter copy through a channel built by RocketMqChannelFactory
            // over its own reading subscription on the DLQ topic (R-28 / AC-41), matching this run's
            // message by id and acknowledging anything else.
            var readerChannelFactory = new RocketMqChannelFactory(
                new RocketMessageConsumerFactory(GatewayFactory.CreateConnection()));

            var readingSubscription = new RocketMqSubscription<ConformanceDeferredCommand>(
                subscriptionName: new SubscriptionName($"dlq-count-reader-{Guid.NewGuid():N}"),
                channelName: new ChannelName($"dlq-count-reader-{Guid.NewGuid():N}"),
                routingKey: dlqRoutingKey,
                consumerGroup: $"dlq-count-reader-{Guid.NewGuid():N}",
                messagePumpType: MessagePumpType.Reactor,
                receiveMessageTimeout: s_receiveMessageTimeout,
                invisibilityTimeout: TimeSpan.FromSeconds(30));

            readingChannel = readerChannelFactory.CreateSyncChannel(readingSubscription);

            var dlqMessage = Message.Empty;
            var receiveStopwatch = Stopwatch.StartNew();
            while (receiveStopwatch.Elapsed < s_dlqReadCeiling)
            {
                var received = readingChannel.Receive(s_receiveMessageTimeout);
                if (received.Header.MessageType == MessageType.MT_NONE)
                    continue;

                readingChannel.Acknowledge(received);

                if (received.Id == cmd.Id)
                {
                    dlqMessage = received;
                    break;
                }
            }

            Assert.Equal(MessageType.MT_COMMAND, dlqMessage.Header.MessageType);

            // AC-4 / R-5: the five rejection metadata keys. Checked before HandledCount below —
            // neither named mutation touches these keys (both (a) and (b) affect only HandledCount),
            // so this order does not mask either mutation; it confirms the metadata survives the
            // round trip before the count assertion is reached.
            Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionReason"),
                "bag must carry rejectionReason");
            Assert.Equal(RejectionReason.DeliveryError.ToString(),
                dlqMessage.Header.Bag["rejectionReason"].ToString());

            Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionMessage"),
                "bag must carry rejectionMessage");
            Assert.False(
                string.IsNullOrWhiteSpace(dlqMessage.Header.Bag["rejectionMessage"].ToString()),
                "rejectionMessage must be non-empty");

            Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp"),
                "bag must carry rejectionTimestamp");
            Assert.True(
                DateTimeOffset.TryParse(dlqMessage.Header.Bag["rejectionTimestamp"].ToString(), out _),
                $"rejectionTimestamp must be ISO-8601 parseable but was "
                + $"'{dlqMessage.Header.Bag["rejectionTimestamp"]}'");

            Assert.True(dlqMessage.Header.Bag.ContainsKey("originalTopic"),
                "bag must carry originalTopic");
            Assert.Equal(routingKey.Value, dlqMessage.Header.Bag["originalTopic"].ToString());

            Assert.True(dlqMessage.Header.Bag.ContainsKey("originalMessageType"),
                "bag must carry originalMessageType");
            Assert.Equal(MessageType.MT_COMMAND.ToString(),
                dlqMessage.Header.Bag["originalMessageType"].ToString());

            // R-28 / AC-41: HandledCount must be >= 3 (the stamped count) and NOT 0 (the DLQ copy's
            // own normalised counter). Both named mutations surface here:
            // (a) DeliveryCount.Resolve ignoring the rejectionReason discriminator presents the DLQ
            //     copy's own normalised count (0) instead of the stamped header count;
            // (b) the publisher's bag loop no longer skipping header-owned keys lets the stale bag
            //     HandledCount = "0" win (last-write-wins) over the correct header-owned property.
            Assert.True(dlqMessage.Header.HandledCount >= 3,
                $"R-28: HandledCount must be >= 3 but was {dlqMessage.Header.HandledCount}");
            Assert.NotEqual(0, dlqMessage.Header.HandledCount);
        }
        finally
        {
            readingChannel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }
}

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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Api.Gax;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// Async (Proactor) twin of <see cref="GcpPullBudgetExhaustedDlqTests"/>. Characterises that the
/// GCP dead-letter copy of a budget-exhausted message, read through a real channel over a reading
/// subscription that carries its own <see cref="DeadLetterPolicy"/>, carries the stamped
/// <see cref="MessageHeader.HandledCount"/> and the full rejection metadata
/// (R-5, R-28, AC-4, AC-41) — Pull, Proactor variant.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullBudgetExhaustedDlqProactorTests
{
    /// <summary>
    /// A pump running over a deferring handler with <c>requeueCount: 3</c>, on a subscription that
    /// also carries a native <see cref="DeadLetterPolicy"/> (M = 5) to <c>.native</c>, sends the
    /// message to the Brighter DLQ once the budget is exhausted. The DLQ copy is read through a
    /// <see cref="GcpPubSubChannelFactory"/> channel over a reading subscription that carries its
    /// own <see cref="DeadLetterPolicy"/> (to a further topic), so the DLQ's own delivery counter is
    /// populated and the read genuinely exercises R-28's "not the destination's own counter" half.
    /// </summary>
    [Fact]
    public async Task When_gcp_budget_is_exhausted_dead_letter_copy_should_keep_stamped_count_and_metadata_async()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new GcpPullMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var dlqChannelName = new ChannelName($"{routingKey.Value}.DLQ");
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        // Reading subscription's own further policy topic — distinct from the main subscription's
        // native DLQ, so the reading subscription's DeadLetterPolicy has somewhere to point.
        var readerFurtherTopic = new RoutingKey($"{dlqRoutingKey.Value}.native");
        var readerFurtherChannel = new ChannelName($"{dlqRoutingKey.Value}.native");
        // Distinct subscription id from the one CreateChannelAsync below pre-provisions on the
        // Brighter DLQ topic (named == dlqChannelName, ADR 0078 step 5) — see CAUTION in the task notes.
        var readingChannelName = new ChannelName($"{dlqChannelName.Value}-RdrA");

        const string serviceAccount = "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com";

        // Main subscription: Brighter budget 3, plus a native DeadLetterPolicy (M = 5) on .native
        // (per the task), and a Brighter DLQ routing key so RejectAsync() has a Brighter destination.
        var subscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 10,
            requeueCount: 3,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = serviceAccount,
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            subscriberMember: serviceAccount,
            deadLetterRoutingKey: dlqRoutingKey);

        var publication = provider.CreatePublication(routingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;
        GcpPubSubChannelFactory? readerChannelFactory = null;
        IAmAChannelAsync? readingChannel = null;
        GcpPubSubSubscription? readingSubscription = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            // CreateChannelAsync pre-provisions the Brighter DLQ topic/subscription because
            // DeadLetterRoutingKey is set and MakeChannels == Create (ADR 0078 step 5), and the main
            // subscription's own native DeadLetterPolicy pre-provisions .native as part of ensuring
            // the main subscription exists.
            channel = await provider.CreateChannelAsync(subscription);

            // Build the policy-carrying reading subscription BEFORE the message is published and
            // pumped — Pub/Sub delivers only messages published after a subscription exists.
            var readerConnection = new GcpMessagingGatewayConnection
            {
                Credential = GatewayFactory.GetCredential(),
                ProjectId = GatewayFactory.GetProjectId(),
                TopicManagerConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
                PublisherConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
                SubscriptionManagerConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
            };
            readerChannelFactory = new GcpPubSubChannelFactory(readerConnection);

            readingSubscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
                subscriptionName: new SubscriptionName(readingChannelName),
                channelName: readingChannelName,
                routingKey: dlqRoutingKey,
                messagePumpType: MessagePumpType.Proactor,
                ackDeadlineSeconds: 60,
                deadLetter: new DeadLetterPolicy(readerFurtherTopic, readerFurtherChannel)
                {
                    AckDeadlineSeconds = 60,
                    MaxDeliveryAttempts = 5,
                    PublisherMember = serviceAccount,
                },
                makeChannels: OnMissingChannel.Create,
                subscriptionMode: SubscriptionMode.Pull,
                subscriberMember: serviceAccount);

            using (var logContext = TestCorrelator.CreateContext())
            {
                readingChannel = await readerChannelFactory.CreateAsyncChannelAsync(readingSubscription);

                // 5.3: channel creation for a DeadLetterPolicy-carrying subscription logs exactly
                // two tolerated IAM Warnings (one per helper) on the emulator.
                var warnings = TestCorrelator.GetLogEventsFromCurrentContext()
                    .Where(e => e.Level == LogEventLevel.Warning)
                    .ToList();
                Assert.Equal(2, warnings.Count);
            }

            var cmd = new ConformanceDeferredCommand { Value = "pull budget exhaustion dlq metadata test (async)" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            await producer.SendAsync(message);

            // Act — run the pump with budget 3; wait until 3 dispatches (budget exhausted, DLQ sent)
            var pump = ConformanceDeferredPump.CreateProactor(channel, 3, TimeSpan.FromMilliseconds(5000));
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

            // Assert — read the dead-letter copy via the policy-carrying reading channel (AC-41 / R-28)
            Message dlqMessage;
            var receiveStopwatch = Stopwatch.StartNew();
            do
            {
                dlqMessage = await readingChannel.ReceiveAsync(TimeSpan.FromSeconds(5));
                if (dlqMessage.Header.MessageType == MessageType.MT_COMMAND)
                {
                    break;
                }
            } while (receiveStopwatch.Elapsed < TimeSpan.FromSeconds(30));

            Assert.Equal(MessageType.MT_COMMAND, dlqMessage.Header.MessageType);
            await readingChannel.AcknowledgeAsync(dlqMessage);

            // AC-4 / R-5: rejection reason must be DeliveryError
            Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionReason"),
                "bag must carry rejectionReason");
            Assert.Equal(RejectionReason.DeliveryError.ToString(),
                dlqMessage.Header.Bag["rejectionReason"].ToString());

            // AC-4 / R-5: rejection message must be non-empty
            Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionMessage"),
                "bag must carry rejectionMessage");
            Assert.False(
                string.IsNullOrWhiteSpace(dlqMessage.Header.Bag["rejectionMessage"].ToString()),
                "rejectionMessage must be non-empty");

            // AC-4 / R-5: rejection timestamp must be ISO-8601 parseable
            Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp"),
                "bag must carry rejectionTimestamp");
            Assert.True(
                DateTimeOffset.TryParse(dlqMessage.Header.Bag["rejectionTimestamp"].ToString(), out _),
                $"rejectionTimestamp must be ISO-8601 parseable but was "
                + $"'{dlqMessage.Header.Bag["rejectionTimestamp"]}'");

            // AC-4 / R-5: original topic must match the source topic
            Assert.True(dlqMessage.Header.Bag.ContainsKey("originalTopic"),
                "bag must carry originalTopic");
            Assert.Equal(routingKey.Value, dlqMessage.Header.Bag["originalTopic"].ToString());

            // AC-4 / R-5: original message type must be present
            Assert.True(dlqMessage.Header.Bag.ContainsKey("originalMessageType"),
                "bag must carry originalMessageType");
            Assert.Equal(MessageType.MT_COMMAND.ToString(),
                dlqMessage.Header.Bag["originalMessageType"].ToString());

            // AC-41 / R-28: HandledCount must be >= 3 (stamped) and NOT 0 (the DLQ's own normalised
            // counter, which is populated because the reading subscription carries its own
            // DeadLetterPolicy). Checked after the AC-4 keys: without the stamping the keys fail
            // first; with the keys intact, a discriminator mutation fails here.
            Assert.True(dlqMessage.Header.HandledCount >= 3,
                $"R-28: HandledCount must be >= 3 but was {dlqMessage.Header.HandledCount}");
            Assert.NotEqual(0, dlqMessage.Header.HandledCount);
        }
        finally
        {
            if (readingChannel != null)
            {
                await readingChannel.DisposeAsync();
            }

            if (readerChannelFactory != null && readingSubscription != null)
            {
                await readerChannelFactory.DeleteTopicAsync(readingSubscription);
                await readerChannelFactory.DeleteSubscriptionAsync(readingSubscription);
            }

            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

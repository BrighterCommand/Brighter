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
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;
using Xunit;
using PullOrderingCommand = Paramore.Brighter.Gcp.Tests.MessagingGateway.PullOrdering.ConformanceDeferredCommand;
using PullOrderingPump = Paramore.Brighter.Gcp.Tests.MessagingGateway.PullOrdering.ConformanceDeferredPump;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// CHARACTERISE (task 6.30, R-19, R-4, R-9, A-6, AC-43) — sync (Reactor), Pull (unordered).
/// </summary>
/// <remarks>
/// <para>
/// The two-subscription Given from 5.8 (<c>GcpPullMessageGatewayProvider.CreateFailedRoutingGiven</c>
/// widened inline): a provisioning subscription (<see cref="OnMissingChannel.Create"/>) that stands up
/// the source topic/subscription and a native <see cref="DeadLetterPolicy"/> (<c>MaxDeliveryAttempts
/// = 5</c>) with its own reading subscription, created before anything is published (Pub/Sub only
/// delivers to subscriptions that exist at publish time); and the subscription under test
/// (<see cref="OnMissingChannel.Assume"/>, <c>requeueCount: 3</c>, <c>requeueDelay: TimeSpan.Zero</c>)
/// carrying a Brighter <c>deadLetterRoutingKey</c> whose topic is never created, so every routing
/// publish fails (ADR 0078 "The destination producer's makeChannels").
/// </para>
/// <para>
/// On the AC-19 branch the handler always defers (<see cref="ConformanceDeferredCommandHandler"/>):
/// once Brighter's own budget (3) is spent, the Reactor rejects with
/// <see cref="RejectionReason.DeliveryError"/> (<c>Reactor.cs</c> <c>RequeueMessage</c>) on every
/// subsequent delivery, since <c>HandledCount</c> only grows. Each <c>Reject</c> fails to route (the
/// destination never exists) and releases the original instead of acknowledging it (R-19), so the
/// pump keeps dispatching the handler and keeps rejecting, outliving the rejection that first fired.
/// Independently of Brighter, Pub/Sub's own native <see cref="DeadLetterPolicy"/> counts each
/// redelivery and, once its own cap (M = 5) is reached, forwards the message to the policy topic and
/// stops redelivering it on the source subscription — with none of Brighter's rejection metadata,
/// because that copy never went through <c>GcpRejectionRouter</c> (R-9).
/// </para>
/// <para>
/// This is also the A-6 risk measurement: if the policy topic never receives the message within the
/// 60 s ceiling but the invocation count has already passed M, A-6 (the emulator enforcing
/// <c>MaxDeliveryAttempts</c>) is refuted for this configuration — recorded by the orchestrator in
/// <c>conformance-status.md</c>, not by this test. The test still asserts the Error, the absent
/// <c>deadLetterRoutingKey</c> topic, and the count past M in that case.
/// </para>
/// </remarks>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullRejectionRoutingReleaseUntilNativeCapTests
{
    private static readonly string? ServiceAccount = GcpEmulatorIamMember.Value;

    [Fact]
    public async Task When_gcp_rejection_routing_keeps_failing_should_release_until_native_dead_letter_policy_forwards_the_message_without_rejection_metadata()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new GcpPullMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        // Provisioning subscription (5.8's Given, widened): Create-mode, carries the native
        // DeadLetterPolicy (M = 5) so its topic and reading subscription exist before publish.
        var provisioning = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = ServiceAccount,
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            subscriberMember: ServiceAccount);

        // Subscription under test: Assume-mode, Brighter budget 3, zero requeue delay, and a
        // deadLetterRoutingKey whose topic is never created — every Reject fails to route.
        var underTest = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            requeueCount: 3,
            requeueDelay: TimeSpan.Zero,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull,
            deadLetterRoutingKey: deadLetterRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        GcpPubSubChannelFactory? readerChannelFactory = null;
        IAmAChannelSync? policyChannel = null;
        GcpPubSubSubscription? policySubscription = null;

        try
        {
            // Stand up the source topic/subscription and the native DLQ topic/subscription.
            provider.CreateChannel(provisioning).Dispose();

            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(underTest);

            // A separate connection/factory to read the native policy topic directly — not the
            // Brighter deadLetterRoutingKey route, which never receives anything in this test.
            var readerConnection = new GcpMessagingGatewayConnection
            {
                Credential = GatewayFactory.GetCredential(),
                ProjectId = GatewayFactory.GetProjectId(),
                TopicManagerConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
                PublisherConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
                SubscriptionManagerConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
            };
            readerChannelFactory = new GcpPubSubChannelFactory(readerConnection);

            // The native policy's own reading subscription already exists (created by
            // provisioning above); Assume attaches to it without touching the broker.
            policySubscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
                subscriptionName: new SubscriptionName(nativeDlqChannel),
                channelName: nativeDlqChannel,
                routingKey: nativeDlqTopic,
                messagePumpType: MessagePumpType.Reactor,
                ackDeadlineSeconds: 60,
                makeChannels: OnMissingChannel.Assume,
                subscriptionMode: SubscriptionMode.Pull);

            policyChannel = readerChannelFactory.CreateSyncChannel(policySubscription);

            var cmd = new ConformanceDeferredCommand { Value = "pull release-until-native-cap test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            using var logContext = TestCorrelator.CreateContext();

            producer.Send(message);

            // Act — run the production pump with Brighter's own budget = 3.
            var pump = ConformanceDeferredPump.CreateReactor(channel, 3, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = ConformanceDeferredPump.KeyOf(message);

            // Poll the policy subscription every 500 ms for up to 60 s.
            var policyMessage = new Message();
            var arrived = false;
            var arrivalStopwatch = Stopwatch.StartNew();
            while (arrivalStopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                policyMessage = policyChannel.Receive(TimeSpan.FromMilliseconds(500));
                if (policyMessage.Header.MessageType == MessageType.MT_COMMAND)
                {
                    arrived = true;
                    break;
                }
            }

            // On arrival, snapshot the dispatch count, then wait W = 10 s before quitting.
            var snapshotCount = ConformanceDeferredPump.GetDispatchCount(key);

            if (arrived)
            {
                policyChannel.Acknowledge(policyMessage);
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var finalCount = ConformanceDeferredPump.GetDispatchCount(key);

            // Assert — arrival first: with no arrival within 60 s, A-6 is refuted only if the count
            // is past M = 5 — a count at or below M with no arrival is an ordinary failure, and this
            // is the assertion that catches it.
            if (!arrived)
            {
                Assert.True(finalCount > 5,
                    $"no arrival within 60s: A-6 refutation requires the invocation count to be "
                    + $"past M=5, but was {finalCount} (a count at or below M is an ordinary failure)");
            }

            // Assert — no rejection metadata on the native-forwarded copy (R-9).
            if (arrived)
            {
                ConformanceDeferredPump.AssertIsTheMessageSent(message, policyMessage);

                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            }

            // Assert — at least one R-19 Error naming the message id and "DeliveryError".
            var errors = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Error)
                .ToList();
            Assert.Contains(errors, e => e.RenderMessage().Contains(message.Id.Value)
                                          && e.RenderMessage().Contains("DeliveryError"));

            // Assert — the Brighter deadLetterRoutingKey topic is still absent.
            Assert.False(provider.TopicExists(deadLetterRoutingKey));

            // Assert — final count: > 3 and equal to the arrival snapshot (the not-arrived case was
            // already asserted above).
            if (arrived)
            {
                Assert.True(finalCount > 3,
                    $"expected final dispatch count > 3 but was {finalCount}");
                Assert.Equal(snapshotCount, finalCount);
            }
        }
        finally
        {
            policyChannel?.Dispose();
            if (readerChannelFactory != null && policySubscription != null)
            {
                readerChannelFactory.DeleteTopic(policySubscription);
                readerChannelFactory.DeleteSubscription(policySubscription);
            }

            provider.CleanUp(producer, channel, []);
        }
    }
}

/// <summary>PullOrdering — same clauses as the unordered Pull configuration (task 6.30).</summary>
[Trait("Category", "GcpPubSubPullOrdering")]
[Collection("PullOrdering")]
public class GcpPullOrderingRejectionRoutingReleaseUntilNativeCapTests
{
    private static readonly string? ServiceAccount = GcpEmulatorIamMember.Value;

    [Fact]
    public async Task When_gcp_pull_ordering_rejection_routing_keeps_failing_should_release_until_native_dead_letter_policy_forwards_the_message_without_rejection_metadata()
    {
        // Arrange
        PullOrderingPump.ResetDispatchCount();

        var provider = new GcpPullOrderingMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        var provisioning = new GcpPubSubSubscription<PullOrderingCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            deadLetter: new DeadLetterPolicy(nativeDlqTopic, nativeDlqChannel)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = ServiceAccount,
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            subscriberMember: ServiceAccount);

        var underTest = new GcpPubSubSubscription<PullOrderingCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            requeueCount: 3,
            requeueDelay: TimeSpan.Zero,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            deadLetterRoutingKey: deadLetterRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        GcpPubSubChannelFactory? readerChannelFactory = null;
        IAmAChannelSync? policyChannel = null;
        GcpPubSubSubscription? policySubscription = null;

        try
        {
            provider.CreateChannel(provisioning).Dispose();

            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(underTest);

            var readerConnection = new GcpMessagingGatewayConnection
            {
                Credential = GatewayFactory.GetCredential(),
                ProjectId = GatewayFactory.GetProjectId(),
                TopicManagerConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
                PublisherConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
                SubscriptionManagerConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
            };
            readerChannelFactory = new GcpPubSubChannelFactory(readerConnection);

            policySubscription = new GcpPubSubSubscription<PullOrderingCommand>(
                subscriptionName: new SubscriptionName(nativeDlqChannel),
                channelName: nativeDlqChannel,
                routingKey: nativeDlqTopic,
                messagePumpType: MessagePumpType.Reactor,
                ackDeadlineSeconds: 60,
                makeChannels: OnMissingChannel.Assume,
                subscriptionMode: SubscriptionMode.Pull);

            policyChannel = readerChannelFactory.CreateSyncChannel(policySubscription);

            var cmd = new PullOrderingCommand { Value = "pull ordering release-until-native-cap test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            using var logContext = TestCorrelator.CreateContext();

            producer.Send(message);

            var pump = PullOrderingPump.CreateReactor(channel, 3, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = PullOrderingPump.KeyOf(message);

            var policyMessage = new Message();
            var arrived = false;
            var arrivalStopwatch = Stopwatch.StartNew();
            while (arrivalStopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                policyMessage = policyChannel.Receive(TimeSpan.FromMilliseconds(500));
                if (policyMessage.Header.MessageType == MessageType.MT_COMMAND)
                {
                    arrived = true;
                    break;
                }
            }

            var snapshotCount = PullOrderingPump.GetDispatchCount(key);

            if (arrived)
            {
                policyChannel.Acknowledge(policyMessage);
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var finalCount = PullOrderingPump.GetDispatchCount(key);

            if (!arrived)
            {
                Assert.True(finalCount > 5,
                    $"no arrival within 60s: A-6 refutation requires the invocation count to be "
                    + $"past M=5, but was {finalCount} (a count at or below M is an ordinary failure)");
            }

            if (arrived)
            {
                PullOrderingPump.AssertIsTheMessageSent(message, policyMessage);

                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason));
                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage));
                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp));
                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic));
                Assert.False(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType));
            }

            var errors = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Error)
                .ToList();
            Assert.Contains(errors, e => e.RenderMessage().Contains(message.Id.Value)
                                          && e.RenderMessage().Contains("DeliveryError"));

            // GcpPullOrderingMessageGatewayProvider has no TopicExists helper (unlike the
            // unordered Pull provider), so check the broker directly via the reader connection.
            Assert.False(TopicExistsOnBroker(readerConnection, deadLetterRoutingKey));

            if (arrived)
            {
                Assert.True(finalCount > 3,
                    $"expected final dispatch count > 3 but was {finalCount}");
                Assert.Equal(snapshotCount, finalCount);
            }
        }
        finally
        {
            policyChannel?.Dispose();
            if (readerChannelFactory != null && policySubscription != null)
            {
                readerChannelFactory.DeleteTopic(policySubscription);
                readerChannelFactory.DeleteSubscription(policySubscription);
            }

            provider.CleanUp(producer, channel, []);
        }
    }

    /// <summary>
    /// Checks whether a topic exists on the broker, mirroring
    /// <c>GcpPullMessageGatewayProvider.TopicExists</c> — which
    /// <c>GcpPullOrderingMessageGatewayProvider</c> does not expose.
    /// </summary>
    private static bool TopicExistsOnBroker(GcpMessagingGatewayConnection connection, RoutingKey routingKey)
    {
        var client = connection.CreatePublisherServiceApiClient();
        var topicName = TopicName.FromProjectTopic(connection.ProjectId, routingKey.Value);
        try
        {
            client.GetTopic(topicName);
            return true;
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return false;
        }
    }
}

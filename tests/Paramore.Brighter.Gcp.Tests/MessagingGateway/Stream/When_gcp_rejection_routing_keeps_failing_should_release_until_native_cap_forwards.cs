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
using StreamOrderingCommand = Paramore.Brighter.Gcp.Tests.MessagingGateway.StreamOrdering.ConformanceDeferredCommand;
using StreamOrderingPump = Paramore.Brighter.Gcp.Tests.MessagingGateway.StreamOrdering.ConformanceDeferredPump;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// CHARACTERISE (task 6.30, R-19, R-4, R-9, A-6, AC-43) — sync (Reactor), Stream (unordered).
/// </summary>
/// <remarks>
/// See <c>Pull/When_gcp_rejection_routing_keeps_failing_should_release_until_native_cap_forwards.cs</c>
/// for the full characterisation. On the stream consumer, R-19's release is a streaming Nack
/// (<c>GcpPubSubStreamMessageConsumer.Reject</c> on <see cref="RoutingOutcome.Failed"/>) rather than
/// <c>ModifyAckDeadline</c>, which also frees the flow-control slot so a failed routing never stalls
/// the subscription (ADR 0078).
/// </remarks>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamRejectionRoutingReleaseUntilNativeCapTests
{
    private static readonly string? ServiceAccount = GcpEmulatorIamMember.Value;

    [Fact]
    public async Task When_gcp_rejection_routing_keeps_failing_should_release_until_native_dead_letter_policy_forwards_the_message_without_rejection_metadata()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new GcpStreamMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        // Provisioning subscription: always Pull mode, even in the Stream provider (ADR 0078), so
        // it does not start a second, competing streaming-pull consumer.
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

        // Subscription under test: leaves subscriptionMode at its constructor default (Stream).
        var underTest = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            requeueCount: 3,
            requeueDelay: TimeSpan.Zero,
            makeChannels: OnMissingChannel.Assume,
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

            // The reading subscription is a plain Pull-mode read (Receive/Acknowledge), sufficient
            // to read the native DLQ copy regardless of the main configuration's stream mode.
            policySubscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
                subscriptionName: new SubscriptionName(nativeDlqChannel),
                channelName: nativeDlqChannel,
                routingKey: nativeDlqTopic,
                messagePumpType: MessagePumpType.Reactor,
                ackDeadlineSeconds: 60,
                makeChannels: OnMissingChannel.Assume,
                subscriptionMode: SubscriptionMode.Pull);

            policyChannel = readerChannelFactory.CreateSyncChannel(policySubscription);

            var cmd = new ConformanceDeferredCommand { Value = "stream release-until-native-cap test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            using var logContext = TestCorrelator.CreateContext();

            producer.Send(message);

            // Act
            var pump = ConformanceDeferredPump.CreateReactor(channel, 3, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = ConformanceDeferredPump.KeyOf(message);

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

            var snapshotCount = ConformanceDeferredPump.GetDispatchCount(key);

            if (arrived)
            {
                policyChannel.Acknowledge(policyMessage);
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var finalCount = ConformanceDeferredPump.GetDispatchCount(key);

            if (!arrived)
            {
                Assert.True(finalCount > 5,
                    $"no arrival within 60s: A-6 refutation requires the invocation count to be "
                    + $"past M=5, but was {finalCount} (a count at or below M is an ordinary failure)");
            }

            if (arrived)
            {
                ConformanceDeferredPump.AssertIsTheMessageSent(message, policyMessage);

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

            Assert.False(provider.TopicExists(deadLetterRoutingKey));

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

/// <summary>StreamOrdering — same clauses as the unordered Stream configuration (task 6.30).</summary>
/// <remarks>
/// <c>GcpStreamOrderingMessageGatewayProvider</c> has no <c>TopicExists</c> helper (unlike the
/// unordered Stream provider), so this class checks the broker directly via its own reader
/// connection.
/// </remarks>
[Trait("Category", "GcpPubSubStreamOrdering")]
[Collection("StreamOrdering")]
public class GcpStreamOrderingRejectionRoutingReleaseUntilNativeCapTests
{
    private static readonly string? ServiceAccount = GcpEmulatorIamMember.Value;

    [Fact]
    public async Task When_gcp_stream_ordering_rejection_routing_keeps_failing_should_release_until_native_dead_letter_policy_forwards_the_message_without_rejection_metadata()
    {
        // Arrange
        StreamOrderingPump.ResetDispatchCount();

        var provider = new GcpStreamOrderingMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        var provisioning = new GcpPubSubSubscription<StreamOrderingCommand>(
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

        var underTest = new GcpPubSubSubscription<StreamOrderingCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            requeueCount: 3,
            requeueDelay: TimeSpan.Zero,
            makeChannels: OnMissingChannel.Assume,
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

            policySubscription = new GcpPubSubSubscription<StreamOrderingCommand>(
                subscriptionName: new SubscriptionName(nativeDlqChannel),
                channelName: nativeDlqChannel,
                routingKey: nativeDlqTopic,
                messagePumpType: MessagePumpType.Reactor,
                ackDeadlineSeconds: 60,
                makeChannels: OnMissingChannel.Assume,
                subscriptionMode: SubscriptionMode.Pull);

            policyChannel = readerChannelFactory.CreateSyncChannel(policySubscription);

            var cmd = new StreamOrderingCommand { Value = "stream ordering release-until-native-cap test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            using var logContext = TestCorrelator.CreateContext();

            producer.Send(message);

            var pump = StreamOrderingPump.CreateReactor(channel, 3, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = StreamOrderingPump.KeyOf(message);

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

            var snapshotCount = StreamOrderingPump.GetDispatchCount(key);

            if (arrived)
            {
                policyChannel.Acknowledge(policyMessage);
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var finalCount = StreamOrderingPump.GetDispatchCount(key);

            if (!arrived)
            {
                Assert.True(finalCount > 5,
                    $"no arrival within 60s: A-6 refutation requires the invocation count to be "
                    + $"past M=5, but was {finalCount} (a count at or below M is an ordinary failure)");
            }

            if (arrived)
            {
                StreamOrderingPump.AssertIsTheMessageSent(message, policyMessage);

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
    /// <c>GcpStreamMessageGatewayProvider.TopicExists</c> — which
    /// <c>GcpStreamOrderingMessageGatewayProvider</c> does not expose.
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

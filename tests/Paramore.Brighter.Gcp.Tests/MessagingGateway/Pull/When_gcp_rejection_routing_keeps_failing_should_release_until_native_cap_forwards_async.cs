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
using PullOrderingCommand = Paramore.Brighter.Gcp.Tests.MessagingGateway.PullOrdering.ConformanceDeferredCommand;
using PullOrderingPump = Paramore.Brighter.Gcp.Tests.MessagingGateway.PullOrdering.ConformanceDeferredPump;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// Async (Proactor) twin of <see cref="GcpPullRejectionRoutingReleaseUntilNativeCapTests"/> (task
/// 6.30, R-19, R-4, R-9, A-6, AC-43) — Pull (unordered).
/// </summary>
[Property("Category", "GcpPubSubPull")]
[NotInParallel("Pull")]
public class GcpPullRejectionRoutingReleaseUntilNativeCapProactorTests
{
    private static readonly string? ServiceAccount = GcpEmulatorIamMember.Value;

    [Test]
    public async Task When_gcp_rejection_routing_keeps_failing_should_release_until_native_dead_letter_policy_forwards_the_message_without_rejection_metadata_async()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new GcpPullMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var nativeDlqTopic = new RoutingKey($"{routingKey.Value}.native");
        var nativeDlqChannel = new ChannelName($"{routingKey.Value}.native");

        var provisioning = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
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

        var underTest = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 10,
            requeueCount: 3,
            requeueDelay: TimeSpan.Zero,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull,
            deadLetterRoutingKey: deadLetterRoutingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;
        GcpPubSubChannelFactory? readerChannelFactory = null;
        IAmAChannelAsync? policyChannel = null;
        GcpPubSubSubscription? policySubscription = null;

        try
        {
            var provisioningChannel = await provider.CreateChannelAsync(provisioning);
            await provisioningChannel.DisposeAsync();

            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(underTest);

            var readerConnection = new GcpMessagingGatewayConnection
            {
                Credential = GatewayFactory.GetCredential(),
                ProjectId = GatewayFactory.GetProjectId(),
                TopicManagerConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
                PublisherConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
                SubscriptionManagerConfiguration = cfg => { cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction; },
            };
            readerChannelFactory = new GcpPubSubChannelFactory(readerConnection);

            policySubscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
                subscriptionName: new SubscriptionName(nativeDlqChannel),
                channelName: nativeDlqChannel,
                routingKey: nativeDlqTopic,
                messagePumpType: MessagePumpType.Proactor,
                ackDeadlineSeconds: 60,
                makeChannels: OnMissingChannel.Assume,
                subscriptionMode: SubscriptionMode.Pull);

            policyChannel = await readerChannelFactory.CreateAsyncChannelAsync(policySubscription);

            var cmd = new ConformanceDeferredCommand { Value = "pull release-until-native-cap test (async)" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            using var logContext = TestCorrelator.CreateContext();

            await producer.SendAsync(message);

            // Act
            var pump = ConformanceDeferredPump.CreateProactor(channel, 3, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = ConformanceDeferredPump.KeyOf(message);

            var policyMessage = new Message();
            var arrived = false;
            var arrivalStopwatch = Stopwatch.StartNew();
            while (arrivalStopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                policyMessage = await policyChannel.ReceiveAsync(TimeSpan.FromMilliseconds(500));
                if (policyMessage.Header.MessageType == MessageType.MT_COMMAND)
                {
                    arrived = true;
                    break;
                }
            }

            var snapshotCount = ConformanceDeferredPump.GetDispatchCount(key);

            if (arrived)
            {
                await policyChannel.AcknowledgeAsync(policyMessage);
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var finalCount = ConformanceDeferredPump.GetDispatchCount(key);

            if (!arrived)
            {
                await Assert.That(finalCount > 5).IsTrue();
            }

            if (arrived)
            {
                await ConformanceDeferredPump.AssertIsTheMessageSent(message, policyMessage);

                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsFalse();
                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsFalse();
                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsFalse();
                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsFalse();
                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsFalse();
            }

            var errors = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Error)
                .ToList();
            await Assert.That(System.Linq.Enumerable.Any(errors, e => e.RenderMessage().Contains(message.Id.Value)
                                          && e.RenderMessage().Contains("DeliveryError"))).IsTrue();

            await Assert.That(await provider.TopicExistsAsync(deadLetterRoutingKey)).IsFalse();

            if (arrived)
            {
                await Assert.That(finalCount > 3).IsTrue().Because($"expected final dispatch count > 3 but was {finalCount}");
                await Assert.That(finalCount).IsEqualTo(snapshotCount);
            }
        }
        finally
        {
            if (policyChannel != null)
            {
                await policyChannel.DisposeAsync();
            }

            if (readerChannelFactory != null && policySubscription != null)
            {
                await readerChannelFactory.DeleteTopicAsync(policySubscription);
                await readerChannelFactory.DeleteSubscriptionAsync(policySubscription);
            }

            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

/// <summary>PullOrdering — async (Proactor) twin, same clauses (task 6.30).</summary>
[Property("Category", "GcpPubSubPullOrdering")]
[NotInParallel("PullOrdering")]
public class GcpPullOrderingRejectionRoutingReleaseUntilNativeCapProactorTests
{
    private static readonly string? ServiceAccount = GcpEmulatorIamMember.Value;

    [Test]
    public async Task When_gcp_pull_ordering_rejection_routing_keeps_failing_should_release_until_native_dead_letter_policy_forwards_the_message_without_rejection_metadata_async()
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
            messagePumpType: MessagePumpType.Proactor,
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
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 10,
            requeueCount: 3,
            requeueDelay: TimeSpan.Zero,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            deadLetterRoutingKey: deadLetterRoutingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;
        GcpPubSubChannelFactory? readerChannelFactory = null;
        IAmAChannelAsync? policyChannel = null;
        GcpPubSubSubscription? policySubscription = null;

        try
        {
            var provisioningChannel = await provider.CreateChannelAsync(provisioning);
            await provisioningChannel.DisposeAsync();

            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(underTest);

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
                messagePumpType: MessagePumpType.Proactor,
                ackDeadlineSeconds: 60,
                makeChannels: OnMissingChannel.Assume,
                subscriptionMode: SubscriptionMode.Pull);

            policyChannel = await readerChannelFactory.CreateAsyncChannelAsync(policySubscription);

            var cmd = new PullOrderingCommand { Value = "pull ordering release-until-native-cap test (async)" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            using var logContext = TestCorrelator.CreateContext();

            await producer.SendAsync(message);

            var pump = PullOrderingPump.CreateProactor(channel, 3, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = PullOrderingPump.KeyOf(message);

            var policyMessage = new Message();
            var arrived = false;
            var arrivalStopwatch = Stopwatch.StartNew();
            while (arrivalStopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                policyMessage = await policyChannel.ReceiveAsync(TimeSpan.FromMilliseconds(500));
                if (policyMessage.Header.MessageType == MessageType.MT_COMMAND)
                {
                    arrived = true;
                    break;
                }
            }

            var snapshotCount = PullOrderingPump.GetDispatchCount(key);

            if (arrived)
            {
                await policyChannel.AcknowledgeAsync(policyMessage);
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var finalCount = PullOrderingPump.GetDispatchCount(key);

            if (!arrived)
            {
                await Assert.That(finalCount > 5).IsTrue();
            }

            if (arrived)
            {
                await PullOrderingPump.AssertIsTheMessageSent(message, policyMessage);

                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason)).IsFalse();
                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage)).IsFalse();
                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp)).IsFalse();
                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic)).IsFalse();
                await Assert.That(policyMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType)).IsFalse();
            }

            var errors = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Error)
                .ToList();
            await Assert.That(System.Linq.Enumerable.Any(errors, e => e.RenderMessage().Contains(message.Id.Value)
                                          && e.RenderMessage().Contains("DeliveryError"))).IsTrue();

            // GcpPullOrderingMessageGatewayProvider has no TopicExistsAsync helper (unlike the
            // unordered Pull provider), so check the broker directly via the reader connection.
            await Assert.That(await TopicExistsOnBrokerAsync(readerConnection, deadLetterRoutingKey)).IsFalse();

            if (arrived)
            {
                await Assert.That(finalCount > 3).IsTrue().Because($"expected final dispatch count > 3 but was {finalCount}");
                await Assert.That(finalCount).IsEqualTo(snapshotCount);
            }
        }
        finally
        {
            if (policyChannel != null)
            {
                await policyChannel.DisposeAsync();
            }

            if (readerChannelFactory != null && policySubscription != null)
            {
                await readerChannelFactory.DeleteTopicAsync(policySubscription);
                await readerChannelFactory.DeleteSubscriptionAsync(policySubscription);
            }

            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    /// <summary>
    /// Checks whether a topic exists on the broker, mirroring
    /// <c>GcpPullMessageGatewayProvider.TopicExistsAsync</c> — which
    /// <c>GcpPullOrderingMessageGatewayProvider</c> does not expose.
    /// </summary>
    private static async Task<bool> TopicExistsOnBrokerAsync(GcpMessagingGatewayConnection connection, RoutingKey routingKey)
    {
        var client = await connection.CreatePublisherServiceApiClientAsync();
        var topicName = TopicName.FromProjectTopic(connection.ProjectId, routingKey.Value);
        try
        {
            await client.GetTopicAsync(topicName);
            return true;
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return false;
        }
    }
}

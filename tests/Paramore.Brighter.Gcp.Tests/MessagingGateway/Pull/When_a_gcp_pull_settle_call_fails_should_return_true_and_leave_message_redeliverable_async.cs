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
using System.Threading.Tasks;
using Google.Api.Gax;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-16, R-17, R-19 — async (Proactor) path. ADR 0078 "Evidence for the two failures a broker
/// cannot produce on demand" / step 8. See the sync file
/// (<see cref="GcpPullSettleCallFailureTests"/>) for the full rationale; this file exercises the
/// same three scenarios through <c>RejectAsync</c>.
///
/// Covers GCP / Pull and GCP / PullOrdering.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
[Trait("Requires", "PubSubEmulator")]
public class GcpPullSettleCallFailureAsyncTests
{
    private const int AckDeadlineSeconds = 10;
    private const int RedeliveryWindowSeconds = 30;
    private const int DlqWindowSeconds = 60;

    private const string AcknowledgeMethod = "/google.pubsub.v1.Subscriber/Acknowledge";
    private const string ModifyAckDeadlineMethod = "/google.pubsub.v1.Subscriber/ModifyAckDeadline";

    /// <summary>
    /// Builds a connection whose subscription-manager builder is wired to a fault-injecting
    /// invoker instead of emulator detection (ADR 0078 "How it is wired"): <c>Credential = null</c>
    /// (the connection pre-sets it), <c>EmulatorDetection</c> left at <c>None</c> (the default —
    /// setting it would silently drop the injected invoker), and <c>CallInvoker</c> pointed at the
    /// same emulator through <paramref name="faults"/>. The topic/publisher builders are left on
    /// ordinary emulator detection so routing/provisioning still works.
    /// </summary>
    private static GcpMessagingGatewayConnection BuildArmedConnection(GcpFaultInjectingInterceptor faults)
    {
        var emulatorHost = Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST")!;
        return new GcpMessagingGatewayConnection
        {
            Credential = GatewayFactory.GetCredential(),
            ProjectId = GatewayFactory.GetProjectId(),
            TopicManagerConfiguration = cfg => cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
            PublisherConfiguration = cfg => cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
            SubscriptionManagerConfiguration = builder =>
            {
                builder.Credential = null;
                builder.CallInvoker = GrpcChannel
                    .ForAddress("http://" + emulatorHost, new GrpcChannelOptions { Credentials = ChannelCredentials.Insecure })
                    .Intercept(faults);
            },
        };
    }

    private static async Task<Message> PollForRedeliveryAsync(IAmAChannelAsync channel, int windowSeconds)
    {
        var redelivered = new Message();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(windowSeconds))
        {
            redelivered = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
            if (redelivered.Header.MessageType != MessageType.MT_NONE)
                break;
            await Task.Delay(250);
        }

        return redelivered;
    }

    // -------------------------------------------------------------------------
    // GCP / Pull
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scenario (a): destination exists, Acknowledge armed.
    /// </summary>
    [Fact]
    public async Task When_ack_fails_async_on_pull_with_destination_should_return_true_and_redeliver_and_route()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");

        var subscriptionUnderTest = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            deadLetterRoutingKey: dlqRoutingKey);

        var faults = new GcpFaultInjectingInterceptor(AcknowledgeMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? provisioningChannel = null;
        IAmAChannelAsync? armedChannel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            provisioningChannel = await provider.CreateChannelAsync(subscriptionUnderTest);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await provisioningChannel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                deadLetterRoutingKey: dlqRoutingKey,
                makeChannels: OnMissingChannel.Create);
            armedChannel = new ChannelAsync(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            // Act
            var result = await armedChannel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "ack rpc fault"));

            // Assert — RejectAsync returns true and does not throw
            Assert.True(result);

            // Assert — the interceptor actually fired
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // Assert — an Error names the message id
            // GcpRejectionRouter also logs its own Error for the failed routing publish, so this
            // must be narrowed to the settle-specific log line (Log.RejectError's "rejecting the
            // message" text).
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error
                     && e.RenderMessage().Contains(received.Id.Value)
                     && e.RenderMessage().Contains("rejecting the message"));
            Assert.NotNull(settleError);

            faults.Disarm();

            // Assert — after disarming, the message is redelivered once its deadline lapses
            var redelivered = await PollForRedeliveryAsync(provisioningChannel, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            await provisioningChannel.AcknowledgeAsync(redelivered);

            // Assert — the destination holds the copy
            Message dlqMessage = new Message();
            var dlqStopwatch = Stopwatch.StartNew();
            while (dlqStopwatch.Elapsed < TimeSpan.FromSeconds(DlqWindowSeconds))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscriptionUnderTest);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            if (armedChannel != null) await armedChannel.DisposeAsync();
            await provider.CleanUpAsync(producer, provisioningChannel, []);
        }
    }

    /// <summary>
    /// Scenario (b): no destination configured, Acknowledge armed.
    /// </summary>
    [Fact]
    public async Task When_ack_fails_async_on_pull_with_no_destination_should_return_true_and_redeliver()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var subscriptionUnderTest = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);
        Assert.Equal(AckDeadlineSeconds, subscriptionUnderTest.AckDeadlineSeconds);

        var faults = new GcpFaultInjectingInterceptor(AcknowledgeMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? provisioningChannel = null;
        IAmAChannelAsync? armedChannel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            provisioningChannel = await provider.CreateChannelAsync(subscriptionUnderTest);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await provisioningChannel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                makeChannels: OnMissingChannel.Create);
            armedChannel = new ChannelAsync(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            var result = await armedChannel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "ack rpc fault"));

            Assert.True(result);
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // GcpRejectionRouter also logs its own Error for the failed routing publish, so this
            // must be narrowed to the settle-specific log line (Log.RejectError's "rejecting the
            // message" text).
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error
                     && e.RenderMessage().Contains(received.Id.Value)
                     && e.RenderMessage().Contains("rejecting the message"));
            Assert.NotNull(settleError);

            faults.Disarm();

            var redelivered = await PollForRedeliveryAsync(provisioningChannel, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            await provisioningChannel.AcknowledgeAsync(redelivered);
        }
        finally
        {
            if (armedChannel != null) await armedChannel.DisposeAsync();
            await provider.CleanUpAsync(producer, provisioningChannel, []);
        }
    }

    /// <summary>
    /// Scenario (c): AC-18 Given, ModifyAckDeadline armed.
    /// </summary>
    [Fact]
    public async Task When_release_fails_async_on_pull_should_return_true_and_redeliver()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var neverExistsDlqRoutingKey = new RoutingKey($"{routingKey.Value}.NeverExists.DLQ");

        var provisioning = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull);

        var faults = new GcpFaultInjectingInterceptor(ModifyAckDeadlineMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? provisioningChannel = null;
        IAmAChannelAsync? armedChannel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            provisioningChannel = await provider.CreateChannelAsync(provisioning);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await provisioningChannel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                deadLetterRoutingKey: neverExistsDlqRoutingKey,
                makeChannels: OnMissingChannel.Assume);
            armedChannel = new ChannelAsync(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            var result = await armedChannel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "release rpc fault"));

            Assert.True(result);
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // GcpRejectionRouter also logs its own Error for the failed routing publish, so this
            // must be narrowed to the settle-specific log line (Log.RejectError's "rejecting the
            // message" text).
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error
                     && e.RenderMessage().Contains(received.Id.Value)
                     && e.RenderMessage().Contains("rejecting the message"));
            Assert.NotNull(settleError);

            faults.Disarm();

            var redelivered = await PollForRedeliveryAsync(provisioningChannel, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            await provisioningChannel.AcknowledgeAsync(redelivered);
        }
        finally
        {
            if (armedChannel != null) await armedChannel.DisposeAsync();
            await provider.CleanUpAsync(producer, provisioningChannel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scenario (a) on an ordering-enabled subscription: destination exists, Acknowledge armed.
    /// </summary>
    [Fact]
    public async Task When_ack_fails_async_on_pull_ordering_with_destination_should_return_true_and_redeliver_and_route()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");

        var subscriptionUnderTest = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            deadLetterRoutingKey: dlqRoutingKey);

        var faults = new GcpFaultInjectingInterceptor(AcknowledgeMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? provisioningChannel = null;
        IAmAChannelAsync? armedChannel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            provisioningChannel = await provider.CreateChannelAsync(subscriptionUnderTest);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await provisioningChannel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                deadLetterRoutingKey: dlqRoutingKey,
                makeChannels: OnMissingChannel.Create);
            armedChannel = new ChannelAsync(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            var result = await armedChannel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "ack rpc fault"));

            Assert.True(result);
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // GcpRejectionRouter also logs its own Error for the failed routing publish, so this
            // must be narrowed to the settle-specific log line (Log.RejectError's "rejecting the
            // message" text).
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error
                     && e.RenderMessage().Contains(received.Id.Value)
                     && e.RenderMessage().Contains("rejecting the message"));
            Assert.NotNull(settleError);

            faults.Disarm();

            var redelivered = await PollForRedeliveryAsync(provisioningChannel, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            await provisioningChannel.AcknowledgeAsync(redelivered);

            Message dlqMessage = new Message();
            var dlqStopwatch = Stopwatch.StartNew();
            while (dlqStopwatch.Elapsed < TimeSpan.FromSeconds(DlqWindowSeconds))
            {
                dlqMessage = await provider.GetMessageFromDeadLetterQueueAsync(subscriptionUnderTest);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            if (armedChannel != null) await armedChannel.DisposeAsync();
            await provider.CleanUpAsync(producer, provisioningChannel, []);
        }
    }

    /// <summary>
    /// Scenario (b) on an ordering-enabled subscription: no destination configured, Acknowledge armed.
    /// </summary>
    [Fact]
    public async Task When_ack_fails_async_on_pull_ordering_with_no_destination_should_return_true_and_redeliver()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var subscriptionUnderTest = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);
        Assert.Equal(AckDeadlineSeconds, subscriptionUnderTest.AckDeadlineSeconds);

        var faults = new GcpFaultInjectingInterceptor(AcknowledgeMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? provisioningChannel = null;
        IAmAChannelAsync? armedChannel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            provisioningChannel = await provider.CreateChannelAsync(subscriptionUnderTest);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await provisioningChannel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                makeChannels: OnMissingChannel.Create);
            armedChannel = new ChannelAsync(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            var result = await armedChannel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "ack rpc fault"));

            Assert.True(result);
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // GcpRejectionRouter also logs its own Error for the failed routing publish, so this
            // must be narrowed to the settle-specific log line (Log.RejectError's "rejecting the
            // message" text).
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error
                     && e.RenderMessage().Contains(received.Id.Value)
                     && e.RenderMessage().Contains("rejecting the message"));
            Assert.NotNull(settleError);

            faults.Disarm();

            var redelivered = await PollForRedeliveryAsync(provisioningChannel, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            await provisioningChannel.AcknowledgeAsync(redelivered);
        }
        finally
        {
            if (armedChannel != null) await armedChannel.DisposeAsync();
            await provider.CleanUpAsync(producer, provisioningChannel, []);
        }
    }

    /// <summary>
    /// Scenario (c) on an ordering-enabled subscription: AC-18 Given, ModifyAckDeadline armed.
    /// </summary>
    [Fact]
    public async Task When_release_fails_async_on_pull_ordering_should_return_true_and_redeliver()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var neverExistsDlqRoutingKey = new RoutingKey($"{routingKey.Value}.NeverExists.DLQ");

        var provisioning = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true);

        var faults = new GcpFaultInjectingInterceptor(ModifyAckDeadlineMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? provisioningChannel = null;
        IAmAChannelAsync? armedChannel = null;

        try
        {
            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            provisioningChannel = await provider.CreateChannelAsync(provisioning);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await provisioningChannel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                deadLetterRoutingKey: neverExistsDlqRoutingKey,
                makeChannels: OnMissingChannel.Assume);
            armedChannel = new ChannelAsync(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            var result = await armedChannel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "release rpc fault"));

            Assert.True(result);
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // GcpRejectionRouter also logs its own Error for the failed routing publish, so this
            // must be narrowed to the settle-specific log line (Log.RejectError's "rejecting the
            // message" text).
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error
                     && e.RenderMessage().Contains(received.Id.Value)
                     && e.RenderMessage().Contains("rejecting the message"));
            Assert.NotNull(settleError);

            faults.Disarm();

            var redelivered = await PollForRedeliveryAsync(provisioningChannel, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            await provisioningChannel.AcknowledgeAsync(redelivered);
        }
        finally
        {
            if (armedChannel != null) await armedChannel.DisposeAsync();
            await provider.CleanUpAsync(producer, provisioningChannel, []);
        }
    }
}

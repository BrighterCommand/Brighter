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
using System.Threading;
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
/// R-16, R-17, R-19 — sync (Reactor) path. ADR 0078 "Evidence for the two failures a broker cannot
/// produce on demand" / step 8.
///
/// These are test-only fault-injection tests for the failures a broker cannot produce on demand: a
/// failed ack (R-16/R-17) or a failed release (R-19) during GCP pull <c>Reject</c>. A test-only gRPC
/// fault-injecting <see cref="Interceptor"/>, supplied through
/// <see cref="GcpMessagingGatewayConnection.SubscriptionManagerConfiguration"/>, throws
/// <see cref="RpcException"/> with <see cref="StatusCode.FailedPrecondition"/> for the named settle
/// RPC when armed. In every scenario, <c>Reject</c> must return <see langword="true"/> without
/// throwing, log an Error naming the message id, and leave the message to be redelivered once its
/// ack deadline (10s) lapses. Every call still reaches the real emulator (ADR 0078 "Why this is not
/// a mock (C-10)"); only the result of the armed RPC is replaced.
///
/// Covers GCP / Pull and GCP / PullOrdering.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
[Trait("Requires", "PubSubEmulator")]
public class GcpPullSettleCallFailureTests
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

    private static Message PollForRedelivery(IAmAChannelSync channel, Id expectedId, int windowSeconds)
    {
        var redelivered = new Message();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(windowSeconds))
        {
            redelivered = channel.Receive(TimeSpan.FromSeconds(1));
            if (redelivered.Header.MessageType != MessageType.MT_NONE)
                break;
            Thread.Sleep(250);
        }

        return redelivered;
    }

    // -------------------------------------------------------------------------
    // GCP / Pull
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scenario (a): destination exists, Acknowledge armed. The routing publish succeeds (the
    /// destination was pre-provisioned), but the settle Acknowledge RPC fails. Reject returns true,
    /// an Error names the message id, the interceptor fired, the message is redelivered once
    /// disarmed, and the destination holds the routed copy.
    /// </summary>
    [Fact]
    public void When_ack_fails_on_pull_with_destination_should_return_true_and_redeliver_and_route()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");

        // MakeChannels: Create with a DeadLetterRoutingKey pre-provisions the destination topic and
        // reading subscription before the source channel is created (GcpPullMessageGatewayProvider.CreateChannel),
        // so the destination already exists when Reject routes to it.
        var subscriptionUnderTest = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            deadLetterRoutingKey: dlqRoutingKey);

        var faults = new GcpFaultInjectingInterceptor(AcknowledgeMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? provisioningChannel = null;
        IAmAChannelSync? armedChannel = null;

        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            provisioningChannel = provider.CreateChannel(subscriptionUnderTest);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = provisioningChannel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // The channel under test: the same broker subscription, but its consumer's Acknowledge
            // RPC is intercepted.
            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                deadLetterRoutingKey: dlqRoutingKey,
                makeChannels: OnMissingChannel.Create);
            armedChannel = new Channel(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            // Act
            var result = armedChannel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "ack rpc fault"));

            // Assert — Reject returns true and does not throw
            Assert.True(result);

            // Assert — the interceptor actually fired
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // Assert — an Error names the message id
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(received.Id.Value));
            Assert.NotNull(settleError);

            faults.Disarm();

            // Assert — after disarming, the message is redelivered once its deadline lapses
            var redelivered = PollForRedelivery(provisioningChannel, received.Id, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            // Settle the redelivered message so it does not keep redelivering into later tests.
            provisioningChannel.Acknowledge(redelivered);

            // Assert — the destination holds the copy
            Message dlqMessage = new Message();
            var dlqStopwatch = Stopwatch.StartNew();
            while (dlqStopwatch.Elapsed < TimeSpan.FromSeconds(DlqWindowSeconds))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscriptionUnderTest);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            armedChannel?.Dispose();
            provider.CleanUp(producer, provisioningChannel, []);
        }
    }

    /// <summary>
    /// Scenario (b): no destination configured, Acknowledge armed. The router logs a Warning
    /// (R-17) and no publish is attempted, but the settle Acknowledge RPC still fails. Reject
    /// returns true, an Error names the message id, the interceptor fired, and the message is
    /// redelivered once disarmed.
    /// </summary>
    [Fact]
    public void When_ack_fails_on_pull_with_no_destination_should_return_true_and_redeliver()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var subscriptionUnderTest = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);
        Assert.Equal(AckDeadlineSeconds, subscriptionUnderTest.AckDeadlineSeconds);

        var faults = new GcpFaultInjectingInterceptor(AcknowledgeMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? provisioningChannel = null;
        IAmAChannelSync? armedChannel = null;

        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            provisioningChannel = provider.CreateChannel(subscriptionUnderTest);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = provisioningChannel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                makeChannels: OnMissingChannel.Create);
            armedChannel = new Channel(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            // Act
            var result = armedChannel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "ack rpc fault"));

            // Assert — Reject returns true and does not throw
            Assert.True(result);

            // Assert — the interceptor actually fired
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // Assert — an Error names the message id
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(received.Id.Value));
            Assert.NotNull(settleError);

            faults.Disarm();

            // Assert — after disarming, the message is redelivered once its deadline lapses
            var redelivered = PollForRedelivery(provisioningChannel, received.Id, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            provisioningChannel.Acknowledge(redelivered);
        }
        finally
        {
            armedChannel?.Dispose();
            provider.CleanUp(producer, provisioningChannel, []);
        }
    }

    /// <summary>
    /// Scenario (c): AC-18 Given, ModifyAckDeadline armed. The routing publish itself fails (the
    /// destination is never created and the producer is built with Assume), so Reject releases the
    /// original via ModifyAckDeadline — which is armed to fail. Reject returns true, an Error names
    /// the message id, the interceptor fired, and the message is redelivered once disarmed (it was
    /// never released; it returns at its own ack deadline, ADR 0078 "Accepted case 1").
    /// </summary>
    [Fact]
    public void When_release_fails_on_pull_should_return_true_and_redeliver()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var neverExistsDlqRoutingKey = new RoutingKey($"{routingKey.Value}.NeverExists.DLQ");

        // AC-18 Given: a provisioning subscription (Create, no destination) stands up the source;
        // the channel under test is built directly against the armed connection with Assume, so
        // the destination is never created and the routing publish fails (ADR 0078 "The destination
        // producer's makeChannels").
        var provisioning = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull);

        var faults = new GcpFaultInjectingInterceptor(ModifyAckDeadlineMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? provisioningChannel = null;
        IAmAChannelSync? armedChannel = null;

        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            provisioningChannel = provider.CreateChannel(provisioning);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = provisioningChannel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                deadLetterRoutingKey: neverExistsDlqRoutingKey,
                makeChannels: OnMissingChannel.Assume);
            armedChannel = new Channel(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            // Act
            var result = armedChannel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "release rpc fault"));

            // Assert — Reject returns true and does not throw
            Assert.True(result);

            // Assert — the interceptor actually fired
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            // Assert — an Error names the message id for the failed settle RPC. GcpRejectionRouter
            // also logs its own Error for the failed routing publish, so this must be narrowed to
            // the settle-specific log line (Log.RejectError's "rejecting the message" text).
            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error
                     && e.RenderMessage().Contains(received.Id.Value)
                     && e.RenderMessage().Contains("rejecting the message"));
            Assert.NotNull(settleError);

            faults.Disarm();

            // Assert — after disarming, the message is redelivered once its (unaltered) deadline lapses
            var redelivered = PollForRedelivery(provisioningChannel, received.Id, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            provisioningChannel.Acknowledge(redelivered);
        }
        finally
        {
            armedChannel?.Dispose();
            provider.CleanUp(producer, provisioningChannel, []);
        }
    }

    // -------------------------------------------------------------------------
    // GCP / PullOrdering
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scenario (a) on an ordering-enabled subscription: destination exists, Acknowledge armed.
    /// </summary>
    [Fact]
    public void When_ack_fails_on_pull_ordering_with_destination_should_return_true_and_redeliver_and_route()
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
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            deadLetterRoutingKey: dlqRoutingKey);

        var faults = new GcpFaultInjectingInterceptor(AcknowledgeMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? provisioningChannel = null;
        IAmAChannelSync? armedChannel = null;

        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            provisioningChannel = provider.CreateChannel(subscriptionUnderTest);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = provisioningChannel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                deadLetterRoutingKey: dlqRoutingKey,
                makeChannels: OnMissingChannel.Create);
            armedChannel = new Channel(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            var result = armedChannel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "ack rpc fault"));

            Assert.True(result);
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(received.Id.Value));
            Assert.NotNull(settleError);

            faults.Disarm();

            var redelivered = PollForRedelivery(provisioningChannel, received.Id, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            provisioningChannel.Acknowledge(redelivered);

            Message dlqMessage = new Message();
            var dlqStopwatch = Stopwatch.StartNew();
            while (dlqStopwatch.Elapsed < TimeSpan.FromSeconds(DlqWindowSeconds))
            {
                dlqMessage = provider.GetMessageFromDeadLetterQueue(subscriptionUnderTest);
                if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(500);
            }

            Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            armedChannel?.Dispose();
            provider.CleanUp(producer, provisioningChannel, []);
        }
    }

    /// <summary>
    /// Scenario (b) on an ordering-enabled subscription: no destination configured, Acknowledge armed.
    /// </summary>
    [Fact]
    public void When_ack_fails_on_pull_ordering_with_no_destination_should_return_true_and_redeliver()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var subscriptionUnderTest = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);
        Assert.Equal(AckDeadlineSeconds, subscriptionUnderTest.AckDeadlineSeconds);

        var faults = new GcpFaultInjectingInterceptor(AcknowledgeMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? provisioningChannel = null;
        IAmAChannelSync? armedChannel = null;

        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            provisioningChannel = provider.CreateChannel(subscriptionUnderTest);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = provisioningChannel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                makeChannels: OnMissingChannel.Create);
            armedChannel = new Channel(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            var result = armedChannel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "ack rpc fault"));

            Assert.True(result);
            Assert.True(faults.FiredCount > 0,
                "The fault-injecting interceptor never fired; the armed invoker may have been dropped");

            var settleError = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains(received.Id.Value));
            Assert.NotNull(settleError);

            faults.Disarm();

            var redelivered = PollForRedelivery(provisioningChannel, received.Id, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            provisioningChannel.Acknowledge(redelivered);
        }
        finally
        {
            armedChannel?.Dispose();
            provider.CleanUp(producer, provisioningChannel, []);
        }
    }

    /// <summary>
    /// Scenario (c) on an ordering-enabled subscription: AC-18 Given, ModifyAckDeadline armed.
    /// </summary>
    [Fact]
    public void When_release_fails_on_pull_ordering_should_return_true_and_redeliver()
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
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: AckDeadlineSeconds,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true);

        var faults = new GcpFaultInjectingInterceptor(ModifyAckDeadlineMethod);
        var armedConnection = BuildArmedConnection(faults);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? provisioningChannel = null;
        IAmAChannelSync? armedChannel = null;

        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            provisioningChannel = provider.CreateChannel(provisioning);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = provisioningChannel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            var armedConsumer = new GcpPullMessageConsumer(
                armedConnection,
                Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(GatewayFactory.GetProjectId(), channelName.Value),
                batchSize: 1,
                TimeProvider.System,
                deadLetterRoutingKey: neverExistsDlqRoutingKey,
                makeChannels: OnMissingChannel.Assume);
            armedChannel = new Channel(channelName, routingKey, armedConsumer, 1);

            using var logContext = TestCorrelator.CreateContext();

            faults.Arm();

            var result = armedChannel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "release rpc fault"));

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

            var redelivered = PollForRedelivery(provisioningChannel, received.Id, RedeliveryWindowSeconds);
            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            provisioningChannel.Acknowledge(redelivered);
        }
        finally
        {
            armedChannel?.Dispose();
            provider.CleanUp(producer, provisioningChannel, []);
        }
    }
}

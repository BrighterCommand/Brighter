#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.MessagingGateway.PullOrdering;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Paramore.Brighter.Tasks;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

/// <summary>
/// A message gateway provider for GCP Pub/Sub Pull subscription with message ordering enabled.
/// Configures topics and subscriptions with <c>EnableMessageOrdering = true</c> so that
/// messages published with an ordering key are delivered in order.
/// </summary>
public class GcpPullOrderingMessageGatewayProvider
    : PullOrdering.Proactor.IAmAMessageGatewayProactorProvider,
        PullOrdering.Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private readonly ConformanceHarnessMessageScheduler _scheduler;
    private GcpPubSubSubscription? _lastSubscription;

    public GcpPullOrderingMessageGatewayProvider()
    {
        _connection = new GcpMessagingGatewayConnection
        {
            Credential = GatewayFactory.GetCredential(),
            ProjectId = GatewayFactory.GetProjectId(),
            // Every Pub/Sub client builder must opt into emulator detection so that a local
            // PUBSUB_EMULATOR_HOST run reaches the emulator (and CI, with no env var, still hits
            // production). The admin topic client (TopicManagerConfiguration) and streaming
            // subscriber (StreamConfiguration) are separate builders from the publish/subscription
            // manager ones, so they must be wired too — otherwise EnsureTopicExist talks to real GCP.
            TopicManagerConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
            PublisherConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
            SubscriptionManagerConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
            StreamConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
        };
        _channelFactory = new GcpPubSubChannelFactory(_connection);
        _scheduler = new ConformanceHarnessMessageScheduler(RepublishToPubSub);
    }

    // The only part of scheduling that is Pub/Sub's: build a producer, send, and hand it back for
    // the scheduler to dispose.
    private IDisposable? RepublishToPubSub(Message message)
    {
        var publication = new GcpPublication<MyCommand>
        {
            Topic = message.Header.Topic,
            MakeChannels = OnMissingChannel.Assume,
        };

        var topicName = TopicName.FromProjectTopic(
            _connection.ProjectId,
            message.Header.Topic.Value
        );

        // The producer's publication leaves ordering disabled, so partition keys travel only as
        // ce-partitionkey attributes. Enabling ordering on the client alone does not set an OrderingKey.
        var enableOrdering = !string.IsNullOrEmpty(message.Header.PartitionKey);
        var builder = new PublisherClientBuilder
        {
            Credential = _connection.Credential,
            TopicName = topicName,
            Settings = new PublisherClient.Settings { EnableMessageOrdering = enableOrdering },
        };
        _connection.PublisherConfiguration?.Invoke(builder);

        var producer = new GcpMessageProducer(builder.Build(), publication);
        return ConformanceHarnessMessageScheduler.SendAndHandBack(producer, () => producer.Send(message));
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"gen-pull-ord-{Guid.NewGuid():N}");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"gen-pull-ord-{Guid.NewGuid():N}");
    }

    public GcpPublication CreatePublication(
        RoutingKey routingKey,
        OnMissingChannel makeChannels = OnMissingChannel.Create
    )
    {
        return new GcpPublication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
            EnableMessageOrdering = true,
        };
    }

    public GcpPubSubSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null
    )
    {
        if (deadLetterRoutingKey != null)
        {
            var nativeTopicName = new RoutingKey($"{deadLetterRoutingKey.Value}.native");
            var nativeChannelName = new ChannelName($"{deadLetterRoutingKey.Value}.native");

            return new GcpPubSubSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: channelName,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                ackDeadlineSeconds: 60,
                requeueCount: 3,
                deadLetter: new DeadLetterPolicy(nativeTopicName, nativeChannelName)
                {
                    AckDeadlineSeconds = 60,
                    MaxDeliveryAttempts = 5,
                    PublisherMember = GcpEmulatorIamMember.Value,
                },
                makeChannels: makeChannel,
                subscriptionMode: SubscriptionMode.Pull,
                enableMessageOrdering: true,
                subscriberMember: GcpEmulatorIamMember.Value,
                deadLetterRoutingKey: deadLetterRoutingKey,
                invalidMessageRoutingKey: invalidMessageRoutingKey
            );
        }

        return new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            // Nack is a no-op for Pub/Sub: redelivery waits for the ack deadline to expire, so the
            // deadline must be shorter than the tests' 30s bounded-retry ceiling (default 30 == 30).
            ackDeadlineSeconds: 10,
            makeChannels: makeChannel,
            subscriptionMode: SubscriptionMode.Pull,
            enableMessageOrdering: true,
            invalidMessageRoutingKey: invalidMessageRoutingKey
        );
    }

    public IAmAMessageProducerSync CreateProducer(GcpPublication publication)
    {
        if (publication.MakeChannels == OnMissingChannel.Create)
        {
            BrighterAsyncContext.Run(() => _channelFactory.EnsureTopicExistAsync(
                new TopicAttributes { Name = publication.Topic!.Value, ProjectId = _connection.ProjectId },
                publication.MakeChannels));
        }

        var topicName = TopicName.FromProjectTopic(_connection.ProjectId, publication.Topic!.Value);
        var builder = new PublisherClientBuilder
        {
            Credential = _connection.Credential,
            TopicName = topicName,
            Settings = new PublisherClient.Settings
            {
                EnableMessageOrdering = publication.EnableMessageOrdering,
            },
        };
        _connection.PublisherConfiguration?.Invoke(builder);
        // GCP has no native delayed publish; the gateway delegates a non-zero send delay to the
        // scheduler seam. Wire the wall-clock harness scheduler so delayed sends conform.
        return new GcpMessageProducer(builder.Build(), publication) { Scheduler = _scheduler };
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        GcpPublication publication,
        CancellationToken cancellationToken = default
    )
    {
        if (publication.MakeChannels == OnMissingChannel.Create)
        {
            await _channelFactory.EnsureTopicExistAsync(
                new TopicAttributes { Name = publication.Topic!.Value, ProjectId = _connection.ProjectId },
                publication.MakeChannels);
        }

        var topicName = TopicName.FromProjectTopic(_connection.ProjectId, publication.Topic!.Value);
        var builder = new PublisherClientBuilder
        {
            Credential = _connection.Credential,
            TopicName = topicName,
            Settings = new PublisherClient.Settings
            {
                EnableMessageOrdering = publication.EnableMessageOrdering,
            },
        };
        _connection.PublisherConfiguration?.Invoke(builder);
        var client = await builder.BuildAsync(cancellationToken);
        // GCP has no native delayed publish; the gateway delegates a non-zero send delay to the
        // scheduler seam. Wire the wall-clock harness scheduler so delayed sends conform.
        return new GcpMessageProducer(client, publication) { Scheduler = _scheduler };
    }

    public IAmAChannelSync CreateChannel(GcpPubSubSubscription subscription)
    {
        _lastSubscription = subscription;

        // Pre-provision the Brighter DLQ topic and reading subscription when channel creation is
        // requested, so the destination exists before Reject routes there (ADR 0078 step 5).
        if (subscription.DeadLetterRoutingKey != null && subscription.MakeChannels == OnMissingChannel.Create)
        {
            var dlqTopicName = subscription.DeadLetterRoutingKey;
            var dlqChannelName = new ChannelName(dlqTopicName.Value);
            var provisioningSubscription = new GcpPubSubSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(dlqChannelName),
                channelName: dlqChannelName,
                routingKey: dlqTopicName,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create,
                subscriptionMode: SubscriptionMode.Pull
            );
            _channelFactory.CreateSyncChannel(provisioningSubscription).Dispose();
        }

        // Pre-provision the invalid-message topic and reading subscription when channel creation is
        // requested, so the destination exists before Reject routes there (ADR 0078 step 5).
        if (subscription.InvalidMessageRoutingKey != null && subscription.MakeChannels == OnMissingChannel.Create)
        {
            var invalidTopicName = subscription.InvalidMessageRoutingKey;
            var invalidChannelName = new ChannelName(invalidTopicName.Value);
            var provisioningSubscription = new GcpPubSubSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(invalidChannelName),
                channelName: invalidChannelName,
                routingKey: invalidTopicName,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create,
                subscriptionMode: SubscriptionMode.Pull
            );
            _channelFactory.CreateSyncChannel(provisioningSubscription).Dispose();
        }

        return _channelFactory.CreateSyncChannel(subscription);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        GcpPubSubSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        _lastSubscription = subscription;

        // Pre-provision the Brighter DLQ topic and reading subscription when channel creation is
        // requested, so the destination exists before Reject routes there (ADR 0078 step 5).
        if (subscription.DeadLetterRoutingKey != null && subscription.MakeChannels == OnMissingChannel.Create)
        {
            var dlqTopicName = subscription.DeadLetterRoutingKey;
            var dlqChannelName = new ChannelName(dlqTopicName.Value);
            var provisioningSubscription = new GcpPubSubSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(dlqChannelName),
                channelName: dlqChannelName,
                routingKey: dlqTopicName,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Create,
                subscriptionMode: SubscriptionMode.Pull
            );
            var provisioningChannel = await _channelFactory.CreateAsyncChannelAsync(provisioningSubscription, cancellationToken);
            provisioningChannel.Dispose();
        }

        // Pre-provision the invalid-message topic and reading subscription when channel creation is
        // requested, so the destination exists before Reject routes there (ADR 0078 step 5).
        if (subscription.InvalidMessageRoutingKey != null && subscription.MakeChannels == OnMissingChannel.Create)
        {
            var invalidTopicName = subscription.InvalidMessageRoutingKey;
            var invalidChannelName = new ChannelName(invalidTopicName.Value);
            var provisioningSubscription = new GcpPubSubSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(invalidChannelName),
                channelName: invalidChannelName,
                routingKey: invalidTopicName,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Create,
                subscriptionMode: SubscriptionMode.Pull
            );
            var provisioningChannel = await _channelFactory.CreateAsyncChannelAsync(provisioningSubscription, cancellationToken);
            provisioningChannel.Dispose();
        }

        return await _channelFactory.CreateAsyncChannelAsync(subscription, cancellationToken);
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages
    )
    {
        channel?.Dispose();
        producer?.Dispose();
        _scheduler.Dispose();

        if (_lastSubscription != null)
        {
            _channelFactory.DeleteTopic(_lastSubscription);
            _channelFactory.DeleteSubscription(_lastSubscription);
        }
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages
    )
    {
        channel?.Dispose();

        if (producer != null)
        {
            await producer.DisposeAsync();
        }

        _scheduler.Dispose();

        if (_lastSubscription != null)
        {
            await _channelFactory.DeleteTopicAsync(_lastSubscription);
            await _channelFactory.DeleteSubscriptionAsync(_lastSubscription);
        }
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        GcpPubSubSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        // Read from the Brighter dead-letter route ({deadLetterRoutingKey}) with a reading
        // subscription of the same name, provisioned alongside the DLQ topic (ADR 0078 step 5).
        var dlqTopicName = subscription.DeadLetterRoutingKey!;
        var dlqChannelName = new ChannelName(dlqTopicName.Value);

        var dlqSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(dlqChannelName),
            channelName: dlqChannelName,
            routingKey: dlqTopicName,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull
        );

        var dlqChannel = await _channelFactory.CreateAsyncChannelAsync(
            dlqSubscription,
            cancellationToken
        );
        try
        {
            var message = await dlqChannel.ReceiveAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken
            );
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                await dlqChannel.AcknowledgeAsync(message, cancellationToken);
            }

            return message;
        }
        finally
        {
            dlqChannel.Dispose();
        }
    }

    public Message GetMessageFromDeadLetterQueue(GcpPubSubSubscription subscription)
    {
        // Read from the Brighter dead-letter route ({deadLetterRoutingKey}) with a reading
        // subscription of the same name, provisioned alongside the DLQ topic (ADR 0078 step 5).
        var dlqTopicName = subscription.DeadLetterRoutingKey!;
        var dlqChannelName = new ChannelName(dlqTopicName.Value);

        var dlqSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(dlqChannelName),
            channelName: dlqChannelName,
            routingKey: dlqTopicName,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull
        );

        var dlqChannel = _channelFactory.CreateSyncChannel(dlqSubscription);
        try
        {
            var message = dlqChannel.Receive(TimeSpan.FromSeconds(5));
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                dlqChannel.Acknowledge(message);
            }

            return message;
        }
        finally
        {
            dlqChannel.Dispose();
        }
    }

    public Message GetMessageFromInvalidChannel(GcpPubSubSubscription subscription)
    {
        // Read from the invalid-message route ({invalidMessageRoutingKey}) with a reading
        // subscription of the same name, provisioned alongside the invalid-message topic (ADR 0078 step 5).
        var invalidTopicName = subscription.InvalidMessageRoutingKey!;
        var invalidChannelName = new ChannelName(invalidTopicName.Value);

        var invalidSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(invalidChannelName),
            channelName: invalidChannelName,
            routingKey: invalidTopicName,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull
        );

        var invalidChannel = _channelFactory.CreateSyncChannel(invalidSubscription);
        try
        {
            var message = invalidChannel.Receive(TimeSpan.FromSeconds(5));
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                invalidChannel.Acknowledge(message);
            }

            return message;
        }
        finally
        {
            invalidChannel.Dispose();
        }
    }

    public async Task<Message> GetMessageFromInvalidChannelAsync(
        GcpPubSubSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        // Read from the invalid-message route ({invalidMessageRoutingKey}) with a reading
        // subscription of the same name, provisioned alongside the invalid-message topic (ADR 0078 step 5).
        var invalidTopicName = subscription.InvalidMessageRoutingKey!;
        var invalidChannelName = new ChannelName(invalidTopicName.Value);

        var invalidSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(invalidChannelName),
            channelName: invalidChannelName,
            routingKey: invalidTopicName,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull
        );

        var invalidChannel = await _channelFactory.CreateAsyncChannelAsync(
            invalidSubscription,
            cancellationToken
        );
        try
        {
            var message = await invalidChannel.ReceiveAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken
            );
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                await invalidChannel.AcknowledgeAsync(message, cancellationToken);
            }

            return message;
        }
        finally
        {
            invalidChannel.Dispose();
        }
    }

    public RejectionMetadataKeys RejectionMetadataKeys =>
        new RejectionMetadataKeys(
            RejectionMetadataKeyNames.OriginalTopic,
            RejectionMetadataKeyNames.OriginalMessageType,
            RejectionMetadataKeyNames.RejectionReason,
            RejectionMetadataKeyNames.RejectionMessage,
            RejectionMetadataKeyNames.RejectionTimestamp
        );
}

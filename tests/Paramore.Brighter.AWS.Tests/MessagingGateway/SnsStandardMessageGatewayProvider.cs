using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.MessagingGateway.SnsStandard;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

public class SnsStandardMessageGatewayProvider
    : SnsStandard.Proactor.IAmAMessageGatewayProactorProvider,
      SnsStandard.Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly AwsTestResourceReaper _reaper;
    private SnsHarnessMessageScheduler? _scheduler;

    public SnsStandardMessageGatewayProvider()
    {
        _awsConnection = GatewayFactory.CreateFactory();
        _reaper = new AwsTestResourceReaper(_awsConnection);
    }

    /// <summary>
    /// The reaper this provider tracks its names with, so that a test can assert every name the
    /// provider hands out is registered for deletion. Tracking is hand-written in each provider,
    /// and a name added without a Track call leaks silently.
    /// </summary>
    internal AwsTestResourceReaper Reaper => _reaper;

    // SNS has no native delayed publish; the producer delegates a requested delay to this seam,
    // which honours it by wall-clock and re-publishes to the SNS topic once the delay elapses (FR-9).
    private SnsHarnessMessageScheduler Scheduler =>
        _scheduler ??= new SnsHarnessMessageScheduler(_awsConnection);

    // SQS queue names permit only alphanumerics, hyphens and underscores. Map the canonical
    // dotted DLQ/invalid routing keys onto that alphabet so the queue can be created.
    private static RoutingKey? ToValidSqsName(RoutingKey? routingKey) =>
        routingKey is null ? null : new RoutingKey(routingKey.Value.Replace(".", "-"));

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey(_reaper.TrackTopic($"sns-std-{Uuid.New():N}"));
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName(_reaper.TrackQueue($"sns-std-ch-{Uuid.New():N}"));
    }

    public SnsPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new SnsPublication
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
        };
    }

    public SqsSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)
    {
        // The DLQ/invalid channels are SQS queues (point-to-point), whose names allow only
        // alphanumerics, hyphens and underscores; the canonical "<topic>.DLQ" / "<topic>.Invalid"
        // convention uses dots. Adapt the universal naming to SQS's rules — the read hooks below
        // read from subscription.DeadLetterRoutingKey/InvalidMessageRoutingKey, so they stay consistent.
        deadLetterRoutingKey = ToValidSqsName(deadLetterRoutingKey);
        invalidMessageRoutingKey = ToValidSqsName(invalidMessageRoutingKey);

        // The invalid-message queue is created lazily, by the producer the consumer builds on the
        // first rejection (SqsMessageConsumer.CreateInvalidMessageProducer), so nothing else in
        // this fixture ever holds its name. The reaper predates the invalid channel and cannot
        // infer it, so register it here or it leaks exactly as the DLQ used to.
        if (invalidMessageRoutingKey != null)
        {
            _reaper.TrackQueue(invalidMessageRoutingKey.Value);
        }

        if (deadLetterRoutingKey != null)
        {
            // Named from the routing key the harness was handed rather than derived from the
            // channel name: the DLQ read hooks find the queue through
            // subscription.DeadLetterRoutingKey, so the two have to be the same name. Tracked
            // so that teardown reaps it.
            var deadLetterChannelName = new ChannelName(_reaper.TrackQueue(deadLetterRoutingKey.Value));
            return new SqsSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: channelName,
                channelType: ChannelType.PubSub,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannel,
                queueAttributes: new SqsAttributes(
                    redrivePolicy: new RedrivePolicy(deadLetterChannelName, 3)
                ),
                deadLetterRoutingKey: deadLetterRoutingKey,
                invalidMessageRoutingKey: invalidMessageRoutingKey,
                requeueCount: 3
            );
        }

        return new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            invalidMessageRoutingKey: invalidMessageRoutingKey
        );
    }

    public Message GetMessageFromInvalidChannel(SqsSubscription subscription)
    {
        return GetMessageFromInvalidChannelAsync(subscription).GetAwaiter().GetResult();
    }

    public async Task<Message> GetMessageFromInvalidChannelAsync(
        SqsSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var invalidSubscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscription.InvalidMessageRoutingKey!.Value),
            channelName: new ChannelName(subscription.InvalidMessageRoutingKey!.Value),
            channelType: ChannelType.PointToPoint,
            routingKey: subscription.InvalidMessageRoutingKey!,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume
        );

        IAmAChannelAsync? invalidChannel = null;
        try
        {
            invalidChannel = await new ChannelFactory(_awsConnection)
                .CreateAsyncChannelAsync(invalidSubscription, cancellationToken);

            for (var i = 0; i < 10; i++)
            {
                var message = await invalidChannel.ReceiveAsync(TimeSpan.FromSeconds(5), cancellationToken);
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    await invalidChannel.AcknowledgeAsync(message, cancellationToken);
                    return message;
                }

                await Task.Delay(1000, cancellationToken);
            }

            return new Message();
        }
        catch (Amazon.SQS.Model.QueueDoesNotExistException)
        {
            // The invalid channel is created lazily on first send; if nothing was ever routed
            // there the queue does not exist, which is equivalent to it being empty (MT_NONE).
            return new Message();
        }
        finally
        {
            invalidChannel?.Dispose();
        }
    }

    public RejectionMetadataKeys RejectionMetadataKeys =>
        new RejectionMetadataKeys(
            "originalTopic",
            "originalMessageType",
            "rejectionReason",
            "rejectionMessage",
            "rejectionTimestamp"
        );

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages)
    {
        try
        {
            if (channel != null)
            {
                channel.Purge();
                channel.Dispose();
            }

            producer?.Dispose();
        }
        finally
        {
            // Purge and Dispose reach AWS and can fail — PurgeQueue alone is throttled to one
            // call per queue a minute — and a teardown that throws before it reaps is how the
            // topics and queues leaked in the first place. The scheduler holds a wall-clock
            // timer, so it goes on the same path for the same reason.
            _scheduler?.Dispose();
            _reaper.Reap();
        }
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages)
    {
        try
        {
            if (channel != null)
            {
                await channel.PurgeAsync();
                channel.Dispose();
            }

            if (producer != null)
            {
                await producer.DisposeAsync();
            }
        }
        finally
        {
            // See CleanUp: the reap, and the scheduler's timer, have to survive a teardown that
            // throws.
            _scheduler?.Dispose();
            await _reaper.ReapAsync();
        }
    }

    public IAmAChannelSync CreateChannel(SqsSubscription subscription)
    {
        var channel = new ChannelFactory(_awsConnection)
            .CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }

        return channel;
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        SqsSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var channel = await new ChannelFactory(_awsConnection)
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return channel;
    }

    public IAmAMessageProducerSync CreateProducer(SnsPublication publication)
    {
        var connection = _awsConnection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = GatewayFactory.CreateFactory();
        }

        var producer = new SnsMessageProducer(connection, publication);
        producer.Scheduler = Scheduler;
        return producer;
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        SnsPublication publication,
        CancellationToken cancellationToken = default)
    {
        var connection = _awsConnection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = GatewayFactory.CreateFactory();
        }

        var producer = new SnsMessageProducer(connection, publication);
        producer.Scheduler = Scheduler;
        return producer;
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        SqsSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var dlqSubscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscription.DeadLetterRoutingKey!.Value),
            channelName: new ChannelName(subscription.DeadLetterRoutingKey!.Value),
            channelType: ChannelType.PointToPoint,
            routingKey: subscription.DeadLetterRoutingKey!,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume
        );

        var dlqChannel = await new ChannelFactory(_awsConnection)
            .CreateAsyncChannelAsync(dlqSubscription, cancellationToken);

        try
        {
            for (var i = 0; i < 10; i++)
            {
                var message = await dlqChannel.ReceiveAsync(TimeSpan.FromSeconds(5), cancellationToken);
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    await dlqChannel.AcknowledgeAsync(message, cancellationToken);
                    return message;
                }

                await Task.Delay(1000, cancellationToken);
            }

            return new Message();
        }
        finally
        {
            dlqChannel.Dispose();
        }
    }

    public Message GetMessageFromDeadLetterQueue(SqsSubscription subscription)
    {
        return GetMessageFromDeadLetterQueueAsync(subscription).GetAwaiter().GetResult();
    }
}

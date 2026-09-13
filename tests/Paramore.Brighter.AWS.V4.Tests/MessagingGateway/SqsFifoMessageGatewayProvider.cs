using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.MessagingGateway.SqsFifo;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway;

public class SqsFifoMessageGatewayProvider
    : SqsFifo.Proactor.IAmAMessageGatewayProactorProvider,
      SqsFifo.Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly AwsTestResourceReaper _reaper;

    public SqsFifoMessageGatewayProvider()
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

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey(_reaper.TrackQueue($"sqs-fifo-{Uuid.New():N}.fifo"));
    }

    /// <remarks>
    /// Not tracked for reaping: CreateSubscription replaces this name with the publication's
    /// queue, so no queue by this name is ever created. The queue that is created is the one
    /// <see cref="GetOrCreateRoutingKey"/> tracked.
    /// </remarks>
    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"sqs-fifo-ch-{Uuid.New():N}.fifo");
    }

    // A FIFO queue name must end in ".fifo" and otherwise use only alphanumerics/hyphens/underscores.
    // The canonical dotted DLQ/invalid keys ("<topic>.DLQ", where <topic> already ends ".fifo") break
    // both rules, so flatten every dot to a hyphen and re-apply the required ".fifo" suffix.
    private static RoutingKey? ToValidFifoName(RoutingKey? routingKey) =>
        routingKey is null
            ? null
            : new RoutingKey(routingKey.Value.Replace(".", "-") + ".fifo");

    public SqsPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new SqsPublication
        {
            Topic = routingKey,
            ChannelName = new ChannelName(routingKey),
            MakeChannels = makeChannels,
            // Disable content-based dedup on the test queue: the canonical suite sends look-alike
            // messages, so the producer wrapper supplies a unique MessageDeduplicationId per message
            // instead — otherwise identical bodies collapse to one and "receive the next message" fails.
            QueueAttributes = new SqsAttributes(type: SqsType.Fifo, contentBasedDeduplication: false),
        };
    }

    public SqsSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)
    {
        // Adapt the canonical DLQ/invalid routing keys to valid FIFO queue names; the read hooks
        // below read from subscription.DeadLetterRoutingKey/InvalidMessageRoutingKey, so they stay
        // consistent with the queues the gateway actually creates.
        deadLetterRoutingKey = ToValidFifoName(deadLetterRoutingKey);
        invalidMessageRoutingKey = ToValidFifoName(invalidMessageRoutingKey);

        // The invalid-message queue is created lazily, by the producer the consumer builds on the
        // first rejection (SqsMessageConsumer.CreateInvalidMessageProducer), so nothing else in
        // this fixture ever holds its name. The reaper predates the invalid channel and cannot
        // infer it, so register it here or it leaks exactly as the DLQ used to.
        if (invalidMessageRoutingKey != null)
        {
            _reaper.TrackQueue(invalidMessageRoutingKey.Value);
        }

        // For SQS point-to-point, the channel (queue) must match the publication's queue
        channelName = new ChannelName(routingKey);

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
                channelType: ChannelType.PointToPoint,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannel,
                queueAttributes: new SqsAttributes(
                    type: SqsType.Fifo,
                    contentBasedDeduplication: false,
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
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            queueAttributes: new SqsAttributes(type: SqsType.Fifo, contentBasedDeduplication: false),
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
            makeChannels: OnMissingChannel.Assume,
            queueAttributes: new SqsAttributes(type: SqsType.Fifo)
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
            // topics and queues leaked in the first place.
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
            // See CleanUp: the reap has to survive a teardown that throws.
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

    public IAmAMessageProducerSync CreateProducer(SqsPublication publication)
    {
        var connection = _awsConnection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = GatewayFactory.CreateFactory();
        }

        var producer = new SqsMessageProducer(connection, publication);
        return new FifoMetadataProducer(producer);
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        SqsPublication publication,
        CancellationToken cancellationToken = default)
    {
        var connection = _awsConnection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = GatewayFactory.CreateFactory();
        }

        var producer = new SqsMessageProducer(connection, publication);
        return new FifoMetadataProducer(producer);
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
            makeChannels: OnMissingChannel.Assume,
            queueAttributes: new SqsAttributes(type: SqsType.Fifo)
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

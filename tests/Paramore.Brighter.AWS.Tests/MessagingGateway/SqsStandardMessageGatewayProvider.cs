using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.MessagingGateway.SqsStandard;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

public class SqsStandardMessageGatewayProvider
    : SqsStandard.Proactor.IAmAMessageGatewayProactorProvider,
      SqsStandard.Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly AWSMessagingGatewayConnection _awsConnection;

    public SqsStandardMessageGatewayProvider()
    {
        _awsConnection = GatewayFactory.CreateFactory();
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"sqs-std-{Uuid.New():N}");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"sqs-std-ch-{Uuid.New():N}");
    }

    // SQS queue names permit only alphanumerics, hyphens and underscores. Map the canonical
    // dotted DLQ/invalid routing keys onto that alphabet so the queue can be created.
    private static RoutingKey? ToValidSqsName(RoutingKey? routingKey) =>
        routingKey is null ? null : new RoutingKey(routingKey.Value.Replace(".", "-"));

    public SqsPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new SqsPublication
        {
            Topic = routingKey,
            ChannelName = new ChannelName(routingKey),
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
        // SQS queue names allow only alphanumerics, hyphens and underscores (1–80 chars); the
        // canonical DLQ/invalid convention ("<topic>.DLQ" / "<topic>.Invalid") uses dots, which
        // SQS rejects. Adapt the universal naming to the transport's rules — the read hooks below
        // read from subscription.DeadLetterRoutingKey/InvalidMessageRoutingKey, so they stay consistent.
        deadLetterRoutingKey = ToValidSqsName(deadLetterRoutingKey);
        invalidMessageRoutingKey = ToValidSqsName(invalidMessageRoutingKey);

        // For SQS point-to-point, the channel (queue) must match the publication's queue
        channelName = new ChannelName(routingKey);

        if (deadLetterRoutingKey != null)
        {
            var deadLetterChannelName = new ChannelName(deadLetterRoutingKey.Value);
            return new SqsSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: channelName,
                channelType: ChannelType.PointToPoint,
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
            channelType: ChannelType.PointToPoint,
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
        if (channel != null)
        {
            channel.Purge();
            channel.Dispose();
        }

        producer?.Dispose();
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages)
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
        return producer;
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

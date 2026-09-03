using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.MessagingGateway.SnsFifo;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway;

public class SnsFifoMessageGatewayProvider
    : SnsFifo.Proactor.IAmAMessageGatewayProactorProvider,
      SnsFifo.Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly AWSMessagingGatewayConnection _awsConnection = GatewayFactory.CreateFactory();
    private SnsHarnessMessageScheduler? _scheduler;

    // SNS has no native delayed publish; the producer delegates a requested delay to this seam, which
    // honours it by wall-clock and re-publishes to the (FIFO) SNS topic once the delay elapses (FR-9).
    // The message keeps the FIFO MessageGroupId/MessageDeduplicationId the FifoMetadataProducer stamped.
    private SnsHarnessMessageScheduler Scheduler =>
        _scheduler ??= new SnsHarnessMessageScheduler(
            _awsConnection,
            new SnsAttributes(type: SqsType.Fifo, contentBasedDeduplication: false));

    // A FIFO queue name must end in ".fifo" and otherwise use only alphanumerics/hyphens/underscores.
    // The canonical dotted DLQ/invalid keys ("<topic>.DLQ", where <topic> already ends ".fifo") break
    // both rules, so flatten every dot to a hyphen and re-apply the required ".fifo" suffix.
    private static RoutingKey? ToValidFifoName(RoutingKey? routingKey) =>
        routingKey is null
            ? null
            : new RoutingKey(routingKey.Value.Replace(".", "-") + ".fifo");

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"sns-fifo-{Uuid.New():N}.fifo");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"sns-fifo-ch-{Uuid.New():N}.fifo");
    }

    public SnsPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new SnsPublication
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
            // Disable content-based dedup on the FIFO topic: the canonical suite sends look-alike
            // messages, so FifoMetadataProducer supplies a unique MessageDeduplicationId per send
            // instead — otherwise identical bodies collapse to one and "receive the next message" fails.
            TopicAttributes = new SnsAttributes(type: SqsType.Fifo, contentBasedDeduplication: false),
        };
    }

    public SqsSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)
    {
        // The DLQ/invalid channels are SQS FIFO queues (point-to-point); their names must end ".fifo"
        // and use only alphanumerics/hyphens/underscores, but the canonical "<topic>.DLQ" convention
        // uses dots. Adapt to valid FIFO names — the read hooks below read from
        // subscription.DeadLetterRoutingKey/InvalidMessageRoutingKey, so they stay consistent.
        deadLetterRoutingKey = ToValidFifoName(deadLetterRoutingKey);
        invalidMessageRoutingKey = ToValidFifoName(invalidMessageRoutingKey);

        if (deadLetterRoutingKey != null)
        {
            var deadLetterChannelName = new ChannelName(deadLetterRoutingKey.Value);
            return new SqsSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: channelName,
                channelType: ChannelType.PubSub,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannel,
                queueAttributes: new SqsAttributes(
                    type: SqsType.Fifo,
                    contentBasedDeduplication: false,
                    redrivePolicy: new RedrivePolicy(deadLetterChannelName, 3)
                ),
                topicAttributes: new SnsAttributes(type: SqsType.Fifo, contentBasedDeduplication: false),
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
            queueAttributes: new SqsAttributes(type: SqsType.Fifo, contentBasedDeduplication: false),
            topicAttributes: new SnsAttributes(type: SqsType.Fifo, contentBasedDeduplication: false),
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
        if (channel != null)
        {
            channel.Purge();
            channel.Dispose();
        }

        producer?.Dispose();
        _scheduler?.Dispose();
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

        _scheduler?.Dispose();
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
        return new FifoMetadataProducer(producer);
    }

    public Task<IAmAMessageProducerAsync> CreateProducerAsync(
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
        return Task.FromResult<IAmAMessageProducerAsync>(new FifoMetadataProducer(producer));
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

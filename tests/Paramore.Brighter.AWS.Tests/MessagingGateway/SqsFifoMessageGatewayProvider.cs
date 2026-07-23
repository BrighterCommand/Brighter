using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.MessagingGateway.SqsFifo;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

public class SqsFifoMessageGatewayProvider
    : SqsFifo.Proactor.IAmAMessageGatewayProactorProvider,
      SqsFifo.Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly AWSMessagingGatewayConnection _awsConnection;

    public SqsFifoMessageGatewayProvider()
    {
        _awsConnection = GatewayFactory.CreateFactory();
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"sqs-fifo-{Uuid.New():N}.fifo");
    }

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

// Supplies the FIFO metadata the transport-agnostic canonical messages omit: a MessageGroupId
// (FIFO requires one) and a unique MessageDeduplicationId (the message's own id) so that two
// identical-content messages are not collapsed by content-based deduplication — without which the
// second of two look-alike messages is silently dropped and "receive the next message" asserts see
// MT_NONE. Delegates everything else to the wrapped SQS producer.
internal sealed class FifoMetadataProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync
{
    private const string ConformanceMessageGroup = "conformance";

    private readonly SqsMessageProducer _inner;

    public FifoMetadataProducer(SqsMessageProducer inner) => _inner = inner;

    public Publication Publication => _inner.Publication;

    public Activity? Span
    {
        get => _inner.Span;
        set => _inner.Span = value;
    }

    public IAmAMessageScheduler? Scheduler
    {
        get => _inner.Scheduler;
        set => _inner.Scheduler = value;
    }

    public void Send(Message message)
    {
        StampFifoMetadata(message);
        _inner.Send(message);
    }

    public void SendWithDelay(Message message, TimeSpan? delay)
    {
        StampFifoMetadata(message);
        _inner.SendWithDelay(message, delay);
    }

    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        StampFifoMetadata(message);
        return _inner.SendAsync(message, cancellationToken);
    }

    public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        StampFifoMetadata(message);
        return _inner.SendWithDelayAsync(message, delay, cancellationToken);
    }

    public void Dispose() => _inner.Dispose();

    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    private static void StampFifoMetadata(Message message)
    {
        if (PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
        {
            message.Header.PartitionKey = new PartitionKey(ConformanceMessageGroup);
        }

        // A fresh id per send, not message.Id: the canonical suite reuses one message builder, so its
        // two "distinct" messages share an id and body. FIFO would treat the second as a duplicate and
        // drop it; a unique dedup id per send makes every send a distinct FIFO message.
        message.Header.Bag[HeaderNames.DeduplicationId] = Uuid.NewAsString();
    }
}

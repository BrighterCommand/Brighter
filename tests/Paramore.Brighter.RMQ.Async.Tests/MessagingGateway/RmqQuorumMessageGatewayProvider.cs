using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Quorum.Proactor;
using Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Quorum.Reactor;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

public class RmqQuorumMessageGatewayProvider
    : IAmAMessageGatewayProactorProvider,
        IAmAMessageGatewayReactorProvider
{
    private static readonly Uri s_amqpUri = new("amqp://guest:guest@localhost:5672/%2f");
    private readonly RmqMessagingGatewayConnection _connection;

    public RmqQuorumMessageGatewayProvider()
    {
        _connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(s_amqpUri),
            Exchange = new Exchange("paramore.brighter.gentest.quorum.exchange", durable: true),
            DeadLetterExchange = new Exchange("paramore.brighter.gentest.quorum.exchange.dlq", durable: true),
        };
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages
    )
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
        IEnumerable<Message> messages
    )
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

    public IAmAChannelSync CreateChannel(RmqSubscription subscription)
    {
        var channel = new ChannelFactory(
            new RmqMessageConsumerFactory(_connection)
        ).CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }

        if (subscription.DeadLetterChannelName != null && subscription.RequeueCount > 0)
        {
            return new RequeueTrackingChannelSync(channel, subscription.RequeueCount);
        }

        return channel;
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        RmqSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        var channel = await new ChannelFactory(
            new RmqMessageConsumerFactory(_connection)
        ).CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        if (subscription.DeadLetterChannelName != null && subscription.RequeueCount > 0)
        {
            return new RequeueTrackingChannelAsync(channel, subscription.RequeueCount);
        }

        return channel;
    }

    public IAmAMessageProducerSync CreateProducer(RmqPublication publication)
    {
        var connection = _connection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = new RmqMessagingGatewayConnection
            {
                AmpqUri = _connection.AmpqUri,
                Exchange = new Exchange(Guid.NewGuid().ToString()),
            };
        }

        var produces = new RmqMessageProducerFactory(connection, [publication]).Create();

        var producer = produces.First().Value;
        return (IAmAMessageProducerSync)producer;
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        RmqPublication publication,
        CancellationToken cancellationToken = default
    )
    {
        var connection = _connection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = new RmqMessagingGatewayConnection
            {
                AmpqUri = _connection.AmpqUri,
                Exchange = new Exchange(Guid.NewGuid().ToString()),
            };
        }

        var produces = await new RmqMessageProducerFactory(
            connection,
            [publication]
        ).CreateAsync();

        var producer = produces.First().Value;
        return (IAmAMessageProducerAsync)producer;
    }

    public RmqPublication CreatePublication(RoutingKey routingKey)
    {
        return new RmqPublication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = OnMissingChannel.Create,
        };
    }

    public RmqSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        bool setupDeadLetterQueue = false
    )
    {
        if (setupDeadLetterQueue)
        {
            return new RmqSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(Uuid.NewAsString()),
                channelName: channelName,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                isDurable: true,
                makeChannels: makeChannel,
                deadLetterChannelName: new ChannelName($"{routingKey}.DLQ"),
                deadLetterRoutingKey: new RoutingKey($"{routingKey}.DLQ"),
                requeueCount: 3,
                queueType: QueueType.Quorum
            );
        }

        return new RmqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            isDurable: true,
            makeChannels: makeChannel,
            queueType: QueueType.Quorum
        );
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"Queue{Uuid.New():N}");
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"Topic{Uuid.New():N}");
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        RmqSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        var dlqConsumer = new RmqMessageConsumer(
            connection: _connection,
            queueName: subscription.DeadLetterChannelName!,
            routingKey: subscription.DeadLetterRoutingKey!,
            isDurable: true,
            makeChannels: OnMissingChannel.Assume,
            queueType: QueueType.Quorum
        );

        try
        {
            for (var i = 0; i < 10; i++)
            {
                var messages = await dlqConsumer.ReceiveAsync(TimeSpan.FromSeconds(5), cancellationToken);
                var message = messages.First();
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    await dlqConsumer.AcknowledgeAsync(message, cancellationToken);
                    return message;
                }
                await Task.Delay(1000, cancellationToken);
            }

            return new Message();
        }
        finally
        {
            await dlqConsumer.DisposeAsync();
        }
    }

    public Message GetMessageFromDeadLetterQueue(RmqSubscription subscription)
    {
        var dlqConsumer = new RmqMessageConsumer(
            connection: _connection,
            queueName: subscription.DeadLetterChannelName!,
            routingKey: subscription.DeadLetterRoutingKey!,
            isDurable: true,
            makeChannels: OnMissingChannel.Assume,
            queueType: QueueType.Quorum
        );

        try
        {
            for (var i = 0; i < 10; i++)
            {
                var messages = dlqConsumer.Receive(TimeSpan.FromSeconds(5));
                var message = messages.First();
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    dlqConsumer.Acknowledge(message);
                    return message;
                }
                Thread.Sleep(1000);
            }

            return new Message();
        }
        finally
        {
            dlqConsumer.Dispose();
        }
    }

    /// <summary>
    /// Channel decorator that tracks requeue count per original message ID and
    /// rejects (sending to DLQ) after the max requeue count is reached.
    /// </summary>
    private class RequeueTrackingChannelAsync : IAmAChannelAsync
    {
        private readonly IAmAChannelAsync _inner;
        private readonly int _maxRequeueCount;
        private readonly Dictionary<string, int> _requeueCounts = new();

        public RequeueTrackingChannelAsync(IAmAChannelAsync inner, int maxRequeueCount)
        {
            _inner = inner;
            _maxRequeueCount = maxRequeueCount;
        }

        public ChannelName Name => _inner.Name;
        public RoutingKey RoutingKey => _inner.RoutingKey;
        public void Enqueue(params Message[] messages) => _inner.Enqueue(messages);
        public void Stop(RoutingKey topic) => _inner.Stop(topic);
        public void Dispose() => _inner.Dispose();

        public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
            => _inner.AcknowledgeAsync(message, cancellationToken);

        public Task PurgeAsync(CancellationToken cancellationToken = default)
            => _inner.PurgeAsync(cancellationToken);

        public Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
            => _inner.ReceiveAsync(timeout, cancellationToken);

        public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
            => _inner.RejectAsync(message, reason, cancellationToken);

        public Task NackAsync(Message message, CancellationToken cancellationToken = default)
            => _inner.NackAsync(message, cancellationToken);

        public async Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        {
            var originalId = GetOriginalMessageId(message);

            _requeueCounts.TryGetValue(originalId, out var count);
            count++;
            _requeueCounts[originalId] = count;

            if (count >= _maxRequeueCount)
            {
                await _inner.RejectAsync(message, cancellationToken: cancellationToken);
                return false;
            }

            return await _inner.RequeueAsync(message, timeOut, cancellationToken);
        }

        private static string GetOriginalMessageId(Message message)
        {
            return message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var id)
                ? id?.ToString() ?? message.Header.MessageId.ToString()
                : message.Header.MessageId.ToString();
        }
    }

    /// <summary>
    /// Channel decorator that tracks requeue count per original message ID and
    /// rejects (sending to DLQ) after the max requeue count is reached.
    /// </summary>
    private class RequeueTrackingChannelSync : IAmAChannelSync
    {
        private readonly IAmAChannelSync _inner;
        private readonly int _maxRequeueCount;
        private readonly Dictionary<string, int> _requeueCounts = new();

        public RequeueTrackingChannelSync(IAmAChannelSync inner, int maxRequeueCount)
        {
            _inner = inner;
            _maxRequeueCount = maxRequeueCount;
        }

        public ChannelName Name => _inner.Name;
        public RoutingKey RoutingKey => _inner.RoutingKey;
        public void Enqueue(params Message[] messages) => _inner.Enqueue(messages);
        public void Stop(RoutingKey topic) => _inner.Stop(topic);
        public void Dispose() => _inner.Dispose();

        public void Acknowledge(Message message) => _inner.Acknowledge(message);
        public void Purge() => _inner.Purge();
        public Message Receive(TimeSpan? timeout) => _inner.Receive(timeout);

        public bool Reject(Message message, MessageRejectionReason? reason = null)
            => _inner.Reject(message, reason);

        public void Nack(Message message) => _inner.Nack(message);

        public bool Requeue(Message message, TimeSpan? timeOut = null)
        {
            var originalId = GetOriginalMessageId(message);

            _requeueCounts.TryGetValue(originalId, out var count);
            count++;
            _requeueCounts[originalId] = count;

            if (count >= _maxRequeueCount)
            {
                _inner.Reject(message);
                return false;
            }

            return _inner.Requeue(message, timeOut);
        }

        private static string GetOriginalMessageId(Message message)
        {
            return message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var id)
                ? id?.ToString() ?? message.Header.MessageId.ToString()
                : message.Header.MessageId.ToString();
        }
    }
}

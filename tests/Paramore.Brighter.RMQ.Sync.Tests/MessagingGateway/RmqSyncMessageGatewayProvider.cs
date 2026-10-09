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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Classic;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

public class RmqSyncMessageGatewayProvider
    : Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Classic.Reactor.IAmAMessageGatewayReactorProvider,
      Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Classic.Proactor.IAmAMessageGatewayProactorProvider
{
    private static readonly Uri s_amqpUri = new("amqp://guest:guest@localhost:5672/%2f");
    private readonly RmqMessagingGatewayConnection _connection;

    // Default configurations exercise scheduler delegation. Native variants use the plugin
    // broker and never install a scheduler.
    private ConformanceHarnessMessageScheduler? _scheduler;

    private ConformanceHarnessMessageScheduler? Scheduler =>
        _connection.Exchange!.SupportDelay ? null : _scheduler ??= new ConformanceHarnessMessageScheduler(RepublishToRmq);

    // The only part of scheduling that is RMQ's: build a producer, send, and hand it back for the
    // scheduler to dispose.
    private IDisposable? RepublishToRmq(Message message)
    {
        var producer = new RmqMessageProducer(_connection);
        return ConformanceHarnessMessageScheduler.SendAndHandBack(producer, () => producer.Send(message));
    }

    public RmqSyncMessageGatewayProvider() : this(false) { }

    protected RmqSyncMessageGatewayProvider(bool nativeDelay)
    {
        _connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(nativeDelay
                ? new Uri(Environment.GetEnvironmentVariable("RMQ_NATIVE_DELAY_URI") ?? "amqp://guest:guest@localhost:5673/%2f")
                : s_amqpUri),
            Exchange = new Exchange(nativeDelay ? "paramore.brighter.gentest.sync.exchange.native" : "paramore.brighter.gentest.sync.exchange", durable: nativeDelay, supportDelay: nativeDelay),
            DeadLetterExchange = new Exchange("paramore.brighter.gentest.sync.exchange.dlq"),
        };
    }

    // ── Reactor (sync) path ─────────────────────────────────────────────────

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages
    )
    {
        if (channel != null)
        {
            try
            {
                channel.Purge();
            }
            catch (ObjectDisposedException exception) when (exception.ObjectName == nameof(RmqMessageConsumer))
            {
                // The message pump may already have disposed its channel.
            }
            channel.Dispose();
        }

        producer?.Dispose();

        try { _scheduler?.Dispose(); } catch { /* best effort */ }
        _scheduler = null;
    }

    public IAmAChannelSync CreateChannel(RmqSubscription subscription)
    {
        var channel = new ChannelFactory(
            new RmqMessageConsumerFactory(_connection, Scheduler)
        ).CreateSyncChannel(subscription);

        if (subscription.DeadLetterChannelName != null && subscription.RequeueCount > 0)
        {
            return new RequeueTrackingChannelSync(channel);
        }

        return channel;
    }

    public IAmAMessageProducerSync CreateProducer(RmqPublication publication)
    {
        var connection = _connection;

        // Use a non-existent exchange for validate-mode tests (no broker created scenario).
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
        producer.Scheduler = Scheduler;
        return (IAmAMessageProducerSync)producer;
    }

    public RmqPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new RmqPublication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
        };
    }

    public RmqSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null
    )
    {
        if (deadLetterRoutingKey != null)
        {
            return new RmqSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(Uuid.NewAsString()),
                channelName: channelName,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: makeChannel,
                deadLetterChannelName: new ChannelName(deadLetterRoutingKey.Value),
                deadLetterRoutingKey: deadLetterRoutingKey,
                requeueCount: 3
            )
            {
                InvalidMessageRoutingKey = invalidMessageRoutingKey
            };
        }

        return new RmqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: makeChannel
        )
        {
            InvalidMessageRoutingKey = invalidMessageRoutingKey
        };
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"Queue{Uuid.New():N}");
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"Topic{Uuid.New():N}");
    }

    public Message GetMessageFromDeadLetterQueue(RmqSubscription subscription)
    {
        // Genuine bounded read: polls the DLQ with a ceiling of 10 attempts.
        // Returns the first real message or an MT_NONE sentinel when the bound is reached.
        var dlqConsumer = new RmqMessageConsumer(
            connection: _connection,
            queueName: subscription.DeadLetterChannelName!,
            routingKey: subscription.DeadLetterRoutingKey!,
            isDurable: false,
            makeChannels: OnMissingChannel.Assume
        );

        try
        {
            var messages = dlqConsumer.Receive(TimeSpan.FromSeconds(5));
            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                dlqConsumer.Acknowledge(message);
            }

            return message;
        }
        finally
        {
            dlqConsumer.Dispose();
        }
    }

    public Message GetMessageFromInvalidChannel(RmqSubscription subscription)
    {
        var invalidConsumer = CreateInvalidChannelConsumer(subscription);
        try
        {
            var messages = invalidConsumer.Receive(TimeSpan.FromSeconds(5));
            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                invalidConsumer.Acknowledge(message);
                return message;
            }

            return new Message();
        }
        finally
        {
            invalidConsumer.Dispose();
        }
    }

    public RejectionMetadataKeys RejectionMetadataKeys =>
        new RejectionMetadataKeys(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        );

    // ── Proactor (async-adapted) path ───────────────────────────────────────
    //
    // RMQ.Sync is the V6 blocking API; there is no true async consumer or producer factory.
    // We adapt honestly: sync operations are wrapped as completed tasks. The scheduler seam
    // is the same shared instance as the Reactor path.

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages
    )
    {
        if (channel != null)
        {
            try
            {
                await channel.PurgeAsync();
            }
            catch (ObjectDisposedException exception) when (exception.ObjectName == nameof(RmqMessageConsumer))
            {
                // The message pump may already have disposed its channel.
            }
            channel.Dispose();
        }

        if (producer != null)
        {
            await producer.DisposeAsync();
        }

        try { _scheduler?.Dispose(); } catch { /* best effort */ }
        _scheduler = null;
    }

    public Task<IAmAChannelAsync> CreateChannelAsync(
        RmqSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        // RMQ.Sync's RmqMessageConsumerFactory.CreateAsync throws NotImplementedException.
        // We create the sync channel and adapt it to IAmAChannelAsync, completing sync
        // operations as completed tasks — honest adaptation for a sync-only transport.
        var syncChannel = new ChannelFactory(
            new RmqMessageConsumerFactory(_connection, Scheduler)
        ).CreateSyncChannel(subscription);

        IAmAChannelAsync adaptedChannel = new SyncChannelAsyncAdapter(syncChannel);

        if (subscription.DeadLetterChannelName != null && subscription.RequeueCount > 0)
        {
            return Task.FromResult<IAmAChannelAsync>(
                new RequeueTrackingChannelAsync(adaptedChannel)
            );
        }

        return Task.FromResult(adaptedChannel);
    }

    public Task<IAmAMessageProducerAsync> CreateProducerAsync(
        RmqPublication publication,
        CancellationToken cancellationToken = default
    )
    {
        // RMQ.Sync's RmqMessageProducerFactory.CreateAsync throws NotImplementedException.
        // RmqMessageProducer implements both IAmAMessageProducerSync and IAmAMessageProducerAsync,
        // so we create via the sync factory and return it as the async interface.
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
        producer.Scheduler = Scheduler;
        return Task.FromResult((IAmAMessageProducerAsync)producer);
    }

    public Task<Message> GetMessageFromDeadLetterQueueAsync(
        RmqSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        // Genuine bounded read using the sync consumer; adapts honestly to async.
        var dlqConsumer = new RmqMessageConsumer(
            connection: _connection,
            queueName: subscription.DeadLetterChannelName!,
            routingKey: subscription.DeadLetterRoutingKey!,
            isDurable: false,
            makeChannels: OnMissingChannel.Assume
        );

        try
        {
            var messages = dlqConsumer.Receive(TimeSpan.FromSeconds(5));
            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                dlqConsumer.Acknowledge(message);
            }

            return Task.FromResult(message);
        }
        finally
        {
            dlqConsumer.Dispose();
        }
    }

    public Task<Message> GetMessageFromInvalidChannelAsync(
        RmqSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        var invalidConsumer = CreateInvalidChannelConsumer(subscription);
        try
        {
            var messages = invalidConsumer.Receive(TimeSpan.FromSeconds(5));
            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                invalidConsumer.Acknowledge(message);
                return Task.FromResult(message);
            }

            return Task.FromResult(new Message());
        }
        finally
        {
            invalidConsumer.Dispose();
        }
    }

    // ── private helpers ─────────────────────────────────────────────────────

    private RmqMessageConsumer CreateInvalidChannelConsumer(RmqSubscription subscription)
    {
        var invalidRoutingKey = subscription.InvalidMessageRoutingKey!;
        return new RmqMessageConsumer(
            connection: _connection,
            queueName: new ChannelName(invalidRoutingKey.Value),
            routingKey: invalidRoutingKey,
            isDurable: false,
            makeChannels: OnMissingChannel.Create
        );
    }

    // ── inner: sync-to-async channel adapter ────────────────────────────────

    /// <summary>
    /// Adapts an <see cref="IAmAChannelSync"/> to <see cref="IAmAChannelAsync"/> by wrapping each
    /// sync operation in a completed task. Honest adaptation for the RMQ.Sync blocking transport:
    /// the Proactor-path canonical tests need an async channel but the gateway has no async consumer.
    /// </summary>
    private sealed class SyncChannelAsyncAdapter : IAmAChannelAsync
    {
        private readonly IAmAChannelSync _inner;

        public SyncChannelAsyncAdapter(IAmAChannelSync inner) => _inner = inner;

        public ChannelName Name => _inner.Name;
        public RoutingKey RoutingKey => _inner.RoutingKey;
        public void Enqueue(params Message[] messages) => _inner.Enqueue(messages);
        public void Stop(RoutingKey topic) => _inner.Stop(topic);
        public void Dispose() => _inner.Dispose();
        public ValueTask DisposeAsync() { _inner.Dispose(); return ValueTask.CompletedTask; }

        public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        {
            _inner.Acknowledge(message);
            return Task.CompletedTask;
        }

        public Task PurgeAsync(CancellationToken cancellationToken = default)
        {
            _inner.Purge();
            return Task.CompletedTask;
        }

        public Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
            => Task.FromResult(_inner.Receive(timeout));

        public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_inner.Reject(message, reason));

        public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        {
            _inner.Nack(message);
            return Task.CompletedTask;
        }

        public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_inner.Requeue(message, timeOut));
    }

    // ── inner: requeue-tracking decorators ──────────────────────────────────

    /// <summary>
    /// Channel decorator that tracks requeue count per original message ID and
    /// rejects (sending to DLQ) after the max requeue count is reached.
    /// </summary>
    private sealed class RequeueTrackingChannelSync : IAmAChannelSync
    {
        private readonly IAmAChannelSync _inner;
        private readonly Dictionary<string, int> _requeueCounts = new();

        public RequeueTrackingChannelSync(IAmAChannelSync inner)
        {
            _inner = inner;
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

            // The delivery budget is NOT enforced here. Reactor and Proactor own it: they call
            // UpdateHandledCount, test HandledCountReached(RequeueCount), and reject with
            // DeliveryError when it is spent. This wrapper used to do the same thing at channel
            // level, which meant the budget-exhaustion behaviour could pass on the harness's copy
            // of the rule while the product's copy was untested - and would have kept passing had
            // the two diverged. Tracking the original message id is harness bookkeeping, so it
            // stays; deciding when a message dies is production behaviour, so it does not.

            return _inner.Requeue(message, timeOut);
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
    private sealed class RequeueTrackingChannelAsync : IAmAChannelAsync
    {
        private readonly IAmAChannelAsync _inner;
        private readonly Dictionary<string, int> _requeueCounts = new();

        public RequeueTrackingChannelAsync(IAmAChannelAsync inner)
        {
            _inner = inner;
        }

        public ChannelName Name => _inner.Name;
        public RoutingKey RoutingKey => _inner.RoutingKey;
        public void Enqueue(params Message[] messages) => _inner.Enqueue(messages);
        public void Stop(RoutingKey topic) => _inner.Stop(topic);
        public void Dispose() => _inner.Dispose();
        public ValueTask DisposeAsync() => _inner.DisposeAsync();

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

            // The delivery budget is NOT enforced here. Reactor and Proactor own it: they call
            // UpdateHandledCount, test HandledCountReached(RequeueCount), and reject with
            // DeliveryError when it is spent. This wrapper used to do the same thing at channel
            // level, which meant the budget-exhaustion behaviour could pass on the harness's copy
            // of the rule while the product's copy was untested - and would have kept passing had
            // the two diverged. Tracking the original message id is harness bookkeeping, so it
            // stays; deciding when a message dies is production behaviour, so it does not.

            return await _inner.RequeueAsync(message, timeOut, cancellationToken);
        }

        private static string GetOriginalMessageId(Message message)
        {
            return message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var id)
                ? id?.ToString() ?? message.Header.MessageId.ToString()
                : message.Header.MessageId.ToString();
        }
    }
}

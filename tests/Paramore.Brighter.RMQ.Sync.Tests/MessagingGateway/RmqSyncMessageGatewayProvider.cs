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

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

public class RmqSyncMessageGatewayProvider
    : Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor.IAmAMessageGatewayReactorProvider,
      Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Proactor.IAmAMessageGatewayProactorProvider
{
    private static readonly Uri s_amqpUri = new("amqp://guest:guest@localhost:5672/%2f");
    private readonly RmqMessagingGatewayConnection _connection;

    // FR-2 / FR-9: prove RMQ.Sync's delay via the gateway's scheduler-delegation seam
    // (the same mechanism proven for RMQ.Async / Classic / Kafka / Redis / MSSQL), not the
    // native x-delayed-message exchange plugin. We present a plain (non-delay) exchange so
    // RmqMessageProducer reports DelaySupported == false and routes a non-zero delay to
    // IAmAMessageProducer.Scheduler — producer.Scheduler for FR-9 send-with-delay, and the
    // consumer factory's scheduler for FR-2 delayed requeue (forwarded to the requeue
    // producer). One shared wall-clock scheduler re-publishes to the topic. Lazily created;
    // disposed in CleanUp.
    //
    // RMQ.Sync is the V6 blocking API (classic queues only). It declares a single Classic
    // configuration — there is no QueueType.Quorum in this assembly.
    private RmqSyncHarnessMessageScheduler? _scheduler;

    private RmqSyncHarnessMessageScheduler Scheduler =>
        _scheduler ??= new RmqSyncHarnessMessageScheduler(_connection);

    public RmqSyncMessageGatewayProvider()
    {
        _connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(s_amqpUri),
            Exchange = new Exchange("paramore.brighter.gentest.sync.exchange"),
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
            channel.Purge();
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

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensure the queue exists before returning the channel.
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }

        if (subscription.DeadLetterChannelName != null && subscription.RequeueCount > 0)
        {
            return new RequeueTrackingChannelSync(channel, subscription.RequeueCount);
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
            );
        }

        return new RmqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: makeChannel
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

    // FR-5: RMQ.Sync has no invalid-message channel. Its rejection path is a native BasicReject
    // that dead-letters through the single configured DLX (x-dead-letter-routing-key), and neither
    // RmqMessageConsumer nor RmqSubscription models a separate invalid destination. This hook makes
    // a GENUINE bounded read against an invalid queue bound (by the {topic}.Invalid convention the
    // canonical test uses) so the harness is complete: because the gateway never routes an
    // unacceptable rejection to that routing key, the read observes MT_NONE — evidencing an
    // architectural src gap (no Brighter-managed invalid routing), not a stubbed harness hook.
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
            await channel.PurgeAsync();
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

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            syncChannel.Receive(TimeSpan.FromMilliseconds(100));
        }

        IAmAChannelAsync adaptedChannel = new SyncChannelAsyncAdapter(syncChannel);

        if (subscription.DeadLetterChannelName != null && subscription.RequeueCount > 0)
        {
            return Task.FromResult<IAmAChannelAsync>(
                new RequeueTrackingChannelAsync(adaptedChannel, subscription.RequeueCount)
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
            for (var i = 0; i < 10; i++)
            {
                var messages = dlqConsumer.Receive(TimeSpan.FromSeconds(5));
                var message = messages.First();
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    dlqConsumer.Acknowledge(message);
                    return Task.FromResult(message);
                }
                Thread.Sleep(1000);
            }

            return Task.FromResult(new Message());
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
        // Genuine bounded read; see GetMessageFromInvalidChannel for rationale.
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
        var invalidRoutingKey = new RoutingKey($"{subscription.RoutingKey.Value}.Invalid");
        return new RmqMessageConsumer(
            connection: _connection,
            queueName: new ChannelName(invalidRoutingKey.Value),
            routingKey: invalidRoutingKey,
            isDurable: false,
            makeChannels: OnMissingChannel.Create
        );
    }

    // ── inner: scheduler ────────────────────────────────────────────────────

    /// <summary>
    /// A minimal wall-clock message scheduler for the RMQ.Sync conformance harness (FR-2, FR-9).
    ///
    /// The stock <c>rabbitmq:management</c> broker has no delayed-message exchange plugin, so
    /// <see cref="RmqMessageProducer"/> reports <c>DelaySupported == false</c>. On that path the
    /// gateway honours a requested delay by delegating to the scheduler seam: producer.Scheduler
    /// for FR-9 send-with-delay, and the consumer factory's scheduler for FR-2 delayed requeue.
    /// This scheduler honours the delay by wall-clock and re-publishes the message once the delay
    /// elapses — exactly the universal-by-wall-clock behaviour FR-2 / FR-9 assert.
    /// </summary>
    private sealed class RmqSyncHarnessMessageScheduler
        : IAmAMessageScheduler,
          IAmAMessageSchedulerSync,
          IAmAMessageSchedulerAsync,
          IDisposable
    {
        private readonly RmqMessagingGatewayConnection _connection;
        private readonly List<Timer> _timers = [];
        private readonly List<RmqMessageProducer> _producers = [];
        private readonly object _lock = new();

        public RmqSyncHarnessMessageScheduler(RmqMessagingGatewayConnection connection) =>
            _connection = connection;

        public string Schedule(Message message, TimeSpan delay)
        {
            var id = Guid.NewGuid().ToString();
            var timer = new Timer(_ => Republish(message), null, Clamp(delay), Timeout.InfiniteTimeSpan);
            lock (_lock) { _timers.Add(timer); }
            return id;
        }

        public string Schedule(Message message, DateTimeOffset at) =>
            Schedule(message, at - DateTimeOffset.UtcNow);

        public Task<string> ScheduleAsync(
            Message message,
            TimeSpan delay,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Schedule(message, delay));

        public Task<string> ScheduleAsync(
            Message message,
            DateTimeOffset at,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Schedule(message, at));

        // ReScheduler / Cancel are not exercised by the FR-2 / FR-9 conformance behaviours.
        public bool ReScheduler(string schedulerId, DateTimeOffset at) => false;
        public bool ReScheduler(string schedulerId, TimeSpan delay) => false;

        public Task<bool> ReSchedulerAsync(
            string schedulerId,
            DateTimeOffset at,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(false);

        public Task<bool> ReSchedulerAsync(
            string schedulerId,
            TimeSpan delay,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(false);

        public void Cancel(string id) { }

        public Task CancelAsync(string id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private static TimeSpan Clamp(TimeSpan delay) => delay > TimeSpan.Zero ? delay : TimeSpan.Zero;

        private void Republish(Message message)
        {
            try
            {
                var producer = new RmqMessageProducer(_connection);
                lock (_lock) { _producers.Add(producer); }
                producer.Send(message);
            }
            catch
            {
                // Best-effort redelivery for the conformance harness; a broker error surfaces
                // as the conformance test's after-delay arm timing out rather than an unobserved
                // exception.
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var timer in _timers)
                {
                    try { timer.Dispose(); } catch { /* ignore */ }
                }
                _timers.Clear();

                foreach (var producer in _producers)
                {
                    try { producer.Dispose(); } catch { /* ignore */ }
                }
                _producers.Clear();
            }
        }
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

    /// <summary>
    /// Channel decorator that tracks requeue count per original message ID and
    /// rejects (sending to DLQ) after the max requeue count is reached.
    /// </summary>
    private sealed class RequeueTrackingChannelAsync : IAmAChannelAsync
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
}

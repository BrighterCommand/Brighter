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
using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.TestDoubles;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway;

public class MqttMessageGatewayProvider
    : Proactor.IAmAMessageGatewayProactorProvider,
        Reactor.IAmAMessageGatewayReactorProvider
{
    private const string HOSTNAME = "localhost";
    private const int PORT = 1883;

    private MqttMessageConsumer? _dlqConsumer;
    private MqttMessageConsumer? _invalidConsumer;

    // Shared harness scheduler for FR-2 (delayed requeue) and FR-9 (delayed send).
    // MQTT has no native delayed delivery; the gateway delegates to the scheduler seam.
    private MqttHarnessMessageScheduler? _scheduler;
    private MqttHarnessMessageScheduler Scheduler =>
        _scheduler ??= new MqttHarnessMessageScheduler(HOSTNAME, PORT);

    // MQTT uses the base Publication type — there is no transport-specific publication class.
    public Publication CreatePublication(
        RoutingKey routingKey,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new Publication
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
        };
    }

    // Each test gets a unique MQTT topic prefix so there is no cross-test message pollution.
    // The consumer subscribes to  "{prefix}/#"  and the producer publishes to  "{prefix}/{topic}".
    // When a DLQ or invalid-message key is provided, pre-subscribe dedicated consumers to those
    // topics so they are ready to receive before any message is rejected.
    public IAmAChannelSync CreateChannel(MqttSubscription subscription)
    {
        var consumerConfig = BuildConsumerConfig(subscription.RoutingKey.Value);
        var factory = new MqttMessageConsumerFactory(consumerConfig, Scheduler);
        var channel = new ChannelFactory(factory).CreateSyncChannel(subscription);

        if (subscription.DeadLetterRoutingKey != null)
        {
            var dlqConfig = BuildConsumerConfig(subscription.DeadLetterRoutingKey.Value);
            _dlqConsumer = new MqttMessageConsumer(dlqConfig);
        }

        if (subscription.InvalidMessageRoutingKey != null)
        {
            var invalidConfig = BuildConsumerConfig(subscription.InvalidMessageRoutingKey.Value);
            _invalidConsumer = new MqttMessageConsumer(invalidConfig);
        }

        // Wrap with requeue-count tracking so exhausted requeueing routes to the DLQ.
        var maxRequeue = subscription.DeadLetterRoutingKey != null && subscription.RequeueCount > 0
            ? subscription.RequeueCount
            : int.MaxValue;
        return new RequeueTrackingChannelSync(channel, maxRequeue);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        MqttSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var consumerConfig = BuildConsumerConfig(subscription.RoutingKey.Value);
        var factory = new MqttMessageConsumerFactory(consumerConfig, Scheduler);
        var channel = await new ChannelFactory(factory).CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.DeadLetterRoutingKey != null)
        {
            var dlqConfig = BuildConsumerConfig(subscription.DeadLetterRoutingKey.Value);
            _dlqConsumer = new MqttMessageConsumer(dlqConfig);
        }

        if (subscription.InvalidMessageRoutingKey != null)
        {
            var invalidConfig = BuildConsumerConfig(subscription.InvalidMessageRoutingKey.Value);
            _invalidConsumer = new MqttMessageConsumer(invalidConfig);
        }

        var maxRequeue = subscription.DeadLetterRoutingKey != null && subscription.RequeueCount > 0
            ? subscription.RequeueCount
            : int.MaxValue;
        return new RequeueTrackingChannelAsync(channel, maxRequeue);
    }

    public IAmAMessageProducerSync CreateProducer(Publication publication)
    {
        var topicPrefix = publication.Topic?.Value ?? string.Empty;
        var producerConfig = BuildProducerConfig(topicPrefix);
        var publisher = new MqttMessagePublisher(producerConfig);
        return new MqttMessageProducer(publisher, publication) { Scheduler = Scheduler };
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        Publication publication,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var topicPrefix = publication.Topic?.Value ?? string.Empty;
        var producerConfig = BuildProducerConfig(topicPrefix);
        var publisher = new MqttMessagePublisher(producerConfig);
        return new MqttMessageProducer(publisher, publication) { Scheduler = Scheduler };
    }

    public MqttSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)
    {
        // bufferSize = 5: the Channel wrapper drains all messages that have accumulated
        // between polls in one Receive call. The multi-message canonical test sends 4 messages;
        // a buffer of 5 prevents the Channel from throwing "too many items to enqueue".
        // Channel.maxQueueLength is capped at 10 by the framework, so 5 is a safe mid-range value.
        const int bufferSize = 5;

        if (deadLetterRoutingKey != null)
        {
            return new MqttSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(Guid.NewGuid().ToString()),
                channelName: channelName,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannel,
                deadLetterRoutingKey: deadLetterRoutingKey,
                invalidMessageRoutingKey: invalidMessageRoutingKey,
                requeueCount: 3,
                bufferSize: bufferSize
            );
        }

        return new MqttSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Guid.NewGuid().ToString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            invalidMessageRoutingKey: invalidMessageRoutingKey,
            bufferSize: bufferSize
        );
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages)
    {
        if (channel != null)
        {
            try { channel.Purge(); } catch { /* best effort */ }
            try { channel.Dispose(); } catch { /* best effort */ }
        }

        try { producer?.Dispose(); } catch { /* best effort */ }
        try { _dlqConsumer?.Dispose(); } catch { /* best effort */ }
        _dlqConsumer = null;
        try { _invalidConsumer?.Dispose(); } catch { /* best effort */ }
        _invalidConsumer = null;
        try { _scheduler?.Dispose(); } catch { /* best effort */ }
        _scheduler = null;
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages)
    {
        if (channel != null)
        {
            try { await channel.PurgeAsync(); } catch { /* best effort */ }
            try { channel.Dispose(); } catch { /* best effort */ }
        }

        if (producer != null)
        {
            try { await producer.DisposeAsync(); } catch { /* best effort */ }
        }

        if (_dlqConsumer != null)
        {
            try { await _dlqConsumer.DisposeAsync(); } catch { /* best effort */ }
            _dlqConsumer = null;
        }

        if (_invalidConsumer != null)
        {
            try { await _invalidConsumer.DisposeAsync(); } catch { /* best effort */ }
            _invalidConsumer = null;
        }

        try { _scheduler?.Dispose(); } catch { /* best effort */ }
        _scheduler = null;
    }

    // Polls the pre-subscribed DLQ consumer with a bounded retry ceiling (NFR-2, AC-20, AC-25).
    // Returns MT_NONE when nothing arrives within the bound or when no DLQ key was configured.
    public Message GetMessageFromDeadLetterQueue(MqttSubscription subscription)
    {
        if (_dlqConsumer == null)
            throw new InvalidOperationException(
                "DLQ consumer was not pre-created. Ensure CreateChannel was called with a DLQ-configured subscription.");

        for (var i = 0; i < 10; i++)
        {
            var messages = _dlqConsumer.Receive(TimeSpan.FromSeconds(5));
            var found = messages.FirstOrDefault(m => m.Header.MessageType != MessageType.MT_NONE);
            if (found != null)
            {
                _dlqConsumer.Acknowledge(found);
                RestoreOriginalTopic(found);
                return found;
            }
            Thread.Sleep(1000);
        }

        return new Message();
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        MqttSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        if (_dlqConsumer == null)
            throw new InvalidOperationException(
                "DLQ consumer was not pre-created. Ensure CreateChannelAsync was called with a DLQ-configured subscription.");

        await Task.CompletedTask;

        for (var i = 0; i < 10; i++)
        {
            var messages = _dlqConsumer.Receive(TimeSpan.FromSeconds(5));
            var found = messages.FirstOrDefault(m => m.Header.MessageType != MessageType.MT_NONE);
            if (found != null)
            {
                _dlqConsumer.Acknowledge(found);
                RestoreOriginalTopic(found);
                return found;
            }
            Thread.Sleep(1000);
        }

        return new Message();
    }

    public Message GetMessageFromInvalidChannel(MqttSubscription subscription)
    {
        if (_invalidConsumer == null)
            throw new InvalidOperationException(
                "Invalid-message consumer was not pre-created. Ensure CreateChannel was called with an invalid-message-configured subscription.");

        for (var i = 0; i < 10; i++)
        {
            var messages = _invalidConsumer.Receive(TimeSpan.FromSeconds(5));
            var found = messages.FirstOrDefault(m => m.Header.MessageType != MessageType.MT_NONE);
            if (found != null)
            {
                _invalidConsumer.Acknowledge(found);
                RestoreOriginalTopic(found);
                return found;
            }
            Thread.Sleep(1000);
        }

        return new Message();
    }

    public async Task<Message> GetMessageFromInvalidChannelAsync(
        MqttSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        if (_invalidConsumer == null)
            throw new InvalidOperationException(
                "Invalid-message consumer was not pre-created. Ensure CreateChannelAsync was called with an invalid-message-configured subscription.");

        await Task.CompletedTask;

        for (var i = 0; i < 10; i++)
        {
            var messages = _invalidConsumer.Receive(TimeSpan.FromSeconds(5));
            var found = messages.FirstOrDefault(m => m.Header.MessageType != MessageType.MT_NONE);
            if (found != null)
            {
                _invalidConsumer.Acknowledge(found);
                RestoreOriginalTopic(found);
                return found;
            }
            Thread.Sleep(1000);
        }

        return new Message();
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
        => new ChannelName($"Queue{Guid.NewGuid().ToString("N")[..8]}");

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
        => new RoutingKey($"gen-mqtt-{Guid.NewGuid().ToString("N")[..8]}");

    // MQTT stamps the same camelCase metadata keys as Redis.
    public RejectionMetadataKeys RejectionMetadataKeys =>
        new RejectionMetadataKeys(
            "originalTopic",
            "originalMessageType",
            "rejectionReason",
            "rejectionMessage",
            "rejectionTimestamp"
        );

    // ── private helpers ──────────────────────────────────────────────────────

    private static MqttMessagingGatewayConsumerConfiguration BuildConsumerConfig(string topicPrefix)
        => new()
        {
            Hostname = HOSTNAME,
            Port = PORT,
            TopicPrefix = topicPrefix,
            ClientID = $"brighter-{Guid.NewGuid().ToString("N")[..8]}",
        };

    private static MqttMessagingGatewayProducerConfiguration BuildProducerConfig(string topicPrefix)
        => new()
        {
            Hostname = HOSTNAME,
            Port = PORT,
            TopicPrefix = topicPrefix,
            ClientID = $"brighter-{Guid.NewGuid().ToString("N")[..8]}",
        };

    // Rejection rewrites Header.Topic to the DLQ/invalid routing key; restore the original.
    private static void RestoreOriginalTopic(Message message)
    {
        if (message.Header.Bag.TryGetValue("originalTopic", out var originalTopic))
            message.Header.Topic = new RoutingKey(originalTopic.ToString()!);
    }

    // ── inner channel decorators ─────────────────────────────────────────────

    /// <summary>
    /// Tracks per-message requeue counts and routes to the DLQ once the ceiling is reached.
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
        public bool Reject(Message message, MessageRejectionReason? reason = null) => _inner.Reject(message, reason);
        public void Nack(Message message) => _inner.Nack(message);

        public bool Requeue(Message message, TimeSpan? timeOut = null)
        {
            var originalId = GetOriginalId(message);
            if (!message.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName))
                message.Header.Bag[Message.OriginalMessageIdHeaderName] = message.Header.MessageId.ToString();

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

        private static string GetOriginalId(Message message) =>
            message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var id)
                ? id?.ToString() ?? message.Header.MessageId.ToString()
                : message.Header.MessageId.ToString();
    }

    /// <summary>
    /// Tracks per-message requeue counts and routes to the DLQ once the ceiling is reached.
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
            var originalId = GetOriginalId(message);
            if (!message.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName))
                message.Header.Bag[Message.OriginalMessageIdHeaderName] = message.Header.MessageId.ToString();

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

        private static string GetOriginalId(Message message) =>
            message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var id)
                ? id?.ToString() ?? message.Header.MessageId.ToString()
                : message.Header.MessageId.ToString();
    }
}

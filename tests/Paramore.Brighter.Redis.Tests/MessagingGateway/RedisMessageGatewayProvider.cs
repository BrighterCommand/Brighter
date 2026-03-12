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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

public class RedisMessageGatewayProvider
    : Proactor.IAmAMessageGatewayProactorProvider,
        Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly RedisMessagingGatewayConfiguration _configuration;
    private RedisMessageConsumer? _dlqConsumer;

    public RedisMessageGatewayProvider()
    {
        _configuration = RedisFixture.RedisMessagingGatewayConfiguration();
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages
    )
    {
        if (channel != null)
        {
            try { channel.Purge(); } catch { /* pool may already be disposed */ }
            try { channel.Dispose(); } catch { /* best effort */ }
        }

        try { producer?.Dispose(); } catch { /* best effort */ }
        try { _dlqConsumer?.Dispose(); } catch { /* best effort */ }
        _dlqConsumer = null;
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages
    )
    {
        if (channel != null)
        {
            try { await channel.PurgeAsync(); } catch { /* pool may already be disposed */ }
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
    }

    public IAmAChannelSync CreateChannel(RedisSubscription subscription)
    {
        var channel = new ChannelFactory(
            new RedisMessageConsumerFactory(_configuration)
        ).CreateSyncChannel(subscription);

        // Redis requires a receive before send to establish the subscription
        channel.Receive(TimeSpan.FromMilliseconds(100));

        // Pre-subscribe DLQ consumer so it receives notifications when messages
        // are rejected. Must be created before any requeue/reject operations.
        if (subscription.DeadLetterRoutingKey != null)
        {
            var dlqQueueName = new ChannelName($"dlq-{Guid.NewGuid().ToString("N")[..8]}");
            _dlqConsumer = new RedisMessageConsumer(
                _configuration,
                dlqQueueName,
                subscription.DeadLetterRoutingKey
            );
            _dlqConsumer.Receive(TimeSpan.FromMilliseconds(1000));
        }

        // Always wrap with requeue tracking to ensure x-original-message-id is set.
        // When DLQ is configured, uses the subscription's requeue count for DLQ routing.
        // Otherwise, uses int.MaxValue so DLQ routing never triggers.
        var maxRequeue = subscription.DeadLetterRoutingKey != null && subscription.RequeueCount > 0
            ? subscription.RequeueCount
            : int.MaxValue;

        return new RequeueTrackingChannelSync(channel, maxRequeue);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        RedisSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        var channel = await new ChannelFactory(
            new RedisMessageConsumerFactory(_configuration)
        ).CreateAsyncChannelAsync(subscription, cancellationToken);

        // Redis async ReceiveAsync does NOT enforce a 1s minimum timeout like
        // sync Receive does, so we use 1000ms explicitly to ensure the BLPOP
        // registration completes reliably.
        await channel.ReceiveAsync(TimeSpan.FromMilliseconds(1000), cancellationToken);

        if (subscription.DeadLetterRoutingKey != null)
        {
            var dlqQueueName = new ChannelName($"dlq-{Guid.NewGuid().ToString("N")[..8]}");
            _dlqConsumer = new RedisMessageConsumer(
                _configuration,
                dlqQueueName,
                subscription.DeadLetterRoutingKey
            );
            _dlqConsumer.Receive(TimeSpan.FromMilliseconds(1000));
        }

        var maxRequeue = subscription.DeadLetterRoutingKey != null && subscription.RequeueCount > 0
            ? subscription.RequeueCount
            : int.MaxValue;

        return new RequeueTrackingChannelAsync(channel, maxRequeue);
    }

    public IAmAMessageProducerSync CreateProducer(RedisMessagePublication publication)
    {
        return new RedisMessageProducer(_configuration, publication);
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        RedisMessagePublication publication,
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
        return new RedisMessageProducer(_configuration, publication);
    }

    public RedisMessagePublication CreatePublication(
        RoutingKey routingKey,
        OnMissingChannel makeChannels = OnMissingChannel.Create
    )
    {
        return new RedisMessagePublication
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
        };
    }

    public RedisSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        bool setupDeadLetterQueue = false
    )
    {
        if (setupDeadLetterQueue)
        {
            return new RedisSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(Guid.NewGuid().ToString()),
                channelName: channelName,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannel,
                deadLetterRoutingKey: new RoutingKey($"{routingKey}.DLQ"),
                requeueCount: 3
            );
        }

        return new RedisSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Guid.NewGuid().ToString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel
        );
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"Queue{Guid.NewGuid().ToString("N")[..8]}");
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"gen.redis.{Guid.NewGuid().ToString("N")[..8]}");
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        RedisSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        if (_dlqConsumer == null)
            throw new InvalidOperationException("DLQ consumer was not pre-created. Ensure CreateChannelAsync was called with a DLQ-configured subscription.");

        await Task.CompletedTask;

        for (var i = 0; i < 10; i++)
        {
            var messages = _dlqConsumer.Receive(TimeSpan.FromSeconds(5));
            if (!messages.Any())
            {
                Thread.Sleep(1000);
                continue;
            }

            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                _dlqConsumer.Acknowledge(message);

                // Restore original topic — Reject changes it to the DLQ routing key
                if (message.Header.Bag.TryGetValue("originalTopic", out var originalTopic))
                    message.Header.Topic = new RoutingKey(originalTopic.ToString()!);

                return message;
            }
            Thread.Sleep(1000);
        }

        return new Message();
    }

    public Message GetMessageFromDeadLetterQueue(RedisSubscription subscription)
    {
        if (_dlqConsumer == null)
            throw new InvalidOperationException("DLQ consumer was not pre-created. Ensure CreateChannel was called with a DLQ-configured subscription.");

        for (var i = 0; i < 10; i++)
        {
            var messages = _dlqConsumer.Receive(TimeSpan.FromSeconds(5));
            if (!messages.Any())
            {
                Thread.Sleep(1000);
                continue;
            }

            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                _dlqConsumer.Acknowledge(message);

                // Restore original topic — Reject changes it to the DLQ routing key
                if (message.Header.Bag.TryGetValue("originalTopic", out var originalTopic))
                    message.Header.Topic = new RoutingKey(originalTopic.ToString()!);

                return message;
            }
            Thread.Sleep(1000);
        }

        return new Message();
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

        public async Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
        {
            // Redis BLPOP needs at least 1s to work reliably in async mode,
            // matching the sync consumer's enforcement in RedisMessageConsumer.Receive
            if (timeout != null && timeout.Value.TotalSeconds < 1)
                timeout = TimeSpan.FromSeconds(1);
            return await _inner.ReceiveAsync(timeout, cancellationToken);
        }

        public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
            => _inner.RejectAsync(message, reason, cancellationToken);

        public Task NackAsync(Message message, CancellationToken cancellationToken = default)
            => _inner.NackAsync(message, cancellationToken);

        public async Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        {
            var originalId = GetOriginalMessageId(message);

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

        private static string GetOriginalMessageId(Message message)
        {
            return message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var id)
                ? id?.ToString() ?? message.Header.MessageId.ToString()
                : message.Header.MessageId.ToString();
        }
    }
}

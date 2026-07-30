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

using System.Runtime.CompilerServices;
using Org.Apache.Rocketmq;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;
using Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway;

public class RocketMqMessageGatewayProvider
    : IAmAMessageGatewayProactorProvider,
        IAmAMessageGatewayReactorProvider
{
    private readonly RocketMessagingGatewayConnection _connection;

    // Every generated test resolves its topic through this map. RocketMQ topics must be pre-created
    // (the C# client resolves a topic route before publishing, so auto-create never triggers) and
    // topic names may not contain '.', so each canonical/legacy behaviour maps to a dedicated
    // pre-created topic. Topics whose names contain "delayed_msg"/"requeue_delay" are created as
    // DELAY types and "partition_key" as FIFO (see GetTopicType). The infrastructure-missing tests
    // are deliberately NOT mapped so they fall through to a non-existent topic.
    private static readonly Dictionary<string, string> s_topicMap = new()
    {
        // Reactor topics — basic
        ["When_posting_a_message_via_the_messaging_gateway_should_be_received"] = "gen_r_post_msg",
        ["When_a_message_consumer_reads_multiple_messages_should_receive_all_messages"] = "gen_r_multi_msg",
        ["When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception"] = "gen_r_multi_thread",
        ["When_sending_a_message_should_propagate_activity_context"] = "gen_r_activity",
        ["When_posting_a_message_with_partition_key_via_the_messaging_gateway_should_be_received"] = "gen_r_partition_key",
        // Reactor topics — delay
        ["When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery"] = "gen_r_delayed_msg",
        ["When_sending_a_delayed_message_should_deliver_after_delay"] = "gen_r_send_delayed_msg",
        // Reactor topics — requeue / redeliver / nack
        ["When_requeuing_a_failed_message_should_be_redelivered"] = "gen_r_rq_redeliver",
        ["When_requeuing_a_failed_message_should_receive_message_again"] = "gen_r_rq_again",
        ["When_requeuing_a_failed_message_with_delay_should_receive_message_again"] = "gen_r_rq_delay_again",
        ["When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately"] = "gen_r_rq_zero",
        ["When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue"] = "gen_r_exhaust",
        ["When_nacking_a_message_it_should_be_redelivered"] = "gen_r_nack",
        ["When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second"] = "gen_r_nack2",
        // Reactor topics — reject
        ["When_rejecting_message_with_delivery_error_should_send_to_dlq"] = "gen_r_rej_de",
        ["When_rejecting_message_should_include_metadata"] = "gen_r_rej_meta",
        ["When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log"] = "gen_r_rej_noch",
        ["When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq"] = "gen_r_rej_fb",
        ["When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel"] = "gen_r_rej_inv",
        ["When_rejecting_message_with_unknown_reason_should_send_to_dlq"] = "gen_r_rej_unk",

        // Proactor topics — basic
        ["When_posting_a_message_via_the_messaging_gateway_should_be_received_async"] = "gen_p_post_msg",
        ["When_a_message_consumer_reads_multiple_messages_should_receive_all_messages_async"] = "gen_p_multi_msg",
        ["When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception_async"] = "gen_p_multi_thread",
        ["When_sending_a_message_should_propagate_activity_context_async"] = "gen_p_activity",
        ["When_posting_a_message_with_partition_key_via_the_messaging_gateway_should_be_received_async"] = "gen_p_partition_key",
        // Proactor topics — delay
        ["When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery_async"] = "gen_p_delayed_msg",
        ["When_sending_a_delayed_message_should_deliver_after_delay_async"] = "gen_p_send_delayed_msg",
        // Proactor topics — requeue / redeliver / nack
        ["When_requeuing_a_failed_message_should_be_redelivered_async"] = "gen_p_rq_redeliver",
        ["When_requeuing_a_failed_message_should_receive_message_again_async"] = "gen_p_rq_again",
        ["When_requeuing_a_failed_message_with_delay_should_receive_message_again_async"] = "gen_p_rq_delay_again",
        ["When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately_async"] = "gen_p_rq_zero",
        ["When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue_async"] = "gen_p_exhaust",
        ["When_nacking_a_message_it_should_be_redelivered_async"] = "gen_p_nack",
        ["When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second_async"] = "gen_p_nack2",
        // Proactor topics — reject
        ["When_rejecting_message_with_delivery_error_should_send_to_dlq_async"] = "gen_p_rej_de",
        ["When_rejecting_message_should_include_metadata_async"] = "gen_p_rej_meta",
        ["When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log_async"] = "gen_p_rej_noch",
        ["When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq_async"] = "gen_p_rej_fb",
        ["When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel_async"] = "gen_p_rej_inv",
        ["When_rejecting_message_with_unknown_reason_should_send_to_dlq_async"] = "gen_p_rej_unk",
    };

    public RocketMqMessageGatewayProvider()
    {
        _connection = GatewayFactory.CreateConnection();
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        if (testName != null && s_topicMap.TryGetValue(testName, out var topic))
        {
            return new RoutingKey(topic);
        }

        // Infrastructure-missing tests get a non-existent topic
        return new RoutingKey($"gen_nonexistent_{Guid.NewGuid():N}");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"Channel{Uuid.New():N}");
    }

    public RocketMqPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new RocketMqPublication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
            TopicType = GetTopicType(routingKey),
        };
    }

    // Requeue/Nack are no-ops on RocketMQ; a message redelivers when its invisibility lease expires
    // (like a Postgres/SQS visibility lease). RocketMQ enforces a 10 s minimum invisibility, so plain
    // requeue / nack redelivery (FR-22/FR-16, 30 s ceiling) is observable but a zero-delay "redeliver
    // within 5 s" (FR-15) is not — that needs the commented-out ChangeInvisibleDuration (the FR-2 gap).
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);

    // Bound the consumer poll below the canonical delay (5 s) so the delayed-send before-D arm
    // (a single receive shorter than the delay must yield MT_NONE) is honoured; the RocketMQ consumer
    // long-polls for ReceiveMessageTimeout and ignores the per-call timeout, so this is the real bound.
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);

    // The canonical rejection templates derive dead-letter/invalid routing keys as "{topic}.DLQ" /
    // "{topic}.Invalid", but RocketMQ topic names may not contain '.'. Flatten the dot so the routing
    // key names a valid pre-created topic; the reject producer and the read hooks both resolve the
    // dead-letter/invalid destination from the subscription, so they stay in sync.
    private static RoutingKey? ToValidTopicName(RoutingKey? routingKey)
        => routingKey is null ? null : new RoutingKey(routingKey.Value.Replace(".", "_"));

    public RocketSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)
    {
        if (deadLetterRoutingKey != null || invalidMessageRoutingKey != null)
        {
            return new RocketMqSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(Uuid.NewAsString()),
                channelName: channelName,
                routingKey: routingKey,
                consumerGroup: Guid.NewGuid().ToString(),
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannel,
                invisibilityTimeout: s_invisibilityTimeout,
                receiveMessageTimeout: s_receiveMessageTimeout,
                deadLetterRoutingKey: ToValidTopicName(deadLetterRoutingKey),
                invalidMessageRoutingKey: ToValidTopicName(invalidMessageRoutingKey),
                requeueCount: 3
            );
        }

        return new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            consumerGroup: Guid.NewGuid().ToString(),
            messagePumpType: MessagePumpType.Proactor,
            invisibilityTimeout: s_invisibilityTimeout,
            receiveMessageTimeout: s_receiveMessageTimeout,
            makeChannels: makeChannel
        );
    }

    public IAmAMessageProducerSync CreateProducer(RocketMqPublication publication)
    {
        var producer = BrighterAsyncContext.Run(() => GatewayFactory.CreateProducer(_connection, publication));
        return new RocketMqMessageProducer(_connection, producer, publication);
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        RocketMqPublication publication,
        CancellationToken cancellationToken = default)
    {
        var producer = await GatewayFactory.CreateProducer(_connection, publication);
        return new RocketMqMessageProducer(_connection, producer, publication);
    }

    public IAmAChannelSync CreateChannel(RocketSubscription subscription)
    {
        var channelFactory = new RocketMqChannelFactory(new RocketMessageConsumerFactory(_connection));
        var channel = channelFactory.CreateSyncChannel(subscription);

        if (subscription.DeadLetterRoutingKey != null && subscription.RequeueCount > 0)
        {
            return new RequeueTrackingChannelSync(channel, subscription.RequeueCount);
        }

        return channel;
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        RocketSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var channelFactory = new RocketMqChannelFactory(new RocketMessageConsumerFactory(_connection));
        var channel = await channelFactory.CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.DeadLetterRoutingKey != null && subscription.RequeueCount > 0)
        {
            return new RequeueTrackingChannelAsync(channel, subscription.RequeueCount);
        }

        return channel;
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages)
    {
        channel?.Dispose();
        producer?.Dispose();
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages)
    {
        channel?.Dispose();

        if (producer != null)
        {
            await producer.DisposeAsync();
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

    public Message GetMessageFromDeadLetterQueue(RocketSubscription subscription)
    {
        return BrighterAsyncContext.Run(() => GetMessageFromDeadLetterQueueAsync(subscription));
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        RocketSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        if (subscription.DeadLetterRoutingKey == null)
            return Message.Empty;

        var dlqConsumer = await new SimpleConsumer.Builder()
            .SetClientConfig(_connection.ClientConfig)
            .SetConsumerGroup(Guid.NewGuid().ToString())
            .SetAwaitDuration(TimeSpan.FromSeconds(5))
            .SetSubscriptionExpression(new Dictionary<string, FilterExpression>
            {
                [subscription.DeadLetterRoutingKey!] = new("*")
            })
            .Build();

        var consumer = new RocketMessageConsumer(dlqConsumer, 1, TimeSpan.FromSeconds(30));

        try
        {
            for (var i = 0; i < 10; i++)
            {
                var messages = await consumer.ReceiveAsync(TimeSpan.FromSeconds(5), cancellationToken);
                var message = messages.First();
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    await consumer.AcknowledgeAsync(message, cancellationToken);
                    return message;
                }

                await Task.Delay(1000, cancellationToken);
            }

            return new Message();
        }
        finally
        {
            await consumer.DisposeAsync();
        }
    }

    public Message GetMessageFromInvalidChannel(RocketSubscription subscription)
    {
        return BrighterAsyncContext.Run(() => GetMessageFromInvalidChannelAsync(subscription));
    }

    public async Task<Message> GetMessageFromInvalidChannelAsync(
        RocketSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        if (subscription.InvalidMessageRoutingKey == null)
            return Message.Empty;

        var invalidConsumer = await new SimpleConsumer.Builder()
            .SetClientConfig(_connection.ClientConfig)
            .SetConsumerGroup(Guid.NewGuid().ToString())
            .SetAwaitDuration(TimeSpan.FromSeconds(5))
            .SetSubscriptionExpression(new Dictionary<string, FilterExpression>
            {
                [subscription.InvalidMessageRoutingKey!] = new("*")
            })
            .Build();

        var consumer = new RocketMessageConsumer(invalidConsumer, 1, TimeSpan.FromSeconds(30));

        try
        {
            for (var i = 0; i < 10; i++)
            {
                var messages = await consumer.ReceiveAsync(TimeSpan.FromSeconds(5), cancellationToken);
                var message = messages.First();
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    await consumer.AcknowledgeAsync(message, cancellationToken);
                    return message;
                }

                await Task.Delay(1000, cancellationToken);
            }

            return new Message();
        }
        finally
        {
            await consumer.DisposeAsync();
        }
    }

    private static TopicType GetTopicType(RoutingKey routingKey)
    {
        var topic = routingKey.Value;
        if (topic.Contains("partition_key") || topic.EndsWith("partition_key"))
        {
            return TopicType.Fifo;
        }

        if (topic.Contains("delayed_msg") || topic.Contains("requeue_delay"))
        {
            return TopicType.Delay;
        }

        return TopicType.Normal;
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

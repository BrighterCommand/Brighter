using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway;

/// <summary>
/// Enforces a minimum receive timeout for SQS channels and tracks original message ID on requeue.
/// Generated tests use 300ms which maps to WaitTimeSeconds=0 (short polling).
/// AWS SNS→SQS needs long-polling (≥5s) for reliable message delivery.
/// Also sets x-original-message-id on requeue since SQS ChangeMessageVisibility doesn't propagate it.
/// </summary>
internal class MinimumTimeoutChannelAsync(IAmAChannelAsync inner, TimeSpan minimumTimeout) : IAmAChannelAsync
{
    private readonly Dictionary<string, string> _requeuedMessageIds = new();

    public ChannelName Name => inner.Name;
    public RoutingKey RoutingKey => inner.RoutingKey;
    public void Enqueue(params Message[] messages) => inner.Enqueue(messages);
    public void Stop(RoutingKey topic) => inner.Stop(topic);
    public void Dispose() => inner.Dispose();

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        => inner.AcknowledgeAsync(message, cancellationToken);

    public Task PurgeAsync(CancellationToken cancellationToken = default)
        => inner.PurgeAsync(cancellationToken);

    public async Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout.HasValue && timeout.Value < minimumTimeout ? minimumTimeout : timeout;
        var message = await inner.ReceiveAsync(effectiveTimeout, cancellationToken);

        if (message.Header.MessageType != MessageType.MT_NONE
            && _requeuedMessageIds.TryGetValue(message.Header.MessageId, out var originalId))
        {
            message.Header.Bag[Message.OriginalMessageIdHeaderName] = originalId;
        }

        return message;
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => inner.RejectAsync(message, reason, cancellationToken);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        => inner.NackAsync(message, cancellationToken);

    public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        var originalId = message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var existing)
            ? existing.ToString()!
            : message.Header.MessageId.ToString();

        _requeuedMessageIds[message.Header.MessageId] = originalId;
        message.Header.Bag[Message.OriginalMessageIdHeaderName] = originalId;

        return inner.RequeueAsync(message, timeOut, cancellationToken);
    }
}

/// <summary>
/// Enforces a minimum receive timeout for SQS channels (sync version).
/// </summary>
internal class MinimumTimeoutChannelSync(IAmAChannelSync inner, TimeSpan minimumTimeout) : IAmAChannelSync
{
    private readonly Dictionary<string, string> _requeuedMessageIds = new();

    public ChannelName Name => inner.Name;
    public RoutingKey RoutingKey => inner.RoutingKey;
    public void Enqueue(params Message[] messages) => inner.Enqueue(messages);
    public void Stop(RoutingKey topic) => inner.Stop(topic);
    public void Dispose() => inner.Dispose();

    public void Acknowledge(Message message) => inner.Acknowledge(message);
    public void Purge() => inner.Purge();

    public Message Receive(TimeSpan? timeout)
    {
        var effectiveTimeout = timeout.HasValue && timeout.Value < minimumTimeout ? minimumTimeout : timeout;
        var message = inner.Receive(effectiveTimeout);

        if (message.Header.MessageType != MessageType.MT_NONE
            && _requeuedMessageIds.TryGetValue(message.Header.MessageId, out var originalId))
        {
            message.Header.Bag[Message.OriginalMessageIdHeaderName] = originalId;
        }

        return message;
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null)
        => inner.Reject(message, reason);

    public void Nack(Message message) => inner.Nack(message);

    public bool Requeue(Message message, TimeSpan? timeOut = null)
    {
        var originalId = message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var existing)
            ? existing.ToString()!
            : message.Header.MessageId.ToString();

        _requeuedMessageIds[message.Header.MessageId] = originalId;
        message.Header.Bag[Message.OriginalMessageIdHeaderName] = originalId;

        return inner.Requeue(message, timeOut);
    }
}

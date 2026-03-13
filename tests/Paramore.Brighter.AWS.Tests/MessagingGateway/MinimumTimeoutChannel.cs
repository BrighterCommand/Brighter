using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

/// <summary>
/// Enforces a minimum receive timeout for SQS channels and tracks original message ID on requeue.
/// Generated tests use 300ms which maps to WaitTimeSeconds=0 (short polling).
/// AWS SNS→SQS needs long-polling (≥5s) for reliable message delivery.
/// Also sets x-original-message-id on requeue since SQS ChangeMessageVisibility doesn't propagate it.
/// </summary>
internal class MinimumTimeoutChannelAsync(IAmAChannelAsync inner, TimeSpan minimumTimeout) : IAmAChannelAsync
{
    public ChannelName Name => inner.Name;
    public RoutingKey RoutingKey => inner.RoutingKey;
    public void Enqueue(params Message[] messages) => inner.Enqueue(messages);
    public void Stop(RoutingKey topic) => inner.Stop(topic);
    public void Dispose() => inner.Dispose();

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        => inner.AcknowledgeAsync(message, cancellationToken);

    public Task PurgeAsync(CancellationToken cancellationToken = default)
        => inner.PurgeAsync(cancellationToken);

    public Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout.HasValue && timeout.Value < minimumTimeout ? minimumTimeout : timeout;
        return inner.ReceiveAsync(effectiveTimeout, cancellationToken);
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => inner.RejectAsync(message, reason, cancellationToken);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        => inner.NackAsync(message, cancellationToken);

    public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName))
        {
            message.Header.Bag[Message.OriginalMessageIdHeaderName] = message.Header.MessageId.ToString();
        }

        return inner.RequeueAsync(message, timeOut, cancellationToken);
    }
}

/// <summary>
/// Enforces a minimum receive timeout for SQS channels (sync version).
/// </summary>
internal class MinimumTimeoutChannelSync(IAmAChannelSync inner, TimeSpan minimumTimeout) : IAmAChannelSync
{
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
        return inner.Receive(effectiveTimeout);
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null)
        => inner.Reject(message, reason);

    public void Nack(Message message) => inner.Nack(message);

    public bool Requeue(Message message, TimeSpan? timeOut = null)
    {
        if (!message.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName))
        {
            message.Header.Bag[Message.OriginalMessageIdHeaderName] = message.Header.MessageId.ToString();
        }

        return inner.Requeue(message, timeOut);
    }
}

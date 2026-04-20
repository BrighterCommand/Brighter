using System;
using System.Collections.Generic;
using System.Transactions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Archiving.TestDoubles;

/// <summary>
/// Wraps an InMemoryOutbox exposing only the sync interface.
/// This makes OutboxArchiver detect it as sync-only (HasAsyncOutbox() == false).
/// </summary>
public class SyncOnlyOutboxWrapper : IAmAnOutboxSync<Message, CommittableTransaction>
{
    private readonly InMemoryOutbox _inner;

    public SyncOnlyOutboxWrapper(InMemoryOutbox inner) => _inner = inner;

    public IAmABrighterTracer? Tracer
    {
        set => _inner.Tracer = value;
    }

    public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
        => _inner.Add(message, requestContext, outBoxTimeout, transactionProvider);

    public void Add(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
        => _inner.Add(messages, requestContext, outBoxTimeout, transactionProvider);

    public void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
        => _inner.Delete(messageIds, requestContext, args);

    public IEnumerable<Message> DispatchedMessages(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100, int pageNumber = 1, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
        => _inner.DispatchedMessages(dispatchedSince, requestContext, pageSize, pageNumber, outBoxTimeout, args);

    public Message Get(Id messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null)
        => _inner.Get(messageId, requestContext, outBoxTimeout, args);

    public IEnumerable<Message> Get(IEnumerable<Id> messageIds, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null)
        => _inner.Get(messageIds, requestContext, outBoxTimeout, args);

    public void MarkDispatched(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null)
        => _inner.MarkDispatched(id, requestContext, dispatchedAt, args);

    public IEnumerable<Message> OutstandingMessages(TimeSpan dispatchedSince, RequestContext? requestContext,
        int pageSize = 100, int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null,
        Dictionary<string, object>? args = null)
        => _inner.OutstandingMessages(dispatchedSince, requestContext, pageSize, pageNumber, trippedTopics, args);

    public int GetOutstandingMessageCount(TimeSpan dispatchedSince, RequestContext? requestContext,
        int maxCount = 100, Dictionary<string, object>? args = null)
        => _inner.GetOutstandingMessageCount(dispatchedSince, requestContext, maxCount, args);
}

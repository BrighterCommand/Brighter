using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Archiving.TestDoubles;

/// <summary>
/// Wraps an InMemoryOutbox exposing only the async interface.
/// This makes OutboxArchiver detect it as async-only (HasOutbox() == false).
/// </summary>
public class AsyncOnlyOutboxWrapper : IAmAnOutboxAsync<Message, CommittableTransaction>
{
    private readonly InMemoryOutbox _inner;

    public AsyncOnlyOutboxWrapper(InMemoryOutbox inner) => _inner = inner;

    public IAmABrighterTracer? Tracer
    {
        set => _inner.Tracer = value;
    }

    public bool ContinueOnCapturedContext
    {
        get => _inner.ContinueOnCapturedContext;
        set => _inner.ContinueOnCapturedContext = value;
    }

    public Task AddAsync(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
        => _inner.AddAsync(message, requestContext, outBoxTimeout, transactionProvider, cancellationToken);

    public Task AddAsync(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
        => _inner.AddAsync(messages, requestContext, outBoxTimeout, transactionProvider, cancellationToken);

    public Task DeleteAsync(Id[] messageIds, RequestContext requestContext, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => _inner.DeleteAsync(messageIds, requestContext, args, cancellationToken);

    public Task<IEnumerable<Message>> DispatchedMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100, int pageNumber = 1, int outboxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => _inner.DispatchedMessagesAsync(dispatchedSince, requestContext, pageSize, pageNumber, outboxTimeout, args, cancellationToken);

    public Task<Message> GetAsync(Id messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => _inner.GetAsync(messageId, requestContext, outBoxTimeout, args, cancellationToken);

    public Task<IEnumerable<Message>> GetAsync(IEnumerable<Id> messageId, RequestContext requestContext,
        int outBoxTimeout = -1, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => _inner.GetAsync(messageId, requestContext, outBoxTimeout, args, cancellationToken);

    public Task MarkDispatchedAsync(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => _inner.MarkDispatchedAsync(id, requestContext, dispatchedAt, args, cancellationToken);

    public Task MarkDispatchedAsync(IEnumerable<Id> ids, RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => _inner.MarkDispatchedAsync(ids, requestContext, dispatchedAt, args, cancellationToken);

    public Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100, int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => _inner.OutstandingMessagesAsync(dispatchedSince, requestContext, pageSize, pageNumber, trippedTopics, args, cancellationToken);

    public Task<int> GetOutstandingMessageCountAsync(TimeSpan dispatchedSince, RequestContext? requestContext,
        int maxCount = 100, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => _inner.GetOutstandingMessageCountAsync(dispatchedSince, requestContext, maxCount, args, cancellationToken);
}

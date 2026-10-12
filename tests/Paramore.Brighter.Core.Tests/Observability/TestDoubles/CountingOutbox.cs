#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

public class CountingOutbox : IAmAnOutboxSync<Message, CommittableTransaction>, IAmAnOutboxAsync<Message, CommittableTransaction>
{
    private readonly InMemoryOutbox _inner;
    private readonly List<Id> _allAddedIds = new();
    private readonly object _lock = new();

    public int AddCount { get; private set; }
    public int AddAsyncCount { get; private set; }
    public int GetCount { get; private set; }
    public int GetAsyncCount { get; private set; }
    public int OutstandingMessagesCount { get; private set; }
    public int OutstandingMessagesAsyncCount { get; private set; }
    public int MarkDispatchedCount { get; private set; }
    public int MarkDispatchedAsyncCount { get; private set; }

    public bool FailAdd { get; set; }
    public bool FailMarkDispatched { get; set; }
    public bool ForceOutstanding { get; set; }

    public Dictionary<string, DateTimeOffset> MarkDispatchedTimestamps { get; } = new();

    public bool ContinueOnCapturedContext
    {
        get => _inner.ContinueOnCapturedContext;
        set => _inner.ContinueOnCapturedContext = value;
    }

    public IAmABrighterTracer? Tracer
    {
        set => _inner.Tracer = value;
    }

    public CountingOutbox(TimeProvider? timeProvider = null)
    {
        _inner = new InMemoryOutbox(timeProvider ?? TimeProvider.System);
    }

    public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
    {
        if (FailAdd) throw new ChannelFailureException("forced add failure");
        AddCount++;
        lock (_lock) _allAddedIds.Add(message.Id);
        _inner.Add(message, requestContext, outBoxTimeout, transactionProvider);
    }

    public void Add(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
    {
        if (FailAdd) throw new ChannelFailureException("forced add failure");
        var list = messages.ToList();
        AddCount += list.Count;
        lock (_lock) _allAddedIds.AddRange(list.Select(m => m.Id));
        _inner.Add(list, requestContext, outBoxTimeout, transactionProvider);
    }

    public Task AddAsync(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
    {
        if (FailAdd) throw new ChannelFailureException("forced add failure");
        AddAsyncCount++;
        lock (_lock) _allAddedIds.Add(message.Id);
        return _inner.AddAsync(message, requestContext, outBoxTimeout, transactionProvider, cancellationToken);
    }

    public Task AddAsync(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
    {
        if (FailAdd) throw new ChannelFailureException("forced add failure");
        var list = messages.ToList();
        AddAsyncCount += list.Count;
        lock (_lock) _allAddedIds.AddRange(list.Select(m => m.Id));
        return _inner.AddAsync(list, requestContext, outBoxTimeout, transactionProvider, cancellationToken);
    }

    public void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
        => _inner.Delete(messageIds, requestContext, args);

    public Task DeleteAsync(Id[] messageIds, RequestContext requestContext,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => _inner.DeleteAsync(messageIds, requestContext, args, cancellationToken);

    public IEnumerable<Message> DispatchedMessages(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100, int pageNumber = 1, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
        => _inner.DispatchedMessages(dispatchedSince, requestContext, pageSize, pageNumber, outBoxTimeout, args);

    public Task<IEnumerable<Message>> DispatchedMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100, int pageNumber = 1, int outboxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => _inner.DispatchedMessagesAsync(dispatchedSince, requestContext, pageSize, pageNumber, outboxTimeout, args, cancellationToken);

    public Message Get(Id messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null)
    {
        GetCount++;
        return _inner.Get(messageId, requestContext, outBoxTimeout, args);
    }

    public IEnumerable<Message> Get(IEnumerable<Id> messageIds, RequestContext requestContext,
        int outBoxTimeout = -1, Dictionary<string, object>? args = null)
    {
        GetCount++;
        return _inner.Get(messageIds, requestContext, outBoxTimeout, args);
    }

    public Task<Message> GetAsync(Id messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        GetAsyncCount++;
        return _inner.GetAsync(messageId, requestContext, outBoxTimeout, args, cancellationToken);
    }

    public Task<IEnumerable<Message>> GetAsync(IEnumerable<Id> messageId, RequestContext requestContext,
        int outBoxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        GetAsyncCount++;
        return _inner.GetAsync(messageId, requestContext, outBoxTimeout, args, cancellationToken);
    }

    public void MarkDispatched(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null)
    {
        if (FailMarkDispatched) throw new InvalidOperationException("forced mark failure");
        MarkDispatchedCount++;
        MarkDispatchedTimestamps.TryAdd(id.Value, dispatchedAt ?? DateTimeOffset.UtcNow);
        _inner.MarkDispatched(id, requestContext, dispatchedAt, args);
    }

    public Task MarkDispatchedAsync(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        if (FailMarkDispatched) throw new InvalidOperationException("forced mark failure");
        MarkDispatchedAsyncCount++;
        MarkDispatchedTimestamps.TryAdd(id.Value, dispatchedAt ?? DateTimeOffset.UtcNow);
        return _inner.MarkDispatchedAsync(id, requestContext, dispatchedAt, args, cancellationToken);
    }

    public Task MarkDispatchedAsync(IEnumerable<Id> ids, RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        if (FailMarkDispatched) throw new InvalidOperationException("forced mark failure");
        var list = ids.ToList();
        MarkDispatchedAsyncCount += list.Count;
        var ts = dispatchedAt ?? DateTimeOffset.UtcNow;
        foreach (var id in list) MarkDispatchedTimestamps.TryAdd(id.Value, ts);
        return _inner.MarkDispatchedAsync(list, requestContext, dispatchedAt, args, cancellationToken);
    }

    public IEnumerable<Message> OutstandingMessages(TimeSpan dispatchedSince, RequestContext? requestContext,
        int pageSize = 100, int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null,
        Dictionary<string, object>? args = null)
    {
        OutstandingMessagesCount++;
        if (ForceOutstanding)
        {
            List<Id> ids;
            lock (_lock) ids = _allAddedIds.ToList();
            return _inner.Get(ids, requestContext ?? new RequestContext());
        }
        return _inner.OutstandingMessages(dispatchedSince, requestContext, pageSize, pageNumber, trippedTopics, args);
    }

    public Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince,
        RequestContext requestContext, int pageSize = 100, int pageNumber = 1,
        IEnumerable<RoutingKey>? trippedTopics = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        OutstandingMessagesAsyncCount++;
        if (ForceOutstanding)
        {
            List<Id> ids;
            lock (_lock) ids = _allAddedIds.ToList();
            return _inner.GetAsync(ids, requestContext, cancellationToken: cancellationToken);
        }
        return _inner.OutstandingMessagesAsync(dispatchedSince, requestContext, pageSize, pageNumber, trippedTopics, args, cancellationToken);
    }

    public int GetOutstandingMessageCount(TimeSpan dispatchedSince, RequestContext? requestContext,
        int maxCount = 100, Dictionary<string, object>? args = null)
        => _inner.GetOutstandingMessageCount(dispatchedSince, requestContext, maxCount, args);

    public Task<int> GetOutstandingMessageCountAsync(TimeSpan dispatchedSince, RequestContext? requestContext,
        int maxCount = 100, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => _inner.GetOutstandingMessageCountAsync(dispatchedSince, requestContext, maxCount, args, cancellationToken);
}

#region Licence
/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

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

namespace Paramore.Brighter.Core.Tests.Confirmation.TestDoubles;

internal sealed class GatedAsyncOutbox : IAmAnOutboxAsync<Message, CommittableTransaction>
{
    private readonly TaskCompletionSource _allowDispatch = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _dispatchCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _dispatchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly InMemoryOutbox _inner = new(TimeProvider.System);

    public Task DispatchCompleted => _dispatchCompleted.Task;
    public Task DispatchStarted => _dispatchStarted.Task;

    public IAmABrighterTracer? Tracer
    {
        set => _inner.Tracer = value;
    }

    public bool ContinueOnCapturedContext
    {
        get => _inner.ContinueOnCapturedContext;
        set => _inner.ContinueOnCapturedContext = value;
    }

    public void AllowDispatch() => _allowDispatch.TrySetResult();

    public bool WasDispatched(Id id, RequestContext requestContext) =>
        _inner.DispatchedMessages(TimeSpan.Zero, requestContext).Any(message => message.Id == id);

    public Task AddAsync(
        Message message,
        RequestContext requestContext,
        int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default) =>
        _inner.AddAsync(message, requestContext, outBoxTimeout, transactionProvider, cancellationToken);

    public Task AddAsync(
        IEnumerable<Message> messages,
        RequestContext? requestContext,
        int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default) =>
        _inner.AddAsync(messages, requestContext, outBoxTimeout, transactionProvider, cancellationToken);

    public Task DeleteAsync(
        Id[] messageIds,
        RequestContext requestContext,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(messageIds, requestContext, args, cancellationToken);

    public Task<IEnumerable<Message>> DispatchedMessagesAsync(
        TimeSpan dispatchedSince,
        RequestContext requestContext,
        int pageSize = 100,
        int pageNumber = 1,
        int outboxTimeout = -1,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default) =>
        _inner.DispatchedMessagesAsync(
            dispatchedSince, requestContext, pageSize, pageNumber, outboxTimeout, args, cancellationToken);

    public Task<Message> GetAsync(
        Id messageId,
        RequestContext requestContext,
        int outBoxTimeout = -1,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default) =>
        _inner.GetAsync(messageId, requestContext, outBoxTimeout, args, cancellationToken);

    public Task<IEnumerable<Message>> GetAsync(
        IEnumerable<Id> messageIds,
        RequestContext requestContext,
        int outBoxTimeout = -1,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default) =>
        _inner.GetAsync(messageIds, requestContext, outBoxTimeout, args, cancellationToken);

    public async Task MarkDispatchedAsync(
        Id id,
        RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        _dispatchStarted.TrySetResult();
        await _allowDispatch.Task.WaitAsync(cancellationToken);
        await _inner.MarkDispatchedAsync(id, requestContext, dispatchedAt, args, cancellationToken);
        _dispatchCompleted.TrySetResult();
    }

    public Task MarkDispatchedAsync(
        IEnumerable<Id> ids,
        RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default) =>
        _inner.MarkDispatchedAsync(ids, requestContext, dispatchedAt, args, cancellationToken);

    public Task<IEnumerable<Message>> OutstandingMessagesAsync(
        TimeSpan dispatchedSince,
        RequestContext requestContext,
        int pageSize = 100,
        int pageNumber = 1,
        IEnumerable<RoutingKey>? trippedTopics = null,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default) =>
        _inner.OutstandingMessagesAsync(
            dispatchedSince, requestContext, pageSize, pageNumber, trippedTopics, args, cancellationToken);

    public Task<int> GetOutstandingMessageCountAsync(
        TimeSpan dispatchedSince,
        RequestContext? requestContext,
        int maxCount = 100,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default) =>
        _inner.GetOutstandingMessageCountAsync(
            dispatchedSince, requestContext, maxCount, args, cancellationToken);
}

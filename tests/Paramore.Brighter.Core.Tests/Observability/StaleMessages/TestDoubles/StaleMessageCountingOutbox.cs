#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// An outbox that implements both outbox interfaces and not <see cref="IAmATopicCountingOutbox"/>, and records each
/// call of its counts. It does not derive from <see cref="InMemoryOutbox"/>, which offers the per-topic count.
/// It completes a released or cancelled call's task, on a pool thread, through a <see cref="TaskCompletionSource{TResult}"/> created
/// without <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>, unlike the other gating doubles of this
/// project. With no <see cref="SynchronizationContext"/> current, the rest of the count's task then runs inline, so it
/// has ended before <see cref="CallEnded"/> completes.
/// </summary>
public class StaleMessageCountingOutbox : IAmAnOutboxSync<Message, CommittableTransaction>, IAmAnOutboxAsync<Message, CommittableTransaction>
{
    private static readonly TimeSpan s_threadBlockCeiling = TimeSpan.FromSeconds(30);

    private readonly ConcurrentQueue<StaleCountCall> _countCalls = new();
    private readonly TimeProvider? _clock;
    private readonly TaskCompletionSource<bool> _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<Func<CancellationToken, Task<int>>> _answers = new();
    private readonly ConcurrentQueue<TaskCompletionSource<int>> _blocked = new();
    private readonly ManualResetEventSlim _threadReleased = new(false);
    private volatile int _result;

    /// <summary>Creates the double.</summary>
    /// <param name="clock">When given, each recorded call carries the time on this clock when it was made.</param>
    public StaleMessageCountingOutbox(TimeProvider? clock = null)
    {
        _clock = clock;
    }

    /// <summary>Each call of the overall count, sync or async, in the order the calls were made.</summary>
    public IReadOnlyCollection<StaleCountCall> CountCalls => _countCalls;

    /// <summary>Completes once a call blocked until its token is cancelled, or until released, has ended.</summary>
    public Task CallEnded => _ended.Task;

    /// <summary>Makes the next call of the async count that has no other answer queued block until its token is cancelled.</summary>
    public void BlockUntilCancelled() => _answers.Enqueue(WaitUntilCancelled);

    /// <summary>Makes the next call of the async count that has no other answer queued ignore its token and block until <see cref="Release"/>.</summary>
    public void BlockUntilReleased() => _answers.Enqueue(WaitUntilReleased);

    /// <summary>
    /// Makes the next call of the async count that has no other answer queued ignore its token and block the calling
    /// thread, before it returns a task, until <see cref="ReleaseThread"/> or 30 s of wall-clock time, so a thread
    /// that is never released is not held past the test.
    /// </summary>
    public void BlockThreadUntilReleased() => _answers.Enqueue(BlockThread);

    /// <summary>Lets the thread blocked by <see cref="BlockThreadUntilReleased"/> return its task.</summary>
    public void ReleaseThread() => _threadReleased.Set();

    /// <summary>Ends the oldest call blocked until released with the given result, then completes <see cref="CallEnded"/>.</summary>
    /// <param name="count">The number of outstanding messages the released call returns.</param>
    /// <exception cref="InvalidOperationException">No call is blocked until released.</exception>
    public void Release(int count)
    {
        if (!_blocked.TryDequeue(out var answer))
            throw new InvalidOperationException("No call of the async count is blocked until released.");

        Task.Run(() =>
        {
            answer.SetResult(count);
            _ended.TrySetResult(true);
        });
    }

    /// <summary>Sets the result of each call of the async count that has no answer queued.</summary>
    /// <param name="count">The number of outstanding messages the call returns.</param>
    public void Returns(int count) => _result = count;

    public bool ContinueOnCapturedContext { get; set; }

    public IAmABrighterTracer? Tracer
    {
        set { }
    }

    public int GetOutstandingMessageCount(TimeSpan dispatchedSince, RequestContext? requestContext,
        int maxCount = 100, Dictionary<string, object>? args = null)
    {
        _countCalls.Enqueue(new StaleCountCall(dispatchedSince, requestContext, maxCount, args, CancellationToken.None, Synchronous: true, _clock?.GetUtcNow()));
        return 0;
    }

    public Task<int> GetOutstandingMessageCountAsync(TimeSpan dispatchedSince, RequestContext? requestContext,
        int maxCount = 100, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        _countCalls.Enqueue(new StaleCountCall(dispatchedSince, requestContext, maxCount, args, cancellationToken, Synchronous: false, _clock?.GetUtcNow()));
        return _answers.TryDequeue(out var answer) ? answer(cancellationToken) : Task.FromResult(_result);
    }

    private Task<int> WaitUntilCancelled(CancellationToken cancellationToken)
    {
        var answer = new TaskCompletionSource<int>();
        cancellationToken.Register(() => Task.Run(() =>
        {
            answer.TrySetCanceled(cancellationToken);
            _ended.TrySetResult(true);
        }));
        return answer.Task;
    }

    private Task<int> BlockThread(CancellationToken cancellationToken)
    {
        _threadReleased.Wait(s_threadBlockCeiling);
        return Task.FromResult(_result);
    }

    private Task<int> WaitUntilReleased(CancellationToken cancellationToken)
    {
        var answer = new TaskCompletionSource<int>();
        _blocked.Enqueue(answer);
        return answer.Task;
    }

    public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
        => throw new NotImplementedException();

    public void Add(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
        => throw new NotImplementedException();

    public Task AddAsync(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task AddAsync(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
        => throw new NotImplementedException();

    public Task DeleteAsync(Id[] messageIds, RequestContext requestContext,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IEnumerable<Message> DispatchedMessages(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100, int pageNumber = 1, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
        => throw new NotImplementedException();

    public Task<IEnumerable<Message>> DispatchedMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100, int pageNumber = 1, int outboxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Message Get(Id messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null)
        => throw new NotImplementedException();

    public IEnumerable<Message> Get(IEnumerable<Id> messageIds, RequestContext requestContext,
        int outBoxTimeout = -1, Dictionary<string, object>? args = null)
        => throw new NotImplementedException();

    public Task<Message> GetAsync(Id messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<Message>> GetAsync(IEnumerable<Id> messageIds, RequestContext requestContext,
        int outBoxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public void MarkDispatched(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null)
        => throw new NotImplementedException();

    public Task MarkDispatchedAsync(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task MarkDispatchedAsync(IEnumerable<Id> ids, RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IEnumerable<Message> OutstandingMessages(TimeSpan dispatchedSince, RequestContext? requestContext,
        int pageSize = 100, int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null,
        Dictionary<string, object>? args = null)
        => throw new NotImplementedException();

    public Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince,
        RequestContext requestContext, int pageSize = 100, int pageNumber = 1,
        IEnumerable<RoutingKey>? trippedTopics = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

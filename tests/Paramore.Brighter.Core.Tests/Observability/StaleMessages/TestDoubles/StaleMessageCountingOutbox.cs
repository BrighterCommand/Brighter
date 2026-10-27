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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// An outbox that implements both outbox interfaces and not <see cref="IAmATopicCountingOutbox"/>, and records each
/// call of its counts. It does not derive from <see cref="InMemoryOutbox"/>, which offers the per-topic count.
/// It completes a released or cancelled call's task, on a pool thread or, for <see cref="ReleaseAt"/>, on the thread
/// that moves the clock, through a <see cref="TaskCompletionSource{TResult}"/> created
/// without <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>, unlike the other gating doubles of this
/// project. It completes the task only once every task it has returned is awaited or complete, and with no
/// <see cref="SynchronizationContext"/> current, so the rest of the count's task runs inline and has ended before
/// <see cref="CallEnded"/> completes. It keeps each message added one at a time, returns it when asked by id, and
/// records the ids added and marked as dispatched, so a test can post through it.
/// </summary>
public class StaleMessageCountingOutbox : IAmAnOutboxSync<Message, CommittableTransaction>, IAmAnOutboxAsync<Message, CommittableTransaction>
{
    private static readonly TimeSpan s_threadBlockCeiling = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_blockedCallWait = TimeSpan.FromSeconds(5);

    // A task keeps the continuation of the code that awaits it in this field, which has no public counterpart.
    private static readonly FieldInfo s_continuation =
        typeof(Task).GetField("m_continuationObject", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Task no longer keeps its continuation in m_continuationObject.");

    private readonly ConcurrentQueue<StaleCountCall> _countCalls = new();
    private readonly ConcurrentDictionary<Id, Message> _messages = new();
    private readonly ConcurrentQueue<Id> _adds = new();
    private readonly ConcurrentQueue<Id> _marks = new();
    private readonly ConcurrentQueue<Task> _returned = new();
    private readonly TimeProvider? _clock;
    private readonly TaskCompletionSource<bool> _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<Func<CancellationToken, Task<int>>> _answers = new();
    private readonly ConcurrentQueue<TaskCompletionSource<int>> _blocked = new();
    // An answer leaves its call to be released or cancelled only once the call's task is recorded as returned.
    private readonly ConcurrentQueue<Action> _exposures = new();
    private readonly ManualResetEventSlim _threadReleased = new(false);
    private readonly ManualResetEventSlim _callbackReleased = new(false);
    private readonly ManualResetEventSlim _syncCountReleased = new(false);
    private readonly TaskCompletionSource<bool> _callbackBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<IEnumerable<Message>> _outstandingMessagesReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile int _result;
    private volatile bool _blockSyncCount;
    private int _blockedSyncMaxCount;
    private int _outstandingMessagesCalls;
    private ITimer? _releaseTimer;

    /// <summary>Creates the double.</summary>
    /// <param name="clock">When given, each recorded call carries the time on this clock when it was made.</param>
    public StaleMessageCountingOutbox(TimeProvider? clock = null)
    {
        _clock = clock;
    }

    /// <summary>Each call of the overall count, sync or async, in the order the calls were made.</summary>
    public IReadOnlyCollection<StaleCountCall> CountCalls => _countCalls;

    /// <summary>The id of each message added one at a time, sync or async, in the order the messages were added.</summary>
    public IReadOnlyCollection<Id> Adds => _adds;

    /// <summary>The id of each message marked as dispatched one at a time, sync or async, in the order of the marks.</summary>
    public IReadOnlyCollection<Id> Marks => _marks;

    /// <summary>The number of calls of <see cref="OutstandingMessagesAsync"/> so far.</summary>
    public int OutstandingMessagesCalls => Volatile.Read(ref _outstandingMessagesCalls);

    /// <summary>Completes once a call blocked until its token is cancelled, or until released, has ended.</summary>
    public Task CallEnded => _ended.Task;

    /// <summary>Completes once the cancellation callback registered by <see cref="BlockCancellationCallbackUntilReleased"/> is blocked.</summary>
    public Task CallbackBlocked => _callbackBlocked.Task;

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

    /// <summary>Makes the next call of the async count that has no other answer queued fail with <paramref name="exception"/>.</summary>
    /// <param name="exception">The exception the call's task faults with.</param>
    public void Throws(Exception exception) => _answers.Enqueue(_ => Task.FromException<int>(exception));

    /// <summary>
    /// Makes the next call of the async count that has no other answer queued ignore its token, block until
    /// <see cref="Release"/> and then throw <paramref name="exception"/>, as an outbox's own async code does, so an
    /// <see cref="OperationCanceledException"/> leaves the call's task cancelled rather than faulted.
    /// </summary>
    /// <param name="exception">The exception the call throws once released.</param>
    public void ThrowsWhenReleased(Exception exception) =>
        _answers.Enqueue(async cancellationToken =>
        {
            await WaitUntilReleased(cancellationToken);
            throw exception;
        });

    /// <summary>
    /// Makes the next call of the async count that has no other answer queued register a cancellation callback that
    /// blocks the thread cancelling its token until <see cref="ReleaseCallback"/> or 30 s of wall-clock time, and then
    /// ends the call as cancelled. The call runs until then.
    /// </summary>
    public void BlockCancellationCallbackUntilReleased() => _answers.Enqueue(BlockCallback);

    /// <summary>
    /// Makes each call of the sync count with <paramref name="maxCount"/>, once recorded, block the calling thread until
    /// <see cref="ReleaseSyncCount"/> or 30 s of wall-clock time, so a thread that is never released is not held past the test.
    /// </summary>
    /// <param name="maxCount">The maximum count of the calls to block.</param>
    public void BlockSyncCountUntilReleased(int maxCount)
    {
        _blockedSyncMaxCount = maxCount;
        _blockSyncCount = true;
    }

    /// <summary>Lets the calls blocked by <see cref="BlockSyncCountUntilReleased"/> return.</summary>
    public void ReleaseSyncCount() => _syncCountReleased.Set();

    /// <summary>Lets the thread blocked by <see cref="BlockThreadUntilReleased"/> return its task.</summary>
    public void ReleaseThread() => _threadReleased.Set();

    /// <summary>Lets the callback blocked by <see cref="BlockCancellationCallbackUntilReleased"/> go on.</summary>
    public void ReleaseCallback() => _callbackReleased.Set();

    /// <summary>Ends the oldest call blocked until released with the given result, then completes <see cref="CallEnded"/>.</summary>
    /// <param name="count">The number of outstanding messages the released call returns.</param>
    /// <exception cref="InvalidOperationException">
    /// No call is blocked until released, or a returned task is not awaited, within 5 s of wall-clock time.
    /// </exception>
    public void Release(int count)
    {
        var answer = OldestBlockedCall();
        WaitUntilEveryCallIsAwaited();
        Task.Run(() =>
        {
            answer.SetResult(count);
            _ended.TrySetResult(true);
        });
    }

    /// <summary>
    /// Ends the oldest call blocked until released with the given result, inside the callback of a timer created now
    /// on the clock given to the double and with no <see cref="SynchronizationContext"/> current, then completes
    /// <see cref="CallEnded"/>. The count's task has ended before the callback returns, and a timer created before the
    /// monitor's fires before the monitor's timers that are due at the same time. The double keeps the timer, so it is
    /// not collected before it fires.
    /// </summary>
    /// <param name="dueTime">How long after now, on the clock, the call ends.</param>
    /// <param name="count">The number of outstanding messages the released call returns.</param>
    public void ReleaseAt(TimeSpan dueTime, int count) =>
        _releaseTimer = _clock!.CreateTimer(_ => WithNoSynchronizationContext(() =>
        {
            var answer = OldestBlockedCall();
            WaitUntilEveryCallIsAwaited();
            answer.SetResult(count);
            _ended.TrySetResult(true);
        }), null, dueTime, System.Threading.Timeout.InfiniteTimeSpan);

    /// <summary>Ends every call of <see cref="OutstandingMessagesAsync"/>, made or to come, with no messages.</summary>
    public void ReleaseOutstandingMessages() => _outstandingMessagesReleased.TrySetResult([]);

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
        if (_blockSyncCount && maxCount == _blockedSyncMaxCount)
            _syncCountReleased.Wait(s_threadBlockCeiling);
        return 0;
    }

    public Task<int> GetOutstandingMessageCountAsync(TimeSpan dispatchedSince, RequestContext? requestContext,
        int maxCount = 100, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        _countCalls.Enqueue(new StaleCountCall(dispatchedSince, requestContext, maxCount, args, cancellationToken, Synchronous: false, _clock?.GetUtcNow()));
        var call = _answers.TryDequeue(out var answer) ? answer(cancellationToken) : Task.FromResult(_result);
        RecordReturned(call);
        while (_exposures.TryDequeue(out var expose))
            expose();
        return call;
    }

    private Task<int> WaitUntilCancelled(CancellationToken cancellationToken)
    {
        var answer = new TaskCompletionSource<int>();
        _exposures.Enqueue(() => cancellationToken.Register(() => Task.Run(() =>
        {
            WaitUntilEveryCallIsAwaited();
            answer.TrySetCanceled(cancellationToken);
            _ended.TrySetResult(true);
        })));
        return answer.Task;
    }

    private Task<int> BlockCallback(CancellationToken cancellationToken)
    {
        var answer = new TaskCompletionSource<int>();
        _exposures.Enqueue(() => cancellationToken.Register(() =>
        {
            _callbackBlocked.TrySetResult(true);
            _callbackReleased.Wait(s_threadBlockCeiling);
            WaitUntilEveryCallIsAwaited();
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
        _exposures.Enqueue(() => _blocked.Enqueue(answer));
        return answer.Task;
    }

    // The clock runs a timer's callback on the thread that moves it, whose test-framework context would defer the
    // rest of the count's task.
    private static void WithNoSynchronizationContext(Action action)
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    // A call is recorded before its task is blocked, so a release made as soon as the call is seen waits for the block.
    private TaskCompletionSource<int> OldestBlockedCall()
    {
        TaskCompletionSource<int>? answer = null;
        if (!SpinWait.SpinUntil(() => _blocked.TryDequeue(out answer), s_blockedCallWait))
            throw new InvalidOperationException("No call of the async count is blocked until released.");

        return answer!;
    }

    /// <summary>Records a task a count returns, so <see cref="WaitUntilEveryCallIsAwaited"/> waits until it is awaited.</summary>
    /// <param name="call">The task the count returns.</param>
    protected void RecordReturned(Task call) => _returned.Enqueue(call);

    /// <summary>Waits until every task recorded as returned is awaited or complete.</summary>
    /// <remarks>
    /// The count's code awaits a task some time after the call returns it. A task completed before then leaves the rest
    /// of the count to that code's thread, which can run it after the test has moved the clock.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A returned task is not awaited within 5 s of wall-clock time.</exception>
    protected void WaitUntilEveryCallIsAwaited()
    {
        if (!SpinWait.SpinUntil(
                () => _returned.All(call => call.IsCompleted || s_continuation.GetValue(call) is not null),
                s_blockedCallWait))
            throw new InvalidOperationException("A task returned by a count is not awaited.");
    }

    /// <summary>Keeps the message, so a get by its id returns it, and records its id in <see cref="Adds"/>.</summary>
    public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
    {
        _messages[message.Id] = message;
        _adds.Enqueue(message.Id);
    }

    public void Add(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
        => throw new NotImplementedException();

    /// <summary>Keeps the message and records its id, as the sync add does.</summary>
    public Task AddAsync(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
    {
        Add(message, requestContext, outBoxTimeout, transactionProvider);
        return Task.CompletedTask;
    }

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

    /// <summary>Returns the messages added with the given ids.</summary>
    public IEnumerable<Message> Get(IEnumerable<Id> messageIds, RequestContext requestContext,
        int outBoxTimeout = -1, Dictionary<string, object>? args = null)
        => messageIds.Select(id => _messages[id]).ToArray();

    public Task<Message> GetAsync(Id messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <summary>Returns the messages added with the given ids, as the sync get does.</summary>
    public Task<IEnumerable<Message>> GetAsync(IEnumerable<Id> messageIds, RequestContext requestContext,
        int outBoxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Get(messageIds, requestContext, outBoxTimeout, args));

    /// <summary>Records the id in <see cref="Marks"/>.</summary>
    public void MarkDispatched(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null)
        => _marks.Enqueue(id);

    /// <summary>Records the id in <see cref="Marks"/>, as the sync mark does.</summary>
    public Task MarkDispatchedAsync(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        MarkDispatched(id, requestContext, dispatchedAt, args);
        return Task.CompletedTask;
    }

    public Task MarkDispatchedAsync(IEnumerable<Id> ids, RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IEnumerable<Message> OutstandingMessages(TimeSpan dispatchedSince, RequestContext? requestContext,
        int pageSize = 100, int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null,
        Dictionary<string, object>? args = null)
        => throw new NotImplementedException();

    /// <summary>
    /// Records the call and ignores its token: every call, made or to come, blocks until
    /// <see cref="ReleaseOutstandingMessages"/>, and then returns no messages.
    /// </summary>
    public Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince,
        RequestContext requestContext, int pageSize = 100, int pageNumber = 1,
        IEnumerable<RoutingKey>? trippedTopics = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _outstandingMessagesCalls);
        return _outstandingMessagesReleased.Task;
    }
}

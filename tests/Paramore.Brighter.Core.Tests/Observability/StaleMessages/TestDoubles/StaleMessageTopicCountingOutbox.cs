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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// A <see cref="StaleMessageCountingOutbox"/> that can also count its stale messages per topic, and records each call
/// of its per-topic count. It completes a released per-topic call's task as <see cref="StaleMessageCountingOutbox"/> completes
/// a released overall call's task, so the per-topic count's task has ended before <see cref="TopicCallEnded"/> completes.
/// </summary>
public class StaleMessageTopicCountingOutbox : StaleMessageCountingOutbox, IAmATopicCountingOutbox
{
    private static readonly TimeSpan s_threadBlockCeiling = TimeSpan.FromSeconds(30);

    private readonly ConcurrentQueue<StaleTopicCountCall> _topicCountCalls = new();
    private readonly TimeProvider? _clock;
    private readonly TaskCompletionSource<bool> _topicCallEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<TaskCompletionSource<IReadOnlyDictionary<RoutingKey, int>>> _blockedTopicCalls = new();
    private readonly ManualResetEventSlim _topicThreadReleased = new(false);
    private volatile bool _blockNextTopicCall;
    private volatile bool _blockNextTopicThread;
    private volatile IReadOnlyDictionary<RoutingKey, int>? _topicResult;

    /// <summary>Creates the double.</summary>
    /// <param name="clock">When given, each recorded call carries the time on this clock when it was made.</param>
    public StaleMessageTopicCountingOutbox(TimeProvider? clock = null) : base(clock)
    {
        _clock = clock;
    }

    /// <summary>Each call of the per-topic count, in the order the calls were made.</summary>
    public IReadOnlyCollection<StaleTopicCountCall> TopicCountCalls => _topicCountCalls;

    /// <summary>Completes once a per-topic call blocked until released has ended.</summary>
    public Task TopicCallEnded => _topicCallEnded.Task;

    /// <summary>Makes the next call of the per-topic count ignore its token and block until <see cref="ReleaseTopicCount"/>.</summary>
    public void BlockTopicCountUntilReleased() => _blockNextTopicCall = true;

    /// <summary>
    /// Makes the next call of the per-topic count ignore its token and block the calling thread, before it returns a
    /// task, until <see cref="ReleaseTopicThread"/> or 30 s of wall-clock time, so a thread that is never released is
    /// not held past the test.
    /// </summary>
    public void BlockTopicThreadUntilReleased() => _blockNextTopicThread = true;

    /// <summary>Lets the thread blocked by <see cref="BlockTopicThreadUntilReleased"/> return its task.</summary>
    public void ReleaseTopicThread() => _topicThreadReleased.Set();

    /// <summary>Makes every per-topic call that is not blocked return these counts, instead of 0 for each topic asked for.</summary>
    /// <param name="counts">The number of outstanding messages each call returns for each topic.</param>
    public void ReturnsByTopic(IReadOnlyDictionary<RoutingKey, int> counts) => _topicResult = counts;

    /// <summary>Ends the oldest per-topic call blocked until released with the given counts, then completes <see cref="TopicCallEnded"/>.</summary>
    /// <param name="counts">The number of outstanding messages the released call returns for each topic.</param>
    /// <exception cref="InvalidOperationException">No per-topic call is blocked until released.</exception>
    public void ReleaseTopicCount(IReadOnlyDictionary<RoutingKey, int> counts)
    {
        if (!_blockedTopicCalls.TryDequeue(out var answer))
            throw new InvalidOperationException("No call of the per-topic count is blocked until released.");

        Task.Run(() =>
        {
            answer.SetResult(counts);
            _topicCallEnded.TrySetResult(true);
        });
    }

    public Task<IReadOnlyDictionary<RoutingKey, int>> GetOutstandingMessageCountsByTopicAsync(
        IReadOnlyDictionary<RoutingKey, TimeSpan> dispatchedSinceByTopic,
        RequestContext? requestContext,
        int maxCountPerTopic,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        _topicCountCalls.Enqueue(new StaleTopicCountCall(dispatchedSinceByTopic, requestContext, maxCountPerTopic, args, cancellationToken, _clock?.GetUtcNow()));
        if (_blockNextTopicCall)
        {
            _blockNextTopicCall = false;
            var answer = new TaskCompletionSource<IReadOnlyDictionary<RoutingKey, int>>();
            _blockedTopicCalls.Enqueue(answer);
            return answer.Task;
        }
        if (_blockNextTopicThread)
        {
            _blockNextTopicThread = false;
            _topicThreadReleased.Wait(s_threadBlockCeiling);
        }

        return Task.FromResult(_topicResult
            ?? dispatchedSinceByTopic.Keys.ToDictionary(topic => topic, _ => 0));
    }
}

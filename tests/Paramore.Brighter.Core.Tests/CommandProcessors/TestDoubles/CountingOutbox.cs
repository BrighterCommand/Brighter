#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

/// <summary>
/// An <see cref="InMemoryOutbox"/> that counts how often the mediator asks it for the number of outstanding messages.
/// The mediator makes that call on a background thread, so the counter is thread-safe and
/// <see cref="WaitForOutstandingCountCallsAsync"/> lets a test wait for it. A test can also hold a count open with
/// <see cref="HoldCounts"/>, which keeps the background thread that asked for it busy until <see cref="ReleaseCounts"/>.
/// </summary>
internal sealed class CountingOutbox(TimeProvider timeProvider)
    : InMemoryOutbox(timeProvider),
        IAmAnOutboxSync<Message, CommittableTransaction>,
        IAmAnOutboxAsync<Message, CommittableTransaction>
{
    private static readonly TimeSpan s_longestHold = TimeSpan.FromSeconds(30);

    private readonly ManualResetEventSlim _countGate = new(initialState: true);
    private readonly ManualResetEventSlim _countStarted = new(initialState: false);
    private int _outstandingCountCalls;

    /// <summary>
    /// How many times <c>GetOutstandingMessageCount</c> or <c>GetOutstandingMessageCountAsync</c> has been called
    /// </summary>
    public int OutstandingCountCalls => Volatile.Read(ref _outstandingCountCalls);

    int IAmAnOutboxSync<Message, CommittableTransaction>.GetOutstandingMessageCount(
        TimeSpan dispatchedSince,
        RequestContext? requestContext,
        int maxCount,
        Dictionary<string, object>? args)
    {
        StartCount();
        return base.GetOutstandingMessageCount(dispatchedSince, requestContext, maxCount, args);
    }

    Task<int> IAmAnOutboxAsync<Message, CommittableTransaction>.GetOutstandingMessageCountAsync(
        TimeSpan dispatchedSince,
        RequestContext? requestContext,
        int maxCount,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        StartCount();
        return base.GetOutstandingMessageCountAsync(dispatchedSince, requestContext, maxCount, args, cancellationToken);
    }

    /// <summary>
    /// Holds every count that starts from now on inside the outbox, until <see cref="ReleaseCounts"/> is called.
    /// </summary>
    public void HoldCounts() => _countGate.Reset();

    /// <summary>
    /// Lets any held count, and every later one, go on.
    /// </summary>
    public void ReleaseCounts() => _countGate.Set();

    /// <summary>
    /// Waits until a count has started in this outbox, at any time since the outbox was created.
    /// </summary>
    /// <param name="timeout">How long to wait before giving up</param>
    /// <returns>True if a count has started, false if we timed out</returns>
    public Task<bool> WaitForCountToStartAsync(TimeSpan timeout) => Task.Run(() => _countStarted.Wait(timeout));

    /// <summary>
    /// Waits until the outstanding message count has been requested at least <paramref name="calls"/> times
    /// </summary>
    /// <param name="calls">The number of calls to wait for</param>
    /// <param name="timeout">How long to wait before giving up</param>
    /// <returns>True if the count has been requested at least <paramref name="calls"/> times, false if we timed out</returns>
    public async Task<bool> WaitForOutstandingCountCallsAsync(int calls, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (OutstandingCountCalls < calls && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(10);
        }

        return OutstandingCountCalls >= calls;
    }

    private void StartCount()
    {
        Interlocked.Increment(ref _outstandingCountCalls);
        _countStarted.Set();
        _countGate.Wait(s_longestHold);
    }
}

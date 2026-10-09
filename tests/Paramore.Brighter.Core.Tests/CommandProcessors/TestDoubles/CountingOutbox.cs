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
/// <see cref="WaitForOutstandingCountCallsAsync"/> lets a test wait for it.
/// </summary>
internal sealed class CountingOutbox(TimeProvider timeProvider)
    : InMemoryOutbox(timeProvider),
        IAmAnOutboxSync<Message, CommittableTransaction>,
        IAmAnOutboxAsync<Message, CommittableTransaction>
{
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
        Interlocked.Increment(ref _outstandingCountCalls);
        return base.GetOutstandingMessageCount(dispatchedSince, requestContext, maxCount, args);
    }

    Task<int> IAmAnOutboxAsync<Message, CommittableTransaction>.GetOutstandingMessageCountAsync(
        TimeSpan dispatchedSince,
        RequestContext? requestContext,
        int maxCount,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _outstandingCountCalls);
        return base.GetOutstandingMessageCountAsync(dispatchedSince, requestContext, maxCount, args, cancellationToken);
    }

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
}

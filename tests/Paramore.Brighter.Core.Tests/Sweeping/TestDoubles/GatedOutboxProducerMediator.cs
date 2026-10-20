using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IAmAnOutboxProducerMediator"/> that signals when a sweep reaches it, after the lock is obtained, and
/// holds the sweep until <see cref="Open"/>. It then completes or throws, as told by <see cref="ThrowOnRelease"/>.
/// <see cref="CompleteFirst"/> and <see cref="ThrowFirst"/> queue calls that complete or throw before it gates.
/// The "sweep reached the gate" signal fires only for a call held at the gate, never for a call that passes.
/// </summary>
public sealed class GatedOutboxProducerMediator : SweepOnlyOutboxProducerMediator
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _entered = new(false);
    private readonly ConcurrentQueue<string?> _beforeGate = new();
    private string? _releaseFailure;

    /// <summary>Makes the next <paramref name="count"/> calls complete at once, before the gate closes on later ones.</summary>
    public GatedOutboxProducerMediator CompleteFirst(int count)
    {
        for (var i = 0; i < count; i++) _beforeGate.Enqueue(null);
        return this;
    }

    /// <summary>Makes the next <paramref name="count"/> calls throw at once, before the gate closes on later ones.</summary>
    public GatedOutboxProducerMediator ThrowFirst(int count, string message = "the sweep failed before the gate")
    {
        for (var i = 0; i < count; i++) _beforeGate.Enqueue(message);
        return this;
    }

    /// <summary>Makes a call held at the gate throw, not complete, when it is released.</summary>
    public GatedOutboxProducerMediator ThrowOnRelease(string message = "the sweep failed after the gate")
    {
        Volatile.Write(ref _releaseFailure, message);
        return this;
    }

    /// <summary>Waits until a sweep has been held at the gate.</summary>
    public bool WaitForSweepToStart(TimeSpan timeout) => _entered.Wait(timeout);

    /// <summary>Lets every held and future call through.</summary>
    public void Open() => _gate.TrySetResult();

    public override async Task ClearOutstandingFromOutboxAsync(int amountToClear,
        TimeSpan minimumAge,
        bool useBulk,
        RequestContext requestContext,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        if (_beforeGate.TryDequeue(out var failure))
        {
            if (failure is not null) throw new InvalidOperationException(failure);
            return;
        }

        if (!_gate.Task.IsCompleted)
        {
            _entered.Set();
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var onRelease = Volatile.Read(ref _releaseFailure);
        if (onRelease is not null) throw new InvalidOperationException(onRelease);
    }
}

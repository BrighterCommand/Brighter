#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="InMemoryLock"/> that holds every caller at the door until <see cref="Open"/> is
/// called. The sweeper asks for its lock first thing in a sweep, so this lets a test keep a sweep
/// in flight for as long as it needs. <see cref="PassFirst"/> lets the first calls straight through
/// before it gates. The "sweep reached the gate" signal fires only for a call held at the gate.
/// </summary>
public sealed class GatedDistributedLock : IDistributedLock, IDisposable
{
    private readonly InMemoryLock _inner = new();
    private readonly ManualResetEventSlim _gate = new(false);
    private readonly ManualResetEventSlim _entered = new(false);
    private int _passRemaining;

    /// <summary>Lets the first <paramref name="count"/> calls through before the gate closes on later ones.</summary>
    public GatedDistributedLock PassFirst(int count)
    {
        Interlocked.Exchange(ref _passRemaining, count);
        return this;
    }

    /// <summary>Waits until a sweep has been held at the gate.</summary>
    public bool WaitForSweepToStart(TimeSpan timeout) => _entered.Wait(timeout);

    /// <summary>Lets every held and future caller through.</summary>
    public void Open() => _gate.Set();

    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (!TryPass())
        {
            _entered.Set();
            _gate.Wait(cancellationToken);
        }
        return _inner.ObtainLockAsync(resource, cancellationToken);
    }

    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken) =>
        _inner.ReleaseLockAsync(resource, lockId, cancellationToken);

    public void Dispose()
    {
        _gate.Dispose();
        _entered.Dispose();
    }

    private bool TryPass()
    {
        if (_gate.IsSet) return true;
        while (true)
        {
            var remaining = Volatile.Read(ref _passRemaining);
            if (remaining <= 0) return false;
            if (Interlocked.CompareExchange(ref _passRemaining, remaining - 1, remaining) == remaining) return true;
        }
    }
}

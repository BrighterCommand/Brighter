using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="InMemoryLock"/> that holds every caller at the door until <see cref="Open"/> is
/// called. The sweeper asks for its lock first thing in a sweep, so this lets a test keep a sweep
/// in flight for as long as it needs.
/// </summary>
public sealed class GatedDistributedLock : IDistributedLock, IDisposable
{
    private readonly InMemoryLock _inner = new();
    private readonly ManualResetEventSlim _gate = new(false);
    private readonly ManualResetEventSlim _entered = new(false);

    /// <summary>Waits until a sweep has reached the lock.</summary>
    public bool WaitForSweepToStart(TimeSpan timeout) => _entered.Wait(timeout);

    /// <summary>Lets every held and future caller through.</summary>
    public void Open() => _gate.Set();

    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        _entered.Set();
        _gate.Wait(cancellationToken);
        return _inner.ObtainLockAsync(resource, cancellationToken);
    }

    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken) =>
        _inner.ReleaseLockAsync(resource, lockId, cancellationToken);

    public void Dispose()
    {
        _gate.Dispose();
        _entered.Dispose();
    }
}

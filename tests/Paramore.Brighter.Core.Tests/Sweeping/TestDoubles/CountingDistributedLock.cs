#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IDistributedLock"/> that passes every call to another lock and counts the attempts to obtain it, so a test
/// can tell when a sweeper has tried again.
/// </summary>
public sealed class CountingDistributedLock(IDistributedLock inner) : IDistributedLock
{
    private int _attempts;

    /// <summary>The number of times the lock has been asked for.</summary>
    public int Attempts => Volatile.Read(ref _attempts);

    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _attempts);
        return inner.ObtainLockAsync(resource, cancellationToken);
    }

    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken) =>
        inner.ReleaseLockAsync(resource, lockId, cancellationToken);
}

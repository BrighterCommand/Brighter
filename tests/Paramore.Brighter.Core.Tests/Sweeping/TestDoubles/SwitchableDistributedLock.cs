#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IDistributedLock"/> that grants the lock or reports it unavailable, as the test switches it between sweeps.
/// </summary>
public sealed class SwitchableDistributedLock : IDistributedLock
{
    private volatile bool _available = true;

    /// <summary>Grants the lock to later callers.</summary>
    public void Grant() => _available = true;

    /// <summary>Reports the lock as held elsewhere to later callers.</summary>
    public void MakeUnavailable() => _available = false;

    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(_available ? Guid.NewGuid().ToString() : null);

    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

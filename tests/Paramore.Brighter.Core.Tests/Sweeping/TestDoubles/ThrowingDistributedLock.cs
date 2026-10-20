using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IDistributedLock"/> whose <see cref="ObtainLockAsync"/> throws on every call, as an unreachable lock store would.
/// </summary>
public sealed class ThrowingDistributedLock(string message = "the lock store is unreachable") : IDistributedLock
{
    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(message);

    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

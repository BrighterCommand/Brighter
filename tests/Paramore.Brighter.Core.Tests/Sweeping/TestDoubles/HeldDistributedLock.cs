using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IDistributedLock"/> that another process always holds: every attempt to obtain it fails.
/// </summary>
public sealed class HeldDistributedLock : IDistributedLock
{
    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

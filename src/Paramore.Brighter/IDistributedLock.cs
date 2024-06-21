using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

public interface IDistributedLock
{
    /// <summary>
    /// Attempt to obtain a lock on a resource
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns>The id of the lock that has been acquired or null if no lock was able to be acquired</returns>
    Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken);

    /// <summary>
    /// Attempt to obtain a lock on a resource
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <returns>The id of the lock that has been acquired or null if no lock was able to be acquired</returns>
    string? ObtainLock(string resource);

    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="lockId">The lock Id that was provided when the lock was obtained</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Awaitable Task</returns>
    Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken);

    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="lockId">The lock Id that was provided when the lock was obtained</param>
    /// <returns>Awaitable Task</returns>
    void ReleaseLock(string resource, string lockId);
}

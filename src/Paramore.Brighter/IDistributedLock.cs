using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public interface IDistributedLock
    {
        /// <summary>
        /// Attempt to obtain a lock on a resource
        /// </summary>
        /// <param name="resource">The name of the resource to Lock</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>True if the lock was obtained</returns>
        Task<bool> ObtainLockAsync(string resource, CancellationToken cancellationToken);

        /// <summary>
        /// Attempt to obtain a lock on a resource
        /// </summary>
        /// <param name="resource">The name of the resource to Lock</param>
        /// <returns>True if the lock was obtained</returns>
        bool ObtainLock(string resource);

        /// <summary>
        /// Release a lock
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Awaitable Task</returns>
        Task ReleaseLockAsync(string resource, CancellationToken cancellationToken);

        /// <summary>
        /// Release a lock
        /// </summary>
        /// <param name="resource"></param>
        /// <returns>Awaitable Task</returns>
        void ReleaseLock(string resource);
    }
}

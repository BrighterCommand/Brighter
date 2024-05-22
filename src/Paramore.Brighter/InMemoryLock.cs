using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{

    public class InMemoryLock : IDistributedLock
    {
        private readonly Dictionary<string, SemaphoreSlim> _semaphores = new Dictionary<string, SemaphoreSlim>();

        public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
        {
            var normalisedResourceName = resource.ToLower();
            if (!_semaphores.ContainsKey(normalisedResourceName))
                _semaphores.Add(normalisedResourceName, new SemaphoreSlim(1, 1));

            return (await _semaphores[normalisedResourceName].WaitAsync(TimeSpan.Zero, cancellationToken)) ? "" : null;
        }

        public string? ObtainLock(string resource)
        {
            var normalisedResourceName = resource.ToLower();
            if (!_semaphores.ContainsKey(normalisedResourceName))
                _semaphores.Add(normalisedResourceName, new SemaphoreSlim(1, 1));

            return _semaphores[normalisedResourceName].Wait(TimeSpan.Zero) ? "" : null;
        }

        public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
        {
            ReleaseLock(resource, lockId);
            return Task.CompletedTask;
        }

        public void ReleaseLock(string resource, string lockId)
        {
            var normalisedResourceName = resource.ToLower();
            if (_semaphores.TryGetValue(normalisedResourceName, out SemaphoreSlim semaphore))
                semaphore.Release();
        }
    }
}

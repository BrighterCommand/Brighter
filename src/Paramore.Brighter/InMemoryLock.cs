#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

/// <summary>
/// An in memory provider for Locking, please note this will only provide a lock localised to your process.
/// </summary>
public class InMemoryLock : IDistributedLock
{
    private readonly Dictionary<string, SemaphoreSlim> _semaphores = new Dictionary<string, SemaphoreSlim>();

    /// <summary>
    /// Attempt to obtain a lock on a resource
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns>The id of the lock that has been acquired or null if no lock was able to be acquired</returns>
    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        var normalisedResourceName = resource.ToLower();
        if (!_semaphores.ContainsKey(normalisedResourceName))
            _semaphores.Add(normalisedResourceName, new SemaphoreSlim(1, 1));

        return (await _semaphores[normalisedResourceName].WaitAsync(TimeSpan.Zero, cancellationToken)) ? "" : null;
    }

    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="lockId">The lock Id that was provided when the lock was obtained</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Awaitable Task</returns>
    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        var normalisedResourceName = resource.ToLower();
        if (_semaphores.TryGetValue(normalisedResourceName, out SemaphoreSlim? semaphore))
            semaphore?.Release();
        return Task.CompletedTask;
    }
}

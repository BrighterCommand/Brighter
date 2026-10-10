using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IDistributedLock"/> that always grants the lock and records the value an <see cref="AsyncLocal{T}"/>
/// held each time a sweep asked for it, so a test can see what ambient state a sweep runs with.
/// </summary>
public sealed class AmbientRecordingLock(AsyncLocal<string?> ambient) : IDistributedLock
{
    private readonly ManualResetEventSlim _obtained = new(false);

    public string? AmbientWhenObtained { get; private set; }

    public bool WaitForSweep(TimeSpan timeout) => _obtained.Wait(timeout);

    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        AmbientWhenObtained = ambient.Value;
        _obtained.Set();
        return Task.FromResult<string?>(Guid.NewGuid().ToString());
    }

    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

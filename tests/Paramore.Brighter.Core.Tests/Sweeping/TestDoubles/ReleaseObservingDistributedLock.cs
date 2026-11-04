#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IDistributedLock"/> that, when it is released, records the answer to a question about the world at that
/// moment, so a test can tell what had happened before the release.
/// </summary>
public sealed class ReleaseObservingDistributedLock(IDistributedLock inner, Func<Task<bool>> observe) : IDistributedLock
{
    private int _released;
    private int _observedTrue;

    /// <summary>Whether the lock has been released.</summary>
    public bool Released => Volatile.Read(ref _released) == 1;

    /// <summary>Whether the observation held when the lock was released.</summary>
    public bool ObservationHeldAtRelease => Volatile.Read(ref _observedTrue) == 1;

    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
        => inner.ObtainLockAsync(resource, cancellationToken);

    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        if (await observe()) Volatile.Write(ref _observedTrue, 1);
        await inner.ReleaseLockAsync(resource, lockId, cancellationToken);
        Volatile.Write(ref _released, 1);
    }
}

#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// An <see cref="IDistributedLock"/> that passes every call to another lock and records each one, as its operation and
/// its resource, in the order the calls were made. It records no lock id, which differs from one run to the next.
/// </summary>
public sealed class RecordingDistributedLock(IDistributedLock inner) : IDistributedLock
{
    private readonly ConcurrentQueue<(string Operation, string Resource)> _calls = new();

    /// <summary>Each call of the lock, in the order the calls were made.</summary>
    public IReadOnlyCollection<(string Operation, string Resource)> Calls => _calls;

    /// <summary>Records the call, then passes it to the other lock.</summary>
    public Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        _calls.Enqueue((nameof(ObtainLockAsync), resource));
        return inner.ObtainLockAsync(resource, cancellationToken);
    }

    /// <summary>Records the call, then passes it to the other lock.</summary>
    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        _calls.Enqueue((nameof(ReleaseLockAsync), resource));
        return inner.ReleaseLockAsync(resource, lockId, cancellationToken);
    }
}

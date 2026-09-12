#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// Wraps a real <see cref="IServiceScopeFactory"/> and counts every scope created and every scope
/// disposed, so a test can assert nothing a container-backed factory created is left undisposed
/// (NFR-5) without depending on GC timing. Pair with <see cref="TrackingServiceProvider"/>, which
/// redirects a root <see cref="IServiceProvider"/>'s <see cref="IServiceScopeFactory"/> requests here.
/// </summary>
public sealed class ScopeTracker(IServiceScopeFactory inner) : IServiceScopeFactory
{
    private int _createdCount;
    private int _disposedCount;

    public int CreatedCount => Volatile.Read(ref _createdCount);
    public int DisposedCount => Volatile.Read(ref _disposedCount);

    /// <summary>
    /// Scopes created but not yet disposed. Zero once every pipeline built through the tracked
    /// provider has released its scope.
    /// </summary>
    public int OutstandingCount => CreatedCount - DisposedCount;

    public IServiceScope CreateScope()
    {
        Interlocked.Increment(ref _createdCount);
        return new TrackingScope(inner.CreateScope(), () => Interlocked.Increment(ref _disposedCount));
    }

    private sealed class TrackingScope(IServiceScope inner, Action onDispose) : IServiceScope, IAsyncDisposable
    {
        private int _disposed;

        public IServiceProvider ServiceProvider => inner.ServiceProvider;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) onDispose();
            inner.Dispose();
        }

        //Production on net8+ disposes an IAsyncDisposable scope through DisposeAsync — real MS DI scopes
        //have implemented it since .NET Core 3.0 — so DisposeScope takes the async branch, not
        //scope.Dispose(). Implement it here so the scope accounting runs against the branch a real app
        //takes; count the disposal in whichever shape the caller uses.
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) onDispose();
            if (inner is IAsyncDisposable asyncInner)
                await asyncInner.DisposeAsync().ConfigureAwait(false);
            else
                inner.Dispose();
        }
    }
}

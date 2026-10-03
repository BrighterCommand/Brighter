#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class InMemoryScopedClaimCheckStorage(InMemoryStorageProvider storage) :
    IAmAStorageProvider, IAmAStorageProviderAsync, IDisposable
{
    public bool IsDisposed { get; private set; }
    public IAmABrighterTracer? Tracer { get; set; }

    public void EnsureStoreExists() => ThrowIfDisposed();
    public Task EnsureStoreExistsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.CompletedTask;
    }

    public void Delete(string claimCheck)
    {
        ThrowIfDisposed();
        storage.Delete(claimCheck);
    }

    public async Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        ThrowIfDisposed();
        await storage.DeleteAsync(claimCheck, cancellationToken);
    }

    public Stream Retrieve(string claimCheck)
    {
        ThrowIfDisposed();
        return storage.Retrieve(claimCheck);
    }

    public async Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        ThrowIfDisposed();
        return await storage.RetrieveAsync(claimCheck, cancellationToken);
    }

    public bool HasClaim(string claimCheck) => storage.HasClaim(claimCheck);
    public Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default) =>
        storage.HasClaimAsync(claimCheck, cancellationToken);
    public string Store(Stream stream) => storage.Store(stream);
    public Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken = default) =>
        storage.StoreAsync(stream, cancellationToken);

    public void Dispose() => IsDisposed = true;

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(InMemoryScopedClaimCheckStorage));
    }
}

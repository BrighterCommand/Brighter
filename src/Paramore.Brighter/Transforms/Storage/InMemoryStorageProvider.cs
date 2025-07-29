#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Transforms.Storage;

/// <summary>
/// Provides in-memory storage of a message body, intended for use with testing only
/// </summary>
public class InMemoryStorageProvider : IAmAStorageProvider, IAmAStorageProviderAsync
{
    private const string ClaimCheckProvider = "in-memory";
    private const string BucketName = "in-memory";
    
    private readonly ConcurrentDictionary<string, string> _contents = new();

    /// <inheritdoc cref="IAmAStorageProvider.Tracer"/>
    public IAmABrighterTracer? Tracer { get; set; }

    /// <inheritdoc />
    public Task EnsureStoreExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
       var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Delete, ClaimCheckProvider, BucketName, claimCheck));
        try
        {
            _contents.TryRemove(claimCheck, out _);
            return Task.CompletedTask;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Retrieve, ClaimCheckProvider, BucketName, claimCheck));
        try
        {
            if (!_contents.TryGetValue(claimCheck, out string? value))
            {
                throw new ArgumentOutOfRangeException(nameof(claimCheck), claimCheck, "Could not find the claim check in the store");
            }
                
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            await writer.WriteAsync(value);
#if NETSTANDARD
            await writer.FlushAsync();
#else
            await writer.FlushAsync(cancellationToken);
#endif
            stream.Position = 0;
            return stream;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.HasClaim, ClaimCheckProvider, BucketName, claimCheck));
        try
        {
            return Task.FromResult(_contents.ContainsKey(claimCheck));
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var claimCheck = Uuid.NewAsString();
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Store, ClaimCheckProvider, BucketName, claimCheck, null, stream.Length));
        try
        {
            var reader = new StreamReader(stream);
            
            _contents.TryAdd(claimCheck,
#if NETSTANDARD
            await reader.ReadToEndAsync()
#else
            await reader.ReadToEndAsync(cancellationToken)
#endif
                );
            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public void EnsureStoreExists() { }

    /// <summary>
    /// Delete the luggage identified by the claim check
    /// Used to clean up after luggage is retrieved
    /// </summary>
    /// <param name="claimCheck">The claim check for the luggage</param>
    public void Delete(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Delete, ClaimCheckProvider, BucketName, claimCheck));
        try
        {
            _contents.TryRemove(claimCheck, out _);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Downloads the luggage associated with the claim check
    /// </summary>
    /// <param name="claimCheck">The claim check for the luggage</param>
    /// <returns>The luggage as a stream</returns>
    public Stream Retrieve(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Retrieve, ClaimCheckProvider, BucketName, claimCheck));
        try
        {
            if (!_contents.TryGetValue(claimCheck, out string? value))
            {
                throw new ArgumentOutOfRangeException(nameof(claimCheck), claimCheck, "Could not find the claim check in the store");
            }
                
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(value);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Do we have luggage for this claim check - in case of error or deletion
    /// </summary>
    /// <param name="claimCheck"></param>
    public bool HasClaim(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.HasClaim, ClaimCheckProvider, BucketName, claimCheck));
        try
        {
            return _contents.ContainsKey(claimCheck);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Puts luggage into the store and provides a claim check for that luggage
    /// </summary>
    /// <param name="stream">A stream representing the luggage to check</param>
    /// <returns>A claim check for the luggage stored</returns>
    public string Store(Stream stream)
    {
        var claimCheck = Uuid.NewAsString();
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Store, ClaimCheckProvider, BucketName, claimCheck, null, stream.Length));
        try
        {
            var reader = new StreamReader(stream);
            _contents.TryAdd(claimCheck, reader.ReadToEnd());
            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
}

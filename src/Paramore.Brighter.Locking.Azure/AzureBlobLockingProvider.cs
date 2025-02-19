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

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Locking.Azure;

/// <summary>
/// The Azure Blob provider for distributed locks
/// </summary>
/// <param name="options"></param>
public class AzureBlobLockingProvider(AzureBlobLockingProviderOptions options) : IDistributedLock
{
    private readonly BlobContainerClient _containerClient =
        new BlobContainerClient(options.BlobContainerUri, options.TokenCredential);

    private readonly ILogger _logger = ApplicationLogging.CreateLogger<AzureBlobLockingProviderOptions>();

    /// <summary>
    /// Attempt to obtain a lock on a resource
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns>The id of the lock that has been acquired or null if no lock was able to be acquired</returns>
    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        var client = GetBlobClient(resource);

        // Write if does not exist
        if (!await client.ExistsAsync(cancellationToken))
        {
#if NETSTANDARD
            using var emptyStream = new MemoryStream();
            using var writer = new StreamWriter(emptyStream);
            await writer.WriteAsync(string.Empty);
            await writer.FlushAsync();
            emptyStream.Position = 0;
            await client.UploadAsync(emptyStream, cancellationToken: cancellationToken);
#else
            await using var emptyStream = new MemoryStream();
            await using var writer = new StreamWriter(emptyStream);
            await writer.WriteAsync(string.Empty);
            await writer.FlushAsync(cancellationToken);
            emptyStream.Position = 0;
            await client.UploadAsync(emptyStream, cancellationToken: cancellationToken);
#endif
        }

        try
        {
            var response = await client.GetBlobLeaseClient()
                .AcquireAsync(options.LeaseValidity, cancellationToken: cancellationToken);
            return response.Value.LeaseId;
        }
        catch (RequestFailedException)
        {
            _logger.LogInformation("Could not Acquire Lease on Blob {LockResourceName}", resource);
            return null;
        }
    }

    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="lockId">The lock Id that was provided when the lock was obtained</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Awaitable Task</returns>
    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        var client = GetBlobLeaseClientForResource(resource, lockId);
        if (client == null)
        {
            _logger.LogInformation("No lock found for {LockResourceName}", resource);
            return;
        }

        await client.ReleaseAsync(cancellationToken: cancellationToken);
    }

    private BlobLeaseClient? GetBlobLeaseClientForResource(string resource, string lockId) =>
        GetBlobClient(resource).GetBlobLeaseClient(lockId);

    private BlobClient GetBlobClient(string resource)
    {
        var storageLocation = options.StorageLocationFunc.Invoke(NormaliseResourceName(resource));
        return _containerClient.GetBlobClient(storageLocation);
    }

    private static string NormaliseResourceName(string resourceName) => resourceName.ToLower();
}

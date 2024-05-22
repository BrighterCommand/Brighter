using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Locking.Azure;

public class AzureBlobLockingProvider(AzureBlobLockingProviderOptions options) : IDistributedLock
{
    private readonly BlobContainerClient _containerClient = new BlobContainerClient(options.BlobContainerUri, options.TokenCredential);
    private readonly ILogger _logger = ApplicationLogging.CreateLogger<AzureBlobLockingProviderOptions>();

    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        var client = GetBlobClient(resource);

        // Write if does not exist
        if (!await client.ExistsAsync(cancellationToken))
        {
            await using var emptyStream = new MemoryStream();
            await using var writer = new StreamWriter(emptyStream);
            await writer.WriteAsync(string.Empty);
            await writer.FlushAsync(cancellationToken);
            emptyStream.Position = 0;
            await client.UploadAsync(emptyStream, cancellationToken: cancellationToken);
        }

        try
        {
            var response = await client.GetBlobLeaseClient().AcquireAsync(options.LeaseValidity, cancellationToken: cancellationToken);
            return response.Value.LeaseId;
        }
        catch (RequestFailedException e)
        {
            _logger.LogInformation("Could not Acquire Lease on Blob {LockResourceName}", resource);
            return null;
        }
    }

    public string? ObtainLock(string resource)
    {
        var client = GetBlobClient(resource);
        
        // Write if does not exist
        if (!client.Exists())
        {
            using var emptyStream = new MemoryStream();
            using var writer = new StreamWriter(emptyStream);
            writer.Write(string.Empty);
            writer.Flush();
            emptyStream.Position = 0;
            client.Upload(emptyStream);
        }

        try
        {
            var response = client.GetBlobLeaseClient().Acquire(options.LeaseValidity);
            return response.Value.LeaseId;
        }
        catch (RequestFailedException e)
        {
            _logger.LogInformation("Could not Acquire Lease on Blob {LockResourceName}", resource);
            return null;
        }
    }

    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        var client = GetBlobLeaseClientForResource(resource, lockId);
        if(client == null)
            return;
        await client.ReleaseAsync(cancellationToken: cancellationToken);
    }

    public void ReleaseLock(string resource, string lockId)
    {
        var client = GetBlobLeaseClientForResource(resource, lockId);
        if(client == null)
            return;
        client.Release();
    }

    private BlobLeaseClient? GetBlobLeaseClientForResource(string resource, string lockId)
    {
        return GetBlobClient(resource).GetBlobLeaseClient(lockId);
        
        // _logger.LogInformation("No lock found for {LockResourceName}", resource);
        // return null;
    }
    
    private BlobClient GetBlobClient(string resource)
    {
        var storageLocation = options.StorageLocationFunc.Invoke(NormaliseResourceName(resource));    
        return _containerClient.GetBlobClient(storageLocation);
    }

    private static string NormaliseResourceName(string resourceName) => resourceName.ToLower();
}

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

    private readonly Dictionary<string, string> _leases = new Dictionary<string, string>();

    public async Task<bool> ObtainLockAsync(string resource, CancellationToken cancellationToken)
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
            _leases.Add(NormaliseResourceName(resource), response.Value.LeaseId);
            return true;
        }
        catch (RequestFailedException e)
        {
            _logger.LogInformation("Could not Acquire Lease on Blob {LockResourceName}", resource);
            return false;
        }
    }

    public bool ObtainLock(string resource)
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
            _leases.Add(NormaliseResourceName(resource), response.Value.LeaseId);
            return true;
        }
        catch (RequestFailedException e)
        {
            _logger.LogInformation("Could not Acquire Lease on Blob {LockResourceName}", resource);
            return false;
        }
    }

    public async Task ReleaseLockAsync(string resource, CancellationToken cancellationToken)
    {
        var client = GetBlobLeaseClientForResource(resource);
        if(client == null)
            return;
        await client.ReleaseAsync(cancellationToken: cancellationToken);
        _leases.Remove(NormaliseResourceName(resource));
    }

    public void ReleaseLock(string resource)
    {
        var client = GetBlobLeaseClientForResource(resource);
        if(client == null)
            return;
        client.Release();
        _leases.Remove(NormaliseResourceName(resource));
    }

    private BlobLeaseClient? GetBlobLeaseClientForResource(string resource)
    {
        if (_leases.ContainsKey(NormaliseResourceName(resource)))
            return GetBlobClient(resource).GetBlobLeaseClient(_leases[NormaliseResourceName(resource)]);
        
        _logger.LogInformation("No lock found for {LockResourceName}", resource);
        return null;
    }
    
    private BlobClient GetBlobClient(string resource)
    {
        var storageLocation = options.StorageLocationFunc.Invoke(NormaliseResourceName(resource));    
        return _containerClient.GetBlobClient(storageLocation);
    }

    private static string NormaliseResourceName(string resourceName) => resourceName.ToLower();
}

using Azure.Core;

namespace Paramore.Brighter.Locking.Azure;

public class AzureBlobLockingProviderOptions(
    Uri blobContainerUri,
    TokenCredential tokenCredential
    )
{
    /// <summary>
    /// The URI of the blob container
    /// </summary>
    public Uri BlobContainerUri { get; init; } = blobContainerUri;

    /// <summary>
    /// The Credential to use when writing blobs
    /// </summary>
    public TokenCredential TokenCredential { get; init; } = tokenCredential;

    /// <summary>
    /// The amount of time before the lease automatically expires
    /// </summary>
    public TimeSpan LeaseValidity { get; init; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// The function to provide the location to store the locks inside of the Blob container
    /// </summary>
    public Func<string, string> StorageLocationFunc = (resource) => $"lock-{resource}";
}

using Azure.Core;
using Azure.Storage.Blobs.Models;

namespace Paramore.Brighter.Storage.Azure;

public class AzureBlobArchiveProviderOptions
{
    /// <summary>
    /// The URI of the blob container
    /// </summary>
    public Uri BlobContainerUri;

    /// <summary>
    /// The Credential to use when writing blobs
    /// </summary>
    public TokenCredential TokenCredential;
    
    /// <summary>
    /// The Access Tier of the blobs
    /// </summary>
    public AccessTier AccessTier = AccessTier.Cool;

    /// <summary>
    /// If enable write tags to the blobs
    /// </summary>
    public bool TagBlobs = false;
}

using Azure.Core;

namespace Paramore.Brighter.Storage.Azure;

public class AzureBlobArchiveProviderOptions
{
    public Uri BlobContainerUri;

    public TokenCredential TokenCredential;
}

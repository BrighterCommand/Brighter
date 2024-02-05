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

    /// <summary>
    /// The maximum number of parallel uploads when using parallel
    /// </summary>
    public int MaxConcurrentUploads = 8;

    /// <summary>
    /// The maximum upload size in mb
    /// </summary>
    public int MaxUploadSize = 50;
    
    /// <summary>
    /// The function to arrange the tags to add when storing, please note that <see cref="TagBlobs"/> must be True for these to be used
    /// </summary>
    public Func<Message, Dictionary<string, string>> TagsFunc = (message) => new Dictionary<string, string>()
    {
        { "topic", message.Header.Topic },
        { "correlationId", message.Header.CorrelationId.ToString() },
        { "message_type", message.Header.MessageType.ToString() },
        { "timestamp", message.Header.TimeStamp.ToString() },
        { "content_type", message.Header.ContentType }
    };

    /// <summary>
    /// The function to provide the location to store the message inside of the Blob container
    /// </summary>
    public Func<Message, string> StorageLocationFunc = (message) => $"{message.Id}";
}

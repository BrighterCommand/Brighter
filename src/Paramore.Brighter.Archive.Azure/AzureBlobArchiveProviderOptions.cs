using System.Globalization;
using System.Net.Mime;
using Azure.Core;
using Azure.Storage.Blobs.Models;

namespace Paramore.Brighter.Storage.Azure;

public class AzureBlobArchiveProviderOptions(
    Uri blobContainerUri,
    TokenCredential tokenCredential,
    AccessTier accessTier,
    bool tagBlobs,
    int maxConcurrentUploads = 8,
    int maxUploadSize = 50
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
    /// The Access Tier of the blobs
    /// </summary>
    public AccessTier AccessTier { get; init; } = accessTier;

    /// <summary>
    /// If enable write tags to the blobs
    /// </summary>
    public bool TagBlobs { get; init; } = tagBlobs;

    /// <summary>
    /// The maximum number of parallel uploads when using parallel
    /// </summary>
    public int MaxConcurrentUploads { get; init; } = maxConcurrentUploads;

    /// <summary>
    /// The maximum upload size in mb
    /// </summary>
    public int MaxUploadSize { get; init; } = maxUploadSize;

    /// <summary>
    /// The function to arrange the tags to add when storing, please note that <see cref="TagBlobs"/> must be True for these to be used
    /// </summary>
    public Func<Message, Dictionary<string, string?>> TagsFunc = (message) => new Dictionary<string, string?>()
    {
        { "topic", message.Header.Topic },
        { "correlationId", message.Header.CorrelationId?.ToString() },
        { "message_type", message.Header.MessageType.ToString() },
        { "timestamp", message.Header.TimeStamp.ToString(CultureInfo.InvariantCulture) },
        { "content_type", message.Header.ContentType is not null ? message.Header.ContentType.ToString() : MediaTypeNames.Text.Plain }
    };

    /// <summary>
    /// The function to provide the location to store the message inside of the Blob container
    /// </summary>
    public Func<Message, string> StorageLocationFunc = (message) => $"{message.Id}";
}

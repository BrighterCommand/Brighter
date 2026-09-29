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
    /// Gets or sets the function that creates blob tags when <see cref="TagBlobs"/> is enabled.
    /// </summary>
    /// <value>A function mapping a message to blob tags. Defaults to the topic, correlation ID, message type, timestamp, and content type.</value>
    public Func<Message, Dictionary<string, string?>> TagsFunc { get; set; } = (message) => new Dictionary<string, string?>()
    {
        { "topic", message.Header.Topic },
        { "correlationId", message.Header.CorrelationId?.ToString() },
#pragma warning disable CS0618 // Preserve the legacy message type for transport compatibility.
        { "message_type", message.Header.MessageType.ToString() },
#pragma warning restore CS0618
        { "timestamp", message.Header.TimeStamp.ToString(CultureInfo.InvariantCulture) },
        { "content_type", message.Header.ContentType is not null ? message.Header.ContentType.ToString() : MediaTypeNames.Text.Plain }
    };

    /// <summary>
    /// Gets or sets the function that selects the message location inside the blob container.
    /// </summary>
    /// <value>A function mapping a message to a blob name. Defaults to the message ID.</value>
    public Func<Message, string> StorageLocationFunc { get; set; } = (message) => $"{message.Id}";
}

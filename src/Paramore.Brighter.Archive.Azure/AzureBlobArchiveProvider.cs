using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Paramore.Brighter.Storage.Azure;

public class AzureBlobArchiveProvider : IAmAnArchiveProvider
{
    private BlobContainerClient _containerClient;
    private readonly AzureBlobArchiveProviderOptions _options;

    public AzureBlobArchiveProvider(AzureBlobArchiveProviderOptions options)
    {
        _containerClient = new BlobContainerClient(options.BlobContainerUri, options.TokenCredential);
        _options = options;
    }

    /// <summary>
    /// Send a Message to the archive provider
    /// </summary>
    /// <param name="message">Message to send</param>
    public void ArchiveMessage(Message message)
    {
        var blobClient = _containerClient.GetBlobClient(message.Id.ToString());

        var alreadyUploaded = blobClient.Exists();

        if (!alreadyUploaded.Value)
        {
            var opts = GetUploadOptions(message);
            blobClient.Upload(BinaryData.FromBytes(message.Body.Bytes), opts);
        }
    }

    /// <summary>
    /// Send a Message to the archive provider
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    public async Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var blobClient = _containerClient.GetBlobClient(message.Id.ToString());

        var alreadyUploaded = await blobClient.ExistsAsync(cancellationToken);
        if (!alreadyUploaded.Value)
        {
            var opts = GetUploadOptions(message);
            await blobClient.UploadAsync(BinaryData.FromBytes(message.Body.Bytes), opts, cancellationToken);
        }
    }

    private BlobUploadOptions GetUploadOptions(Message message)
    {
        var opts = new BlobUploadOptions()
        {
            AccessTier = _options.AccessTier
        };

        if (_options.TagBlobs)
        {
            opts.Tags = new Dictionary<string, string>()
            {
                { "topic", message.Header.Topic },
                { "correlationId", message.Header.CorrelationId.ToString() },
                { "message_type", message.Header.MessageType.ToString() },
                { "timestamp", message.Header.TimeStamp.ToString() },
                { "content_type", message.Header.ContentType }
            };
        }

        return opts;
    }
}

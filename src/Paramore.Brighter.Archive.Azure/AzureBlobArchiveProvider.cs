using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Azure;

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
    public async Task<Guid?> ArchiveMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var blobClient = _containerClient.GetBlobClient(message.Id.ToString());

        var alreadyUploaded = await blobClient.ExistsAsync(cancellationToken);
        if (!alreadyUploaded.Value)
        {
            var opts = GetUploadOptions(message);
            await blobClient.UploadAsync(BinaryData.FromBytes(message.Body.Bytes), opts, cancellationToken);
        }
        return message.Id;
    }

    public async Task<Guid[]> ArchiveMessagesAsync(Message[] messages, CancellationToken cancellationToken)
    {
        var uploads = new Queue<Task<Guid?>>();

        foreach (var message in messages)
        {
            uploads.Enqueue(ArchiveMessageAsync(message, cancellationToken));
        }

        var results = await Task.WhenAll(uploads);
        return results.Where(r => r.HasValue).Select(r => r.Value).ToArray();

    }

    private BlobUploadOptions GetUploadOptions(Message message)
    {
        var opts = new BlobUploadOptions()
        {
            AccessTier = _options.AccessTier,
            TransferOptions = new StorageTransferOptions
            {
                // Set the maximum number of workers that 
                // may be used in a parallel transfer.
                MaximumConcurrency = _options.MaxConcurrentUploads,

                // Set the maximum length of a transfer to 50MB.
                MaximumTransferSize = _options.MaxUploadSize * 1024 * 1024
            }
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

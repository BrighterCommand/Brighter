using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Storage.Azure;

public class AzureBlobArchiveProvider(AzureBlobArchiveProviderOptions options) : IAmAnArchiveProvider
{
    private readonly BlobContainerClient _containerClient = new BlobContainerClient(options.BlobContainerUri, options.TokenCredential);
    private readonly ILogger _logger = ApplicationLogging.CreateLogger<AzureBlobArchiveProvider>();

    /// <summary>
    /// Send a Message to the archive provider
    /// </summary>
    /// <param name="message">Message to send</param>
    public void ArchiveMessage(Message message)
    {
        var blobClient = GetBlobClient(message);

        var alreadyUploaded = blobClient.Exists();

        if (alreadyUploaded.Value)
        {
            _logger.LogDebug("Message with Id {MessageId} has already been uploaded", message.Id);
            return;
        }

        if (message.Body.Bytes is null) throw new AggregateException("Messages must have a body to be archived");

        var opts = GetUploadOptions(message);
        blobClient.Upload(BinaryData.FromBytes(message.Body.Bytes), opts);
    }

    /// <summary>
    /// Send a Message to the archive provider
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    public async Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var blobClient = GetBlobClient(message);

        var alreadyUploaded = await blobClient.ExistsAsync(cancellationToken);
        if (alreadyUploaded.Value)
        {
            _logger.LogDebug("Message with Id {MessageId} has already been uploaded", message.Id);
            return;
        }
        
        if (message.Body.Bytes is null) throw new AggregateException("Messages must have a body to be archived");

        var opts = GetUploadOptions(message);
        await blobClient.UploadAsync(BinaryData.FromBytes(message.Body.Bytes), opts, cancellationToken);
    }

    /// <summary>
    /// Archive messages in Parallel
    /// </summary>
    /// <param name="messages">Messages to send</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns>IDs of successfully archived messages</returns>
    public async Task<Id[]> ArchiveMessagesAsync(Message[] messages, CancellationToken cancellationToken)
    {
        var uploads = new Queue<Task<Id>>();

        foreach (var message in messages)
        {
            uploads.Enqueue(UploadSafe(message, cancellationToken)!);
        }

        var results = await Task.WhenAll(uploads);
#pragma warning disable CS8629 // Nullable value type may be null.
        return results.Where(r => !string.IsNullOrEmpty(r)).Select(r => r).ToArray();
#pragma warning restore CS8629 // Nullable value type may be null.

    }

    private async Task<Id?> UploadSafe(Message message, CancellationToken cancellationToken)
    {
        try
        {
            await ArchiveMessageAsync(message, cancellationToken);
            return message.Id;
        }
        catch(Exception e)
        {
            _logger.LogError(e, "Error archiving message with Id {MessageId}", message.Id);
            return null;
        }
    }

    private BlobClient GetBlobClient(Message message)
    {
        var storageLocation = options.StorageLocationFunc.Invoke(message);
        _logger.LogDebug("Uploading Message with Id {MessageId} to {ArchiveLocation}", message.Id, storageLocation);
        return _containerClient.GetBlobClient(storageLocation);
    }
    
    private BlobUploadOptions GetUploadOptions(Message message)
    {
        var opts = new BlobUploadOptions()
        {
            AccessTier = options.AccessTier,
            TransferOptions = new StorageTransferOptions
            {
                // Set the maximum number of workers that 
                // may be used in a parallel transfer.
                MaximumConcurrency = options.MaxConcurrentUploads,

                // Set the maximum length of a transfer to 50MB.
                MaximumTransferSize = options.MaxUploadSize * 1024 * 1024
            }
        };

        if (options.TagBlobs)
            opts.Tags = options.TagsFunc.Invoke(message);

        return opts;
    }
}

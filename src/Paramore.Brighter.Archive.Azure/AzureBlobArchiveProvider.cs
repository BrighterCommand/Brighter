using Azure.Storage;
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

    /// <summary>
    /// Archive messages in Parallel
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns>IDs of successfully archived messages</returns>
    public async Task<Guid[]> ArchiveMessagesAsync(Message[] messages, CancellationToken cancellationToken)
    {
        var uploads = new Queue<Task<Guid?>>();

        foreach (var message in messages)
        {
            uploads.Enqueue(UploadSafe(message, cancellationToken));
        }

        var results = await Task.WhenAll(uploads);
        return results.Where(r => r.HasValue).Select(r => r.Value).ToArray();

    }

    private async Task<Guid?> UploadSafe(Message message, CancellationToken cancellationToken)
    {
        try
        {
            await ArchiveMessageAsync(message, cancellationToken);
            return message.Id;
        }
        catch(Exception e)
        {
            
            return null;
        }
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
            opts.Tags = _options.TagsFunc.Invoke(message);

        return opts;
    }
}

using Azure.Storage.Blobs;

namespace Paramore.Brighter.Storage.Azure;

public class AzureBlobArchiveProvider : IAmAnArchiveProvider
{
    private BlobContainerClient _containerClient;

    public AzureBlobArchiveProvider(AzureBlobArchiveProviderOptions options)
    {
        _containerClient = new BlobContainerClient(options.BlobContainerUri, options.TokenCredential);
    }

    public void ArchiveMessage(Message message)
    {
        var blobClient = _containerClient.GetBlobClient(message.Id.ToString());
        
        blobClient.Upload(BinaryData.FromBytes(message.Body.Bytes));
    }

    public async Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var blobClient = _containerClient.GetBlobClient(message.Id.ToString());

        // var options = new Dictionary<string, string>()
        //     {
        //         { "topic", message.Header.Topic },
        //         { "correlationId", message.Header.CorrelationId.ToString() },
        //         { "message_type", message.Header.MessageType.ToString() },
        //         { "timestamp", message.Header.TimeStamp.ToString() },
        //         { "content_type", message.Header.ContentType }
        //     };
        //
        // await blobClient.SetTagsAsync(options, cancellationToken: cancellationToken);

        // await blobClient.UploadAsync(BinaryData.FromString(message.Body.Value));
        await blobClient.UploadAsync(BinaryData.FromBytes(message.Body.Bytes), cancellationToken);
    }
}

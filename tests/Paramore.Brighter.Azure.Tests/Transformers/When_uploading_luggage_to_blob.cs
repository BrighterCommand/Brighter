using Azure.Identity;
using Azure.Storage.Blobs;
using Paramore.Brighter.Azure.Tests.Helpers;
using Paramore.Brighter.Transformers.Azure;

namespace Paramore.Brighter.Azure.Tests.Transformers;

public class AzureBlobUploadTests : IAsyncDisposable 
{
    private readonly BlobContainerClient _client;
    private readonly Uri _bucketUrl;

    public AzureBlobUploadTests()
    {
        //arrange
        var bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        _bucketUrl = new Uri($"{TestHelper.BlobLocation}{bucketName}");

        _client = new BlobContainerClient(_bucketUrl, new AzureCliCredential());
    }
    
    [Test]
    public async Task When_uploading_luggage_to_blob()
    {
        //arrange
        var luggageStore = new AzureBlobLuggageStore(new AzureBlobLuggageOptions
        {
            ContainerUri = _bucketUrl,
            Credential = new AzureCliCredential()
        });
        
        await luggageStore.EnsureStoreExistsAsync();
        
        //act
        //Upload the test stream to Azure
        const string testContent = "Well, always know that you shine Brighter";
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteAsync(testContent);
        await streamWriter.FlushAsync();
        stream.Position = 0;

        var claim = luggageStore.Store(stream);

        //assert
        //do we have a claim?
        await Assert.That(luggageStore.HasClaim(claim)).IsTrue();
        
        //check for the contents indicated by the claim id on S3
        var result = luggageStore.Retrieve(claim);
        var resultAsString = await new StreamReader(result).ReadToEndAsync();
        await Assert.That(resultAsString).IsEqualTo(testContent);

        luggageStore.Delete(claim);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DeleteAsync();
    }
}

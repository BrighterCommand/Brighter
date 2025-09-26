using System;
using System.IO;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Tranformers.Gcp;

namespace Paramore.Brighter.Gcp.Tests.Transformers;

[Trait("Category", "GCS")]
public class LuggageUploadTests : IDisposable
{
    private readonly string _bucketName;
    private readonly GcsLuggageOptions _luggageStoreOptions;
    private readonly GcsLuggageStore _luggageStore;

    public LuggageUploadTests()
    {
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        
        _luggageStoreOptions = new GcsLuggageOptions
        {
            ProjectId = GatewayFactory.GetProjectId(),
            Credential = GatewayFactory.GetCredential(),
            BucketName = _bucketName
        };
        
        _luggageStore = new GcsLuggageStore(_luggageStoreOptions);

    }
    
    [Fact]
    public async Task When_uploading_luggage_to_S3()
    {
        //arrange
        await _luggageStore.EnsureStoreExistsAsync();
        
        //act
        //Upload the test stream to S3
        const string testContent = "Well, always know that you shine Brighter";
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteAsync(testContent);
        await streamWriter.FlushAsync();
        stream.Position = 0;

        var claim = await _luggageStore.StoreAsync(stream);

        //assert
        //do we have a claim?
        Assert.True(await _luggageStore.HasClaimAsync(claim));
        
        //check for the contents indicated by the claim id on S3
        var result = await _luggageStore.RetrieveAsync(claim);
        var resultAsString = await new StreamReader(result).ReadToEndAsync();
        Assert.Equal(testContent, resultAsString);

        await _luggageStore.DeleteAsync(claim);

    }

    public void Dispose()
    {
        var client = _luggageStoreOptions.CreateStorageClient();
        client.DeleteBucket(_bucketName);
    }
}

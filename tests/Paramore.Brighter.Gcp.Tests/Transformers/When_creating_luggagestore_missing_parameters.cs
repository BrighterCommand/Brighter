using System;
using System.Threading.Tasks;
using Paramore.Brighter.Tranformers.Gcp;

namespace Paramore.Brighter.Gcp.Tests.Transformers;

[Trait("Category", "GCS")]
public class LuggageUploadMissingParametersTests
{
    private readonly string _bucketName = $"brightertestbucket-{Guid.NewGuid()}";

    [Fact]
    public void When_creating_luggagestore_missing_projectId()
    {
        //arrange
        var exception = Assert.Throws<ConfigurationException>(() =>
        {
            var gcs = new GcsLuggageStore(new GcsLuggageOptions());
            gcs.EnsureStoreExists();
        });

        Assert.NotNull(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void When_creating_luggagestore_missing_bucketName(string? bucketName)
    {
        //arrange
        var exception = Assert.Throws<ConfigurationException>(() =>
        {
            var gcs = new GcsLuggageStore(new GcsLuggageOptions
            {
                ProjectId = Guid.NewGuid().ToString(),
                BucketName = bucketName!
            });
            
            gcs.EnsureStoreExists();
        });
        
        Assert.NotNull(exception);
    }
    [Fact]
    public async Task When_creating_luggagestore_missing_projectId_async()
    {
        //arrange
        var exception = await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            var gcs = new GcsLuggageStore(new GcsLuggageOptions());
            await gcs.EnsureStoreExistsAsync();
        });

        Assert.NotNull(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task When_creating_luggagestore_missing_bucketName_async(string? bucketName)
    {
        //arrange
        var exception = await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            var gcs = new GcsLuggageStore(new GcsLuggageOptions
            {
                ProjectId = Guid.NewGuid().ToString(),
                BucketName = bucketName!
            });
            
            await gcs.EnsureStoreExistsAsync();
        });
        
        Assert.NotNull(exception);
    }
}

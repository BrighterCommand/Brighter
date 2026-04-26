using System;
using System.Threading.Tasks;
using Paramore.Brighter.Transformers.Gcp;

namespace Paramore.Brighter.Gcp.Tests.Transformers;

[Category("GCS")]
public class LuggageUploadMissingParametersTests
{
    private readonly string _bucketName = $"brightertestbucket-{Guid.NewGuid()}";

    [Test]
    public Task When_creating_luggagestore_missing_projectId()
    {
        //arrange
        Assert.ThrowsExactly<ConfigurationException>(() =>
        {
            var gcs = new GcsLuggageStore(new GcsLuggageOptions());
            gcs.EnsureStoreExists();
        });
        return Task.CompletedTask;
    }

    [Test]
    [Arguments("")]
    [Arguments(null)]
    public Task When_creating_luggagestore_missing_bucketName(string? bucketName)
    {
        //arrange
        Assert.ThrowsExactly<ConfigurationException>(() =>
        {
            var gcs = new GcsLuggageStore(new GcsLuggageOptions
            {
                ProjectId = Guid.NewGuid().ToString(),
                BucketName = bucketName!
            });
            
            gcs.EnsureStoreExists();
        });
        return Task.CompletedTask;
    }
    [Test]
    public async Task When_creating_luggagestore_missing_projectId_async()
    {
        //arrange
        var exception = await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            var gcs = new GcsLuggageStore(new GcsLuggageOptions());
            await gcs.EnsureStoreExistsAsync();
        });

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    [Arguments("")]
    [Arguments(null)]
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
        
        await Assert.That(exception).IsNotNull();
    }
}

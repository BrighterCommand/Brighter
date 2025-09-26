using System;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Tranformers.Gcp;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Gcp.Tests.Transformers;

[Trait("Category", "GCS")] 
public class LuggageStoreExistsTests 
{
    [Fact]
    public async Task When_checking_store_that_exists()
    {
        var bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        var options = new GcsLuggageOptions
        {
            Strategy = StorageStrategy.CreateIfMissing,
            BucketName = bucketName,
            ProjectId = GatewayFactory.GetProjectId(),
            Credential = GatewayFactory.GetCredential()
        };
        
        var luggageStore = new GcsLuggageStore(options);
        await luggageStore.EnsureStoreExistsAsync();
        
        // act
        options.Strategy = StorageStrategy.Validate;
        await luggageStore.EnsureStoreExistsAsync();

        
        //teardown
        var client = await options.CreateStorageClientAsync();
        await client.DeleteBucketAsync(bucketName);
    }
    
    [Fact]
    public async Task When_checking_store_that_does_not_exist()
    {
        //act
         var doesNotExist = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
             {
                 var options = new GcsLuggageOptions
                 {
                     Strategy = StorageStrategy.Validate,
                     BucketName =  $"brightertestbucket-{Guid.NewGuid()}",
                     ProjectId = GatewayFactory.GetProjectId(),
                     Credential = GatewayFactory.GetCredential()
                 };
        
                 var luggageStore = new GcsLuggageStore(options);
                 await luggageStore.EnsureStoreExistsAsync();
             });
         
         Assert.NotNull(doesNotExist);
    }
}

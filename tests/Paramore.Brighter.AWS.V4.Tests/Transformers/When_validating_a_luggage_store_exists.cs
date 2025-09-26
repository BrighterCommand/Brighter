using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Paramore.Brighter.Transformers.AWS.V4;
using Paramore.Brighter.Transforms.Storage;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.Transformers;

[Trait("Category", "AWS")] 
[Trait("Fragile", "CI")]
public class S3LuggageStoreExistsTests 
{
    private readonly IHttpClientFactory _httpClientFactory;

    public S3LuggageStoreExistsTests()
    {
        //arrange
        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        _httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    }
    
    [Fact]
    public async Task When_checking_store_that_exists()
    {
        var bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        
        //arrange
        var luggageStore = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), bucketName)
        {
            HttpClientFactory = _httpClientFactory,
            BucketAddressTemplate = CredentialsChain.GetBucketAddressTemple(),
            ACLs = S3CannedACL.Private,
            Tags = [new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" }],
        });
        
        await luggageStore.EnsureStoreExistsAsync();

        //allow bucket endpoint to come into existence
        await Task.Delay(5000);

        //act
        luggageStore = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), bucketName)
        {
            Strategy = StorageStrategy.Validate,
            HttpClientFactory = _httpClientFactory, 
            BucketAddressTemplate = CredentialsChain.GetBucketAddressTemple(),
            Tags = [new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" }],
        });

        Assert.NotNull(luggageStore);
        
        //teardown
        var factory = new AWSClientFactory(GatewayFactory.CreateFactory());
        var client = factory.CreateS3Client();
        await client.DeleteBucketAsync(bucketName);
    }
    
    [Fact]
    public async Task When_checking_store_that_does_not_exist()
    {
        //act
         var doesNotExist = await Catch.ExceptionAsync(async () =>
             {
                 var luggageStore = new S3LuggageStore(
                     new S3LuggageOptions(GatewayFactory.CreateS3Connection(), $"brightertestbucket-{Guid.NewGuid()}")
                     {
                         Strategy = StorageStrategy.Validate,
                         HttpClientFactory = _httpClientFactory,
                         BucketAddressTemplate = CredentialsChain.GetBucketAddressTemple(),
                         ACLs = S3CannedACL.Private,
                         Tags = [new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" }],
                     });

                 await luggageStore.EnsureStoreExistsAsync();
             });
         
         Assert.NotNull(doesNotExist);
         Assert.True(doesNotExist is InvalidOperationException);
    }
}

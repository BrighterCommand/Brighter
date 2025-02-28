using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tranformers.AWS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Transformers;

[Trait("Category", "AWS")] 
[Trait("Fragile", "CI")]
public class S3LuggageStoreExistsTests 
{
    private readonly AmazonS3Client _client;
    private readonly AmazonSecurityTokenServiceClient _stsClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public S3LuggageStoreExistsTests()
    {
        //arrange
        var factory = new AWSClientFactory(GatewayFactory.CreateFactory());
        _client = factory.CreateS3Client();
        _stsClient = factory.CreateStsClient(); 


        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        _httpClientFactory = provider.GetService<IHttpClientFactory>();
    }
    
    [Fact]
    public async Task When_checking_store_that_exists()
    {
        var bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        
        //arrange
        await S3LuggageStore.CreateAsync(
            client: _client,
            bucketName: bucketName,
            storeCreation: S3LuggageStoreCreation.CreateIfMissing,
            httpClientFactory: _httpClientFactory,       
            stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
            bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618
            tags: new List<Tag> {new Tag{Key = "BrighterTests", Value = "S3LuggageUploadTests"}},
            acl: S3CannedACL.Private,
            abortFailedUploadsAfterDays: 1, 
            deleteGoodUploadsAfterDays: 1);

        //allow bucket endpoint to come into existence
        await Task.Delay(5000);

        //act
        var luggageStore = await S3LuggageStore.CreateAsync(
            client: _client,
            bucketName: bucketName,
            storeCreation: S3LuggageStoreCreation.ValidateExists,
            httpClientFactory: _httpClientFactory,
            stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
            bucketRegion: S3Region.EUW1
#pragma warning restore CS0618
            );

        luggageStore.Should().NotBeNull();
        
        //teardown
        await _client.DeleteBucketAsync(bucketName);

    }
    
    [Fact]
    public async Task When_checking_store_that_does_not_exist()
    {
        //act
         var doesNotExist = await Catch.ExceptionAsync(async () =>
             {
                 var luggageStore = await S3LuggageStore.CreateAsync(
                     client: _client,
                     bucketName: $"brightertestbucket-{Guid.NewGuid()}",
                     storeCreation: S3LuggageStoreCreation.ValidateExists,
                     httpClientFactory: _httpClientFactory,
                     stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
                     bucketRegion: S3Region.EUW1
#pragma warning restore CS0618
                     );
             }
         );

         doesNotExist.Should().NotBeNull();
         doesNotExist.Should().BeOfType<InvalidOperationException>();

    }
}

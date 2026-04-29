using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.Transformers.AWS;

namespace Paramore.Brighter.AWS.Tests.Transformers;

[Property("Category", "AWS")]
public class S3LuggageUploadMissingParametersTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _bucketName;
    
    public S3LuggageUploadMissingParametersTests()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        
        _httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
    }

    [Test]
    public async Task When_creating_luggagestore_missing_client()
    {
        //arrange
        var exception = Catch.Exception(() => new S3LuggageStore(new S3LuggageOptions(null!,  null!)));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments(null)]
    public async Task When_creating_luggagestore_missing_bucketName(string? bucketName)
    {
        //arrange
        var exception = Catch.Exception(() => new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(),  bucketName!)));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<ArgumentNullException>();
    }
    
    [Test]
    public async Task When_creating_luggagestore_bad_bucketName()
    {
        //arrange
        var exception = Catch.Exception(() => new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), "A" )));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<ArgumentException>();
    }
    
    [Test]
    public async Task When_creating_luggagestore_missing_httpClient()
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var store = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), _bucketName));
            await store.EnsureStoreExistsAsync();
        });

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<ConfigurationException>();
    }
    
    [Test]
    public async Task When_creating_luggagestore_missing_ACL() 
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var store = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), _bucketName)
            {
                HttpClientFactory = _httpClientFactory,
                BucketAddressTemplate = CredentialsChain.GetBucketAddressTemplate() 
            });
            await store.EnsureStoreExistsAsync();
        });
    
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<ConfigurationException>();
    }
}

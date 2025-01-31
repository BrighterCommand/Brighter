using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.Tranformers.AWS;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using Xunit;
using Policy = Polly.Policy;

namespace Paramore.Brighter.AWS.Tests.Transformers;

public class S3LuggageUploadTests : IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly AmazonSecurityTokenServiceClient _stsClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _bucketName;

    public S3LuggageUploadTests()
    {
        //arrange
        var factory = new AWSClientFactory(GatewayFactory.CreateFactory());
        _client = factory.CreateS3Client();
        _stsClient = factory.CreateStsClient(); 

        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        _httpClientFactory = provider.GetService<IHttpClientFactory>();
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
    }
    
    [Fact]
    public async Task When_uploading_luggage_to_S3()
    {
        //arrange
        var luggageStore = await S3LuggageStore.CreateAsync(
            client: _client,
            bucketName: _bucketName,
            storeCreation: S3LuggageStoreCreation.CreateIfMissing,
            httpClientFactory: _httpClientFactory,       
            stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
            bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618
            tags: new List<Tag> {new Tag{Key = "BrighterTests", Value = "S3LuggageUploadTests"}},
            acl: S3CannedACL.Private,
            policy: GetSimpleHandlerRetryPolicy(), 
            abortFailedUploadsAfterDays: 1, 
            deleteGoodUploadsAfterDays: 1);    
        
        //act
        //Upload the test stream to S3
        var testContent = "Well, always know that you shine Brighter";
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteAsync(testContent);
        await streamWriter.FlushAsync();
        stream.Position = 0;

        var claim = await luggageStore.StoreAsync(stream);

        //assert
        //do we have a claim?
        (await luggageStore.HasClaimAsync(claim)).Should().BeTrue();
        
        //check for the contents indicated by the claim id on S3
        var result = await luggageStore.RetrieveAsync(claim);
        var resultAsString = await new StreamReader(result).ReadToEndAsync();
        resultAsString.Should().Be(testContent);

        await luggageStore.DeleteAsync(claim);

    }
    
    public static AsyncRetryPolicy GetSimpleHandlerRetryPolicy()
    {
        var delay = Backoff.ConstantBackoff(TimeSpan.FromMilliseconds(50), retryCount: 3, fastFirst:true);

        //TODO: Its not worth retrying malformed XML, error code: MalformedXML
        
        return Policy
            .Handle<AmazonS3Exception>(e =>
            {
                switch (e.StatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.BadGateway:
                    case HttpStatusCode.ServiceUnavailable:
                    case HttpStatusCode.GatewayTimeout:
                        return true;
                    default:
                        return false;
                }
            })
            .WaitAndRetryAsync(delay);

    }

    public void Dispose()
    {
        _client.DeleteBucketAsync(_bucketName).GetAwaiter().GetResult();
    }
}

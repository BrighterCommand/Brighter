using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon;
using Amazon.Auth.AccessControlPolicy;
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

public class S3LuggageUploadTests
{
    private readonly AmazonS3Client _client;
    private readonly AmazonSecurityTokenServiceClient _stsClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public S3LuggageUploadTests()
    {
        //arrange
        (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
 
        _client = new AmazonS3Client(credentials, region);
        _stsClient = new AmazonSecurityTokenServiceClient(credentials, region);

        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        _httpClientFactory = provider.GetService<IHttpClientFactory>();
    }
    
    [Fact]
    public async Task When_uploading_luggage_to_S3()
    {
        //arrange
        var luggageStore = await S3LuggageStore.CreateAsync(
            client: _client,
            bucketName: "BrighterTestBucket",
            storeCreation: S3LuggageStoreCreation.CreateIfMissing,
            httpClientFactory: _httpClientFactory,
            stsClient: _stsClient,
            bucketRegion: S3Region.EUWest1,
            tags: new List<Tag>(){new Tag{Key = "BrighterTests", Value = "S3LuggageUploadTests"}},
            acl: S3CannedACL.Private,
            policy: GetSimpleHandlerRetryPolicy(),
            abortFailedUploadsAfterDays: 1,
            deleteGoodUploadsAfterDays:1
            );    
        
        //act
        //Upload the test stream to S3
        //TODO: S3 and large content?? Limits??
        var testContent = "Well, always know that you shine Brighter";
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteAsync(testContent);
        await streamWriter.FlushAsync();
        stream.Position = 0;

        var claim = await luggageStore.UploadAsync(stream);

        //assert
        //do we have a claim?
        (await luggageStore.HasClaimAsync(claim)).Should().BeTrue();
        
        //check for the contents indicated by the claim id on S3
        var result = await luggageStore.DownloadAsync(claim);
        var resultAsString = await new StreamReader(result).ReadToEndAsync();
        resultAsString.Should().Be(testContent);

    }
    
    public static AsyncRetryPolicy GetSimpleHandlerRetryPolicy()
    {
        var delay = Backoff.ConstantBackoff(TimeSpan.FromMilliseconds(50), retryCount: 3, fastFirst:true);

        return Policy
            .Handle<AmazonS3Exception>()
            .WaitAndRetryAsync(delay);

    }
}

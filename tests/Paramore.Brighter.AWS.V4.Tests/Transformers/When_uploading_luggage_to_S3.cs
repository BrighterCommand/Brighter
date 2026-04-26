using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.Transformers.AWS.V4;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using Policy = Polly.Policy;

namespace Paramore.Brighter.AWS.V4.Tests.Transformers;

[Trait("Category", "AWS")]
public class S3LuggageUploadTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _bucketName;

    public S3LuggageUploadTests()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        _httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
    }
    
    [Test]
    public async Task When_uploading_luggage_to_S3()
    {
        //arrange
        var luggageStore = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), _bucketName)
        {
            HttpClientFactory = _httpClientFactory,
            BucketAddressTemplate = CredentialsChain.GetBucketAddressTemplate(),
            ACLs = S3CannedACL.Private,
            Tags = [new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" }],
            RetryPolicy = GetSimpleHandlerRetryPolicy()
        });
        
        await luggageStore.EnsureStoreExistsAsync();
        
        //act
        //Upload the test stream to S3
        const string testContent = "Well, always know that you shine Brighter";
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteAsync(testContent);
        await streamWriter.FlushAsync();
        stream.Position = 0;

        var claim = await luggageStore.StoreAsync(stream);

        //assert
        //do we have a claim?
        await Assert.That((await luggageStore.HasClaimAsync(claim))).IsTrue();
        
        //check for the contents indicated by the claim id on S3
        var result = await luggageStore.RetrieveAsync(claim);
        var resultAsString = await new StreamReader(result).ReadToEndAsync();
        await Assert.That(resultAsString).IsEqualTo(testContent);

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
}

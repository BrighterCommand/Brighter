using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Auth.AccessControlPolicy;
using Amazon.Runtime;
using Amazon.S3;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.Tranformers.AWS;
using Polly;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Transformers;

public class S3LuggageUploadTests
{
    private readonly S3LuggageStore _luggageStore;

    public S3LuggageUploadTests()
    {
        //arrange
        (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
 
        var client = new AmazonS3Client(credentials, region);       
        
        _luggageStore = new S3LuggageStore(
            client:   client,
            bucketName: "BrighterBucket",
            bucketRegion: S3Region.EUWest1,
            tags: new string[]{"BrighterTests"},
            acl: S3CannedACL.Private,
            policy: GetSimpleHandlerRetryPolicy(),
            storeCreation: S3LuggageStoreCreation.CreateIfMissing
            );
    }
    
    [Fact]
    public async Task When_uploading_luggage_to_S3()
    {
        //act
        //Upload the test stream to S3
        //TODO: S3 and large content?? Limits??
        var testContent = "Well, always know that you shine Brighter";
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteAsync(testContent);
        await streamWriter.FlushAsync();
        stream.Position = 0;

        var claim = await _luggageStore.UploadAsync(stream);

        //assert
        //do we have a claim?
        (await _luggageStore.HasClaimAsync(claim)).Should().BeTrue();
        
        //check for the contents indicated by the claim id on S3
        var result = await _luggageStore.DownloadAsync(claim);
        var resultAsString = await new StreamReader(result).ReadToEndAsync();
        resultAsString.Should().Be(testContent);

    }
    
    public static AsyncRetryPolicy GetSimpleHandlerRetryPolicy()
    {
        return Polly.Policy.Handle<Exception>().WaitAndRetryAsync(new[]
        {
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
        });
    }
}

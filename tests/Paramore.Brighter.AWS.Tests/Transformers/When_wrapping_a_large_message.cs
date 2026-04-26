using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transformers.AWS;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.AWS.Tests.Transformers;

[Trait("Category", "AWS")]
public class LargeMessagePayloadWrapTests : IAsyncDisposable 
{
    private string? _id;
    private WrapPipelineAsync<MyLargeCommand>? _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private readonly S3LuggageStore _luggageStore;
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;

    private readonly Publication _publication;

    public LargeMessagePayloadWrapTests()
    {
        //arrange
            
        var mapperRegistry =
            new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(
                _ => new MyLargeCommandMessageMapperAsync())
            );
           
        mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();
            
        _myCommand = new MyLargeCommand(6000);

        var factory = new AWSClientFactory(GatewayFactory.CreateFactory());
        _client = factory.CreateS3Client();

        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";

        _luggageStore = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), _bucketName)
        {
            HttpClientFactory = httpClientFactory,
            BucketAddressTemplate = CredentialsChain.GetBucketAddressTemplate(),
            ACLs = S3CannedACL.Private,
            Tags = [new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" }],
        });
            
        _luggageStore.EnsureStoreExists();

        var transformerFactoryAsync = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _publication = new Publication { Topic = new RoutingKey("MyLargeCommand"), RequestType = typeof(MyLargeCommand) };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactoryAsync, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);

        //assert
        await Assert.That(message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK)).IsTrue();
        await Assert.That(message.Header.DataRef).IsNotNull();
        _id = (string)message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        await Assert.That(message.Body.Value).IsEqualTo($"Claim Check {_id}");
            
        await Assert.That(await _luggageStore.HasClaimAsync(_id)).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
         //We have to empty objects from a bucket before deleting it
         if (_id != null)
         {
             await _luggageStore.DeleteAsync(_id);
         }

         await _client.DeleteBucketAsync(_bucketName);
    }
}

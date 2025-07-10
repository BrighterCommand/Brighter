using System;
using System.Threading.Tasks;
using Google.Apis.Http;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.Tranformers.Gcp;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Gcp.Tests.Transformers;

public class LargeMessagePayloadWrapTests : IAsyncDisposable 
{
    private string? _id;
    private WrapPipelineAsync<MyLargeCommand>? _transformPipeline;
    private readonly string _bucketName;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private readonly GcsLuggageOptions _luggageStoreOptions;
    private readonly GcsLuggageStore _luggageStore;
    private readonly Publication _publication;

    public LargeMessagePayloadWrapTests()
    {
        //arrange
        TransformPipelineBuilderAsync.ClearPipelineCache();
            
        var mapperRegistry =
            new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(
                _ => new MyLargeCommandMessageMapperAsync())
            );
           
        mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();
            
        _myCommand = new MyLargeCommand(6000);

        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        _luggageStoreOptions = new GcsLuggageOptions
        {
            ProjectId = GatewayFactory.GetProjectId(),
            Credential = GatewayFactory.GetCredential(),
            BucketName = _bucketName
        };
        
        _luggageStore = new GcsLuggageStore(_luggageStoreOptions);
        _luggageStore.EnsureStoreExists();

        var transformerFactoryAsync = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _publication = new Publication { Topic = new RoutingKey("MyLargeCommand"), RequestType = typeof(MyLargeCommand) };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactoryAsync);
    }

    [Fact]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);

        //assert
        Assert.True(message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK));
        Assert.NotNull(message.Header.DataRef);
        _id = (string)message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        Assert.Equal($"Claim Check {_id}", message.Body.Value);
            
        Assert.True(await _luggageStore.HasClaimAsync(_id));
    }

    public async ValueTask DisposeAsync()
    {
         //We have to empty objects from a bucket before deleting it
         if (_id != null)
         {
             await _luggageStore.DeleteAsync(_id);
         }

         var client = await _luggageStoreOptions.CreateStorageClientAsync();
         await client.DeleteBucketAsync(_bucketName);
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims.FileSystem;

public class LargeMessagePayloadAsyncWrapTests : IAsyncDisposable
{
    private string? _id;
    private WrapPipelineAsync<MyLargeCommand>? _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private readonly FileSystemStorageProvider _luggageStore;
    private readonly string _bucketName;
    private readonly Publication _publication;

    public LargeMessagePayloadAsyncWrapTests()
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
        _luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{_bucketName}"));
            
        _luggageStore.EnsureStoreExists();

        var transformerFactoryAsync = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _publication = new Publication { Topic = new RoutingKey("MyLargeCommand"), RequestType = typeof(MyLargeCommand) };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactoryAsync);
    }

    [Fact]
    public async Task When_wrapping_a_large_message_async()
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
            
        Directory.Delete($"./{_bucketName}");
    }
}

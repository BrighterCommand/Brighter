using System;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transformers.MongoGridFS;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.MongoDb.Tests.Transformers;

[Category("MongoDb")]
public class LargeMessagePayloadAsyncWrapTests : IAsyncDisposable 
{
    private string? _id;
    private WrapPipelineAsync<MyLargeCommand>? _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private readonly MongoDbLuggageStore _luggageStore;
    private readonly Publication _publication;

    public LargeMessagePayloadAsyncWrapTests ()
    {
        //arrange

        var mapperRegistry =
            new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(
                _ => new MyLargeCommandMessageMapperAsync())
            );
           
        mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();
            
        _myCommand = new MyLargeCommand(6000);

        string bucketName = $"brightertestbucket-{Guid.NewGuid()}";

        _luggageStore = new MongoDbLuggageStore(new MongoDbLuggageStoreOptions(Configuration.ConnectionString, Configuration.DatabaseName, bucketName));
            
        _luggageStore.EnsureStoreExists();

        var transformerFactoryAsync = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _publication = new Publication { Topic = new RoutingKey("MyLargeCommand"), RequestType = typeof(MyLargeCommand) };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactoryAsync, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_wrapping_a_large_message_async()
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
        if (_id != null)
        {
            await _luggageStore.DeleteAsync(_id);
        }
    }
}

using System;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Paramore.Brighter.Transformers.MongoGridFS;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.MongoDb.Tests.Transformers;

[Category("MongoDb")]
public class LargeMessagePayloadWrapTests : IDisposable
{
    private string? _id;
    private WrapPipeline<MyLargeCommand>? _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private readonly MongoDbLuggageStore _luggageStore;
    private readonly Publication _publication;

    public LargeMessagePayloadWrapTests ()
    {
        //arrange

        var mapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()),
                null
            );
           
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();
            
        _myCommand = new MyLargeCommand(6000);

        string bucketName = $"brightertestbucket-{Guid.NewGuid()}";

        _luggageStore = new MongoDbLuggageStore(new MongoDbLuggageStoreOptions(Configuration.ConnectionString, Configuration.DatabaseName, bucketName));
            
        _luggageStore.EnsureStoreExists();

        var transformerFactoryAsync = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _publication = new Publication { Topic = new RoutingKey("MyLargeCommand"), RequestType = typeof(MyLargeCommand) };

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, transformerFactoryAsync);
    }

    [Test]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = _transformPipeline.Wrap(_myCommand, new RequestContext(), _publication);

        //assert
        await Assert.That(message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK)).IsTrue();
        await Assert.That(message.Header.DataRef).IsNotNull();
        _id = (string)message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        await Assert.That(message.Body.Value).IsEqualTo($"Claim Check {_id}");
            
        await Assert.That(await _luggageStore.HasClaimAsync(_id)).IsTrue();
    }

    public void Dispose()
    {
        //We have to empty objects from a bucket before deleting it
        if (_id != null)
        {
            _luggageStore.Delete(_id);
        }
    }
}

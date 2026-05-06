using System;
using System.IO;
using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Claims.FileSystem;
public class LargeMessagePayloadWrapTests
{
    private string? _id;
    private WrapPipeline<MyLargeCommand>? _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private readonly FileSystemStorageProvider _luggageStore;
    private readonly string _bucketName;
    private readonly Publication _publication;
    public LargeMessagePayloadWrapTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()), null);
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();
        _myCommand = new MyLargeCommand(6000);
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        _luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{_bucketName}"));
        _luggageStore.EnsureStoreExists();
        var transformerFactoryAsync = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));
        _publication = new Publication
        {
            Topic = new RoutingKey("MyLargeCommand"),
            RequestType = typeof(MyLargeCommand)
        };
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

    [After(Test)]
    public void Dispose()
    {
        //We have to empty objects from a bucket before deleting it
        if (_id != null)
        {
            _luggageStore.Delete(_id);
        }

        Directory.Delete($"./{_bucketName}");
    }
}
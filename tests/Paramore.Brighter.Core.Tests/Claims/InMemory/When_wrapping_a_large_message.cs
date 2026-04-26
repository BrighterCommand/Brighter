using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Claims.InMemory;
public class LargeMessagePayloadWrapTests
{
    private WrapPipeline<MyLargeCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private InMemoryStorageProvider _inMemoryStorageProvider;
    private readonly Publication _publication;
    public LargeMessagePayloadWrapTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()), null);
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();
        _publication = new Publication
        {
            Topic = new RoutingKey("transform.event")
        };
        _myCommand = new MyLargeCommand(6000);
        _inMemoryStorageProvider = new InMemoryStorageProvider();
        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_inMemoryStorageProvider, _inMemoryStorageProvider));
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Test]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = _transformPipeline.Wrap(_myCommand, new RequestContext(), _publication);
        //assert
        var id = message.Header.DataRef;
        await Assert.That(string.IsNullOrEmpty(id)).IsFalse();
        await Assert.That(message.Body.Value).IsEqualTo($"Claim Check {id}");
        await Assert.That(await _inMemoryStorageProvider.HasClaimAsync(id)).IsTrue();
    }
}
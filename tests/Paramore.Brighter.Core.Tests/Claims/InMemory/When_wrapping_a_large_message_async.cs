using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Claims.InMemory;
public class AsyncLargeMessagePayloadWrapTests
{
    private WrapPipelineAsync<MyLargeCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private readonly InMemoryStorageProvider _inMemoryStorageProviderAsync;
    private readonly Publication _publication;
    public AsyncLargeMessagePayloadWrapTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyLargeCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();
        _myCommand = new MyLargeCommand(6000);
        _inMemoryStorageProviderAsync = new InMemoryStorageProvider();
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_inMemoryStorageProviderAsync, _inMemoryStorageProviderAsync));
        _publication = new Publication
        {
            Topic = new RoutingKey("MyLargeCommand")
        };
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);
        //assert
        await Assert.That(message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK)).IsTrue();
        await Assert.That(message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK]).IsEqualTo(message.Header.DataRef);
        var id = (string)message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        await Assert.That(message.Body.Value).IsEqualTo($"Claim Check {id}");
        await Assert.That(await _inMemoryStorageProviderAsync.HasClaimAsync(id)).IsTrue();
    }
}
using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class AsyncLargeMessagePayloadWrapTests
{
    private WrapPipelineAsync<MyLargeCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private InMemoryStorageProviderAsync _inMemoryStorageProviderAsync;
    private readonly Publication _publication;

    public AsyncLargeMessagePayloadWrapTests()
    {
        //arrange
        TransformPipelineBuilderAsync.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyLargeCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();

        _myCommand = new MyLargeCommand(6000);

        _inMemoryStorageProviderAsync = new InMemoryStorageProviderAsync();
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync(
            _ => new ClaimCheckTransformer(new InMemoryStorageProvider(), _inMemoryStorageProviderAsync));

        _publication = new Publication { Topic = new RoutingKey("MyLargeCommand") };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
    }

    [Fact]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);

        //assert
        Assert.True(message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK));
        Assert.Equal(message.Header.DataRef, message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK]);
        var id = (string) message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        Assert.Equal($"Claim Check {id}", message.Body.Value);
        Assert.True(await _inMemoryStorageProviderAsync.HasClaimAsync(id));
    }
}

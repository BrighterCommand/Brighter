using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class LargeMessagePayloadWrapTests
{
    private WrapPipelineAsync<MyLargeCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private InMemoryStorageProviderAsync _inMemoryStorageProviderAsync;

    public LargeMessagePayloadWrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();

        _myCommand = new MyLargeCommand(6000);

        _inMemoryStorageProviderAsync = new InMemoryStorageProviderAsync();
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync(
            _ => new ClaimCheckTransformer(_inMemoryStorageProviderAsync));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand);
        
        //assert
        message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK).Should().BeTrue();
        var id = (string) message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        message.Body.Value.Should().Be($"Claim Check {id}");
        (await _inMemoryStorageProviderAsync.HasClaimAsync(id)).Should().BeTrue();

    }
}

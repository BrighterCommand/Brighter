using System;
using System.Threading.Tasks;
using FluentAssertions;
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
            _ => new ClaimCheckTransformerAsync(_inMemoryStorageProviderAsync));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand);
        
        //assert
        message.Header.Bag.ContainsKey(ClaimCheckTransformerAsync.CLAIM_CHECK).Should().BeTrue();
        var id = (string) message.Header.Bag[ClaimCheckTransformerAsync.CLAIM_CHECK];
        message.Body.Value.Should().Be($"Claim Check {id}");
        (await _inMemoryStorageProviderAsync.HasClaimAsync(id)).Should().BeTrue();

    }
}

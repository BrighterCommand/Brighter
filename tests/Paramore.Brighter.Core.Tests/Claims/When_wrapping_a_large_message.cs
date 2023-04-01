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
    private WrapPipeline<MyLargeCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private InMemoryStorageProviderAsync _inMemoryStorageProviderAsync;

    public LargeMessagePayloadWrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()))
        {
            { typeof(MyLargeCommand), typeof(MyLargeCommandMessageMapper) }
        };

        _myCommand = new MyLargeCommand(6000);

        _inMemoryStorageProviderAsync = new InMemoryStorageProviderAsync();
        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_inMemoryStorageProviderAsync));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Fact]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = _transformPipeline.WrapAsync(_myCommand).Result;
        
        //assert
        message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK).Should().BeTrue();
        var id = (string) message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        message.Body.Value.Should().Be($"Claim Check {id}");
        (await _inMemoryStorageProviderAsync.HasClaimAsync(id)).Should().BeTrue();

    }
}

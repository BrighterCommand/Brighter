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
    private InMemoryStorageProvider _inMemoryStorageProvider;
    private readonly Publication _publication;

    public LargeMessagePayloadWrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();
        
        _publication = new Publication{Topic = new RoutingKey("transform.event")};

        _myCommand = new MyLargeCommand(6000);

        _inMemoryStorageProvider = new InMemoryStorageProvider();
        var messageTransformerFactory = new SimpleMessageTransformerFactory(
            _ => new ClaimCheckTransformer(_inMemoryStorageProvider));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public void When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = _transformPipeline.Wrap(_myCommand, _publication);
        
        //assert
        message.Header.Bag.ContainsKey(ClaimCheckTransformerAsync.CLAIM_CHECK).Should().BeTrue();
        var id = (string) message.Header.Bag[ClaimCheckTransformerAsync.CLAIM_CHECK];
        message.Body.Value.Should().Be($"Claim Check {id}");
        _inMemoryStorageProvider.HasClaim(id).Should().BeTrue();

    }
}

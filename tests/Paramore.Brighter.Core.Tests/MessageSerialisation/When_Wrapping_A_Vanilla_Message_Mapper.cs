using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class VanillaMessageWrapRequestTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;

    public VanillaMessageWrapRequestTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyVanillaCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyTransformableCommand, MyVanillaCommandMessageMapper>();

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));

        _publication = new Publication { Topic = new RoutingKey("MyTransformableCommand") };

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory, new InMemoryRequestContextFactory());
    }
    
    [Fact]
    public void When_Wrapping_A_Vanilla_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = _transformPipeline.Wrap(_myCommand, _publication);
        
        //assert
        message.Body.Value.Should().Be(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString());
    }
}

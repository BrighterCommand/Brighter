using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class AsyncVanillaMessageWrapRequestTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;

    public AsyncVanillaMessageWrapRequestTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyVanillaCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyVanillaCommandMessageMapperAsync>();

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => null));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public async Task When_Wrapping_A_Vanilla_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand);
        
        //assert
        message.Body.Value.Should().Be(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString());
    }
}

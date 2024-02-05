using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class MessageWrapRequestTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;

    public MessageWrapRequestTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()),
             null);
         mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransform()));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public void When_Wrapping_A_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = _transformPipeline.Wrap(_myCommand);
        
        //assert
        message.Body.Value.Should().Be(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString());
        message.Header.Bag[MySimpleTransformAsync.HEADER_KEY].Should().Be(MySimpleTransformAsync.TRANSFORM_VALUE);
    }
}

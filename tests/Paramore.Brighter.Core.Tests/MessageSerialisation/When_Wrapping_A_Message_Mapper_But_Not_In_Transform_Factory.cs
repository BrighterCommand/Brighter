using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class MessageWrapRequestMissingTransformTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;

    public MessageWrapRequestMissingTransformTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public void When_Wrapping_A_Message_Mapper_But_Not_In_Transform_Factory()
    {
        //act
        var exception = Catch.Exception(() => _pipelineBuilder.BuildWrapPipeline(_myCommand));
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConfigurationException>();
        
    }
}

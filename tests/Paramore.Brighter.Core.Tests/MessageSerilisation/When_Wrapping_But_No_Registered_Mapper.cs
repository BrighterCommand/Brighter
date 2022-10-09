using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerilisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerilisation;

public class MessageWrapRequestMissingMapperTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly MessageTransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;

    public MessageWrapRequestMissingMapperTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new MessageTransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public void When_Wrapping_But_No_Registered_Mapper()
    {
        //act
        var exception = Catch.Exception(() => _transformPipeline = _pipelineBuilder.BuildWrapPipeline(_myCommand));
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConfigurationException>();
    }
}

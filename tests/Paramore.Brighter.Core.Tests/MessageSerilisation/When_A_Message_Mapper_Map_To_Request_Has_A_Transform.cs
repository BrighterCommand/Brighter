using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerilisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerilisation;

public class MessageWrapPathPipelineTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly MessageTransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;

    public MessageWrapPathPipelineTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new MessageTransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
        
    }
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Request_Has_A_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline(_myCommand);
        
        //assert
        TraceFilters().ToString().Should().Be("MyTransformableCommandMessageMapper|MySimpleTransformAsync");
    }

    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

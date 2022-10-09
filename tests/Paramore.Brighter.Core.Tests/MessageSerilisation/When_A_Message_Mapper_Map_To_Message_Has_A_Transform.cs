using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerilisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerilisation;

public class MessageUnwrapPathPipelineTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;

    public MessageUnwrapPathPipelineTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
        
    }
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Message_Has_A_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline(_myCommand);
        
        //assert
        TraceFilters().ToString().Should().Be("MySimpleTransformAsync|MyTransformableCommandMessageMapper");
    }
    
    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

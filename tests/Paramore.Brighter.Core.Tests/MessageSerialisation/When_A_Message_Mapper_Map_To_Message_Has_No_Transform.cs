using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageUnwrapPathNoTransformPipelineTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;

    public MessageUnwrapPathNoTransformPipelineTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyVanillaCommandMessageMapper()))
            { { typeof(MyTransformableCommand), typeof(MyVanillaCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
        
    }
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Message_Has_No_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline(_myCommand);
        
        //assert
        TraceFilters().ToString().Should().Be("MyVanillaCommandMessageMapper");
    }
    
    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

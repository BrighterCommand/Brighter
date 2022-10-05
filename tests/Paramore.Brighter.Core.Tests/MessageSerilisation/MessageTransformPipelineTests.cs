using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerilisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerilisation;

public class MessageTransformPipelineTests
{
    private WrapPipeline _transformPipeline;
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Request_Has_A_Transform()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyTransformableCommandMessageMapper()))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        var pipelineBuilder = new MessageTransformPipelineBuilder(mapperRegistry);

        //act
        _transformPipeline = pipelineBuilder.BuildWrapPipeline();
        
        //assert
        TraceFilters().ToString().Should().Be("MySimpleTransform | MyTransformableCommandMessageMapper");
    }
    
    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

public class TransformPipelineTracer
{
    public override string ToString()
    {
        return string.Empty;
    }

    public void AddTransform(string empty)
    {
    }
}

public class WrapPipeline
{
    public void DescribePath(TransformPipelineTracer pipelineTracer)
    {
        pipelineTracer.AddTransform("");
    }
}

public class MessageTransformPipelineBuilder
{
    private readonly MessageMapperRegistry _mapperRegistry;

    public MessageTransformPipelineBuilder(MessageMapperRegistry mapperRegistry)
    {
        _mapperRegistry = mapperRegistry;
    }

    public WrapPipeline BuildWrapPipeline()
    {
        return new WrapPipeline();
    }
}

﻿using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class AsyncMessageUnwrapPathPipelineTests
{
    private UnwrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;

    public AsyncMessageUnwrapPathPipelineTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();

        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
        
    }
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Message_Has_A_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>();
        
        //assert
        TraceFilters().ToString().Should().Be("MySimpleTransformAsync|MyTransformableCommandMessageMapperAsync");
    }
    
    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

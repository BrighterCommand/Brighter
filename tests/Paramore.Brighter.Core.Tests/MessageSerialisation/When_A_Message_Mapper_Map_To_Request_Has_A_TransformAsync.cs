﻿using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class AsyncMessageWrapPathPipelineTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;

    public AsyncMessageWrapPathPipelineTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             null,
             new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync())
         );
         mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();

        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
        
    }
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Request_Has_A_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        
        //assert
        TraceFilters().ToString().Should().Be("MyTransformableCommandMessageMapperAsync|MySimpleTransformAsync");
    }

    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

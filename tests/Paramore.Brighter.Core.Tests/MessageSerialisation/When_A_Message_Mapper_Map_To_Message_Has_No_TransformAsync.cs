﻿using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.MessageMappers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class AsyncMessageUnwrapPathNoTransformPipelineTests
{
    private UnwrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;

    public AsyncMessageUnwrapPathNoTransformPipelineTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             null,
             new SimpleMessageMapperFactoryAsync(_ => new JsonMessageMapperAsync<MyTransformableCommand>()));
         mapperRegistry.RegisterAsync<MyTransformableCommand, JsonMessageMapperAsync<MyTransformableCommand>>();

        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => null));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
        
    }
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Message_Has_No_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>();
        
        //assert
        TraceFilters().ToString().Should().Be(typeof(JsonMessageMapperAsync<MyTransformableCommand>).Name);
    }
    
    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

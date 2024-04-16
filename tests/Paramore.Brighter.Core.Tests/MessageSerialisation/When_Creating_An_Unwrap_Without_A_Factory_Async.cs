﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class AsyncTransformPipelineMissingFactoryUnwrapTests
{
    private UnwrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Message _message;

    public AsyncTransformPipelineMissingFactoryUnwrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();

        _myCommand = new MyTransformableCommand();
        
        _message = new Message(
            new MessageHeader(_myCommand.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, null);
    }
    
    [Fact]
    public async Task When_Creating_An_Unwrap_Without_A_Factory()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>();
        
        // If no factory we default to just them mapper
        TraceFilters().ToString().Should().Be("MyTransformableCommandMessageMapperAsync");

        //wrap should just do message mapper                                          
        var request = await _transformPipeline.UnwrapAsync(_message);
        
        //assert
        request.Value = _myCommand.Value;
    }
    
    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
    
}

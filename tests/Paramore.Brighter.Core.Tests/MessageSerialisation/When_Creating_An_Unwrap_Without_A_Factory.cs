using System;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class TransformPipelineMissingFactoryUnwrapTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Message _message;

    public TransformPipelineMissingFactoryUnwrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();
        
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();
        
        _message = new Message(
            new MessageHeader(_myCommand.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, null);
    }
    
    [Fact]
    public void When_Creating_An_Unwrap_Without_A_Factory()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline(_myCommand);
        
        // If no factory we default to just them mapper
        TraceFilters().ToString().Should().Be("MyTransformableCommandMessageMapper");

        //wrap should just do message mapper                                          
        var request = _transformPipeline.Unwrap(_message).Result;
        
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

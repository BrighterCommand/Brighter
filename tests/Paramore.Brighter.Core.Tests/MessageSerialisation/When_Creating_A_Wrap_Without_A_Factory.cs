using System.Text.Json;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class TransformPipelineMissingFactoryWrapTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;

    public TransformPipelineMissingFactoryWrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();

        _myCommand = new MyTransformableCommand();

        _publication = new Publication { Topic = new RoutingKey("MyTransformableCommand") };

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, null);
    }
    
    [Fact]
    public void When_Creating_A_Wrap_Without_A_Factory()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        
        // If no factory we default to just them mapper
        Assert.Equal("MyTransformableCommandMessageMapper", TraceFilters().ToString());

        //wrap should just do message mapper                                          
        var message = _transformPipeline.Wrap(_myCommand,new RequestContext(), _publication);
        
        //assert
        Assert.Equal(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString(), message.Body.Value);
        
        //we won't run a transform
        Assert.Equal(false, message.Header.Bag.ContainsKey(MySimpleTransformAsync.HEADER_KEY));
    }
    
    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
    
}

using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class MessageUnwrapPathNoTransformPipelineTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;

    public MessageUnwrapPathNoTransformPipelineTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             new SimpleMessageMapperFactory(_ => new MyVanillaCommandMessageMapper()),
             null);
         mapperRegistry.Register<MyTransformableCommand, MyVanillaCommandMessageMapper>();

        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
        
    }
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Message_Has_No_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>();
        
        //assert
        Assert.Equal("MyVanillaCommandMessageMapper", TraceFilters().ToString()); 
    }
    
    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

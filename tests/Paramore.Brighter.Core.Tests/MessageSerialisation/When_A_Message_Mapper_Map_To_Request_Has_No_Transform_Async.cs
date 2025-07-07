using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class AsyncMessageWrapPathPipelineNoTransformTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;

    public AsyncMessageWrapPathPipelineNoTransformTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             null,
             new SimpleMessageMapperFactoryAsync(_ => new MyVanillaCommandMessageMapperAsync()));
         mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();

        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => null));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
        
    }
    
    [Fact]
    public void When_A_Message_Mapper_Map_To_Request_Has_No_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        
        //assert
        Assert.Equal("MyVanillaCommandMessageMapperAsync", TraceFilters().ToString());
    }

    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}

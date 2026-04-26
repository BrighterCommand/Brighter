using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class AsyncMessageWrapPathPipelineNoTransformTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    public AsyncMessageWrapPathPipelineNoTransformTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyVanillaCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => null));
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_A_Message_Mapper_Map_To_Request_Has_No_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        //assert
        await Assert.That(TraceFilters().ToString()).IsEqualTo("MyVanillaCommandMessageMapperAsync");
    }

    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}
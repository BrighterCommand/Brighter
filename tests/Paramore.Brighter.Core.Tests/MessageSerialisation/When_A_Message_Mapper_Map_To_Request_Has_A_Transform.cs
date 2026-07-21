using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class MessageWrapPathPipelineTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    public MessageWrapPathPipelineTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()), null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransform()));
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Test]
    public async Task When_A_Message_Mapper_Map_To_Request_Has_A_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        //assert
        await Assert.That(TraceFilters().ToString()).IsEqualTo("MyTransformableCommandMessageMapper|MySimpleTransform");
    }

    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}
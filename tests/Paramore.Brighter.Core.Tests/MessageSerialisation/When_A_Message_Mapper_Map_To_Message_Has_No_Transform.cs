using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class MessageUnwrapPathNoTransformPipelineTests
{
    private UnwrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    public MessageUnwrapPathNoTransformPipelineTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyVanillaCommandMessageMapper()), null);
        mapperRegistry.Register<MyTransformableCommand, MyVanillaCommandMessageMapper>();
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Test]
    public async Task When_A_Message_Mapper_Map_To_Message_Has_No_Transform()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyTransformableCommand>();
        //assert
        await Assert.That(TraceFilters().ToString()).IsEqualTo("MyVanillaCommandMessageMapper");
    }

    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}
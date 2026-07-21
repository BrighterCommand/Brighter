using System.Text.Json;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class TransformPipelineMissingFactoryWrapTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;
    public TransformPipelineMissingFactoryWrapTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()), null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();
        _myCommand = new MyTransformableCommand();
        _publication = new Publication
        {
            Topic = new RoutingKey("MyTransformableCommand")
        };
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, null);
    }

    [Test]
    public async Task When_Creating_A_Wrap_Without_A_Factory()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        // If no factory we default to just them mapper
        await Assert.That(TraceFilters().ToString()).IsEqualTo("MyTransformableCommandMessageMapper");
        //wrap should just do message mapper                                          
        var message = _transformPipeline.Wrap(_myCommand, new RequestContext(), _publication);
        //assert
        await Assert.That(message.Body.Value).IsEqualTo(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString());
        //we won't run a transform
        await Assert.That(message.Header.Bag.ContainsKey(MySimpleTransformAsync.HEADER_KEY)).IsEqualTo(false);
    }

    private TransformPipelineTracer TraceFilters()
    {
        var pipelineTracer = new TransformPipelineTracer();
        _transformPipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
}
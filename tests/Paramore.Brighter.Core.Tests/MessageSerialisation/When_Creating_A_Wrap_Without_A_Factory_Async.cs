using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class AsyncTransformPipelineMissingFactoryWrapTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;
    public AsyncTransformPipelineMissingFactoryWrapTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();
        _myCommand = new MyTransformableCommand();
        _publication = new Publication
        {
            Topic = new RoutingKey("MyTransformableCommand"),
            RequestType = typeof(MyTransformableCommand)
        };
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, null, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_Creating_A_Wrap_Without_A_Factory()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        // If no factory we default to just them mapper
        await Assert.That(TraceFilters().ToString()).IsEqualTo("MyTransformableCommandMessageMapperAsync");
        //wrap should just do message mapper                                          
        var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);
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
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class AsyncVanillaMessageWrapRequestTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;
    public AsyncVanillaMessageWrapRequestTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyVanillaCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyVanillaCommandMessageMapperAsync>();
        _myCommand = new MyTransformableCommand();
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => null));
        _publication = new Publication
        {
            Topic = new RoutingKey("MyTransformableCommand"),
            RequestType = typeof(MyTransformableCommand)
        };
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_Wrapping_A_Vanilla_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);
        //assert
        await Assert.That(message.Body.Value).IsEqualTo(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString());
    }
}
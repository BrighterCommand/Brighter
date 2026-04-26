using System.Text.Json;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class VanillaMessageWrapRequestTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;
    public VanillaMessageWrapRequestTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyVanillaCommandMessageMapper()), null);
        mapperRegistry.Register<MyTransformableCommand, MyVanillaCommandMessageMapper>();
        _myCommand = new MyTransformableCommand();
        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => null));
        _publication = new Publication
        {
            Topic = new RoutingKey("MyTransformableCommand")
        };
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Test]
    public async Task When_Wrapping_A_Vanilla_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = _transformPipeline.Wrap(_myCommand, new RequestContext(), _publication);
        //assert
        await Assert.That(message.Body.Value).IsEqualTo(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString());
    }
}
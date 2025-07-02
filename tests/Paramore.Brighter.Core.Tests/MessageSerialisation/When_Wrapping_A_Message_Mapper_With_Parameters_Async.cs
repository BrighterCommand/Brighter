using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class AsyncMessageWrapRequestWithAttributesTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;

    public AsyncMessageWrapRequestWithAttributesTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             null,
             new SimpleMessageMapperFactoryAsync(_ => new MyParameterizedTransformMessageMapperAsync())
             );
         mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => new MyParameterizedTransformAsync()));

        _publication = new Publication
        {
            Topic = new RoutingKey("MyTransformableCommand"), RequestType = typeof(MyTransformableCommand)
        };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
    }
    
    [Fact]
    public async Task When_Wrapping_A_Message_Mapper_With_Attributes()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);
        
        //assert
        Assert.Equal(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString(), message.Body.Value);
        Assert.Equal("I am a format indicator {0}", message.Header.Bag[MyParameterizedTransformAsync.HEADER_KEY]);
    }
}

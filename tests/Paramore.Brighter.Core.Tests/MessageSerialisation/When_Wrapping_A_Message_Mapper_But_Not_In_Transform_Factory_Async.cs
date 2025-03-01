using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class AsyncMessageWrapRequestMissingTransformTests
{
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;

    public AsyncMessageWrapRequestMissingTransformTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync()));
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();

        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => null));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public void When_Wrapping_A_Message_Mapper_But_Not_In_Transform_Factory()
    {
        //act
        var exception = Catch.Exception(() => _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());
        Assert.NotNull(exception);
        Assert.True((exception) is ConfigurationException);
        
    }
}

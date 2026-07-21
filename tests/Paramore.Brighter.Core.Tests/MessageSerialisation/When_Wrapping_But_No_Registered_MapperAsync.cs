using System;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class AsyncMessageWrapRequestMissingMapperTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    public AsyncMessageWrapRequestMissingMapperTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null), null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => new MySimpleTransformAsync()));
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_Wrapping_But_No_Registered_Mapper()
    {
        //act
        var exception = Catch.Exception(() => _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());
        await Assert.That(exception).IsNotNull();
        await Assert.That((exception) is ConfigurationException).IsTrue();
        await Assert.That((exception.InnerException) is InvalidOperationException).IsTrue();
    }
}
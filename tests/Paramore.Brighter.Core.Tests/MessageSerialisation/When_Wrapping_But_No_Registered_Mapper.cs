using System;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class MessageWrapRequestMissingMapperTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;

    public MessageWrapRequestMissingMapperTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Fact]
    public void When_Wrapping_But_No_Registered_Mapper()
    {
        //act
        var exception = Catch.Exception(() => _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConfigurationException>();
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
    }
}

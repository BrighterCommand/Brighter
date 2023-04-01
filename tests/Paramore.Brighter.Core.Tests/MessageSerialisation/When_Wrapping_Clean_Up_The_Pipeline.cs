using System;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class MessageWrapCleanupTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    public static string s_released;

    public MessageWrapCleanupTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()))
            { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

        _myCommand = new MyTransformableCommand();

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, new MyReleaseTrackingTransformFactory());
    }

    [Fact]
    public void When_Wrapping_Clean_Up_The_Pipeline()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = _transformPipeline.WrapAsync(_myCommand).Result;
        _transformPipeline.Dispose();

        //assert
        s_released.Should().Be("|MySimpleTransformAsync");

    }


    private class MyReleaseTrackingTransformFactory : IAmAMessageTransformerFactory
    {
        public IAmAMessageTransformAsync Create(Type transformerType)
        {
            return new MySimpleTransformAsync();
        }

        public void Release(IAmAMessageTransformAsync transformer)
        {
            var disposable = transformer as IDisposable;
            disposable?.Dispose();

            s_released += "|" + transformer.GetType().Name;
        }
    }

}

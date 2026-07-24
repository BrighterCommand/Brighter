using System;
using System.Collections.Generic;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformPipelinePartialWrapBuildReleaseTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly RecordingTransformerFactory _transformerFactory;

    public TransformPipelinePartialWrapBuildReleaseTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyDoubleWrapTransformMessageMapper()),
            null);
        mapperRegistry.Register<MyTransformableCommand, MyDoubleWrapTransformMessageMapper>();

        _transformerFactory = new RecordingTransformerFactory();
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, _transformerFactory);
    }

    [Fact]
    public void When_A_Later_Wrap_Transform_Cannot_Be_Created_Earlier_Transforms_Are_Released()
    {
        //act
        //the mapper declares two wrap transforms; the factory builds the first but cannot build the second,
        //so the build throws part-way through the pipeline
        var exception = Catch.Exception(() => _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert
        Assert.IsType<ConfigurationException>(exception);
        //the first transform was created before the second failed; it must be released back to the factory,
        //not leaked, because no pipeline was ever constructed to own it
        Assert.Single(_transformerFactory.Created);
        Assert.Equal(_transformerFactory.Created, _transformerFactory.Released);
    }

    // a mapper whose MapToMessage declares two wrap transforms of different types (built in step order)
    private sealed class MyDoubleWrapTransformMessageMapper : IAmAMessageMapper<MyTransformableCommand>
    {
        public IRequestContext? Context { get; set; }

        [MySimpleWrapWith(2)]                        // higher step: built first, factory succeeds
        [MyParameterizedWrapWith(1, "unused")]       // lower step: built second, factory returns null -> throws
        public Message MapToMessage(MyTransformableCommand request, Publication publication)
            => new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

        public MyTransformableCommand MapToRequest(Message message) => new();
    }

    private sealed class RecordingTransformerFactory : IAmAMessageTransformerFactory
    {
        public List<IAmAMessageTransform> Created { get; } = new();
        public List<IAmAMessageTransform> Released { get; } = new();

        public IAmAMessageTransform? Create(Type transformerType)
        {
            //the factory can satisfy the first transform but not the second
            if (transformerType == typeof(MyParameterizedTransformAsync))
                return null;

            var transform = new MySimpleTransform();
            Created.Add(transform);
            return transform;
        }

        public void Release(IAmAMessageTransform transformer) => Released.Add(transformer);
    }
}

using System;
using System.Collections.Generic;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformPipelinePostConstructionFailureReleaseTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly RecordingTransformerFactory _transformerFactory;

    public TransformPipelinePostConstructionFailureReleaseTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyExplicitUnwrapMessageMapper()),
            null);
        mapperRegistry.Register<MyTransformableCommand, MyExplicitUnwrapMessageMapper>();

        _transformerFactory = new RecordingTransformerFactory();
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, _transformerFactory);
    }

    [Fact]
    public void When_Building_A_Wrap_Pipeline_Fails_After_Construction_Transforms_Are_Released()
    {
        //act
        //the wrap pipeline (and its transform) is constructed successfully, then discovering the unwrap
        //transforms throws because MapToRequest is not discoverable; the built transform must not leak
        var exception = Catch.Exception(() => _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert
        Assert.IsType<ConfigurationException>(exception);
        Assert.Single(_transformerFactory.Created);
        //the transform owned by the discarded pipeline must be released deterministically, not left to a finalizer
        Assert.Equal(_transformerFactory.Created, _transformerFactory.Released);
    }

    // a mapper whose MapToMessage is discoverable (so a wrap transform is built) but whose MapToRequest is
    // an explicit interface implementation, so MapperMethodDiscovery cannot find it and unwrap discovery
    // throws AFTER the wrap pipeline has been constructed
    private sealed class MyExplicitUnwrapMessageMapper : IAmAMessageMapper<MyTransformableCommand>
    {
        public IRequestContext? Context { get; set; }

        [MySimpleWrapWith(0)]
        public Message MapToMessage(MyTransformableCommand request, Publication publication)
            => new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

        MyTransformableCommand IAmAMessageMapper<MyTransformableCommand>.MapToRequest(Message message) => new();
    }

    private sealed class RecordingTransformerFactory : IAmAMessageTransformerFactory
    {
        public List<IAmAMessageTransform> Created { get; } = new();
        public List<IAmAMessageTransform> Released { get; } = new();

        public IAmAMessageTransform? Create(Type transformerType)
        {
            var transform = new MySimpleTransform();
            Created.Add(transform);
            return transform;
        }

        public void Release(IAmAMessageTransform transformer) => Released.Add(transformer);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class AsyncTransformPipelinePostConstructionFailureReleaseTests
{
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly RecordingTransformerFactoryAsync _transformerFactory;

    public AsyncTransformPipelinePostConstructionFailureReleaseTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyExplicitUnwrapMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyExplicitUnwrapMessageMapperAsync>();

        _transformerFactory = new RecordingTransformerFactoryAsync();
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, _transformerFactory, InstrumentationOptions.All);
    }

    [Fact]
    public void When_Building_A_Wrap_Pipeline_Fails_After_Construction_Transforms_Are_Released_Async()
    {
        //act
        //the wrap pipeline (and its transform) is constructed successfully, then discovering the unwrap
        //transforms throws because MapToRequestAsync is not discoverable; the built transform must not leak
        var exception = Catch.Exception(() => _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert
        Assert.IsType<ConfigurationException>(exception);
        Assert.Single(_transformerFactory.Created);
        //the transform owned by the discarded pipeline must be released deterministically, not left to a finalizer
        Assert.Equal(_transformerFactory.Created, _transformerFactory.Released);
    }

    // a mapper whose MapToMessageAsync is discoverable (so a wrap transform is built) but whose MapToRequestAsync
    // is an explicit interface implementation, so MapperMethodDiscovery cannot find it and unwrap discovery
    // throws AFTER the wrap pipeline has been constructed
    private sealed class MyExplicitUnwrapMessageMapperAsync : IAmAMessageMapperAsync<MyTransformableCommand>
    {
        public IRequestContext? Context { get; set; }

        [MySimpleWrapWith(0)]
        public Task<Message> MapToMessageAsync(MyTransformableCommand request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND),
                new MessageBody("test")));

        Task<MyTransformableCommand> IAmAMessageMapperAsync<MyTransformableCommand>.MapToRequestAsync(Message message, CancellationToken cancellationToken)
            => Task.FromResult(new MyTransformableCommand());
    }

    private sealed class RecordingTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public List<IAmAMessageTransformAsync> Created { get; } = new();
        public List<IAmAMessageTransformAsync> Released { get; } = new();

        public IAmAMessageTransformAsync? Create(Type transformerType)
        {
            var transform = new MySimpleTransformAsync();
            Created.Add(transform);
            return transform;
        }

        public void Release(IAmAMessageTransformAsync transformer) => Released.Add(transformer);
    }
}

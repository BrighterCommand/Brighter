using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class AsyncTransformPipelinePartialWrapBuildReleaseTests
{
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly RecordingTransformerFactoryAsync _transformerFactory;

    public AsyncTransformPipelinePartialWrapBuildReleaseTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyDoubleWrapTransformMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyDoubleWrapTransformMessageMapperAsync>();

        _transformerFactory = new RecordingTransformerFactoryAsync();
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, _transformerFactory, InstrumentationOptions.All);
    }

    [Fact]
    public void When_A_Later_Wrap_Transform_Cannot_Be_Created_Earlier_Transforms_Are_Released_Async()
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

    // a mapper whose MapToMessageAsync declares two wrap transforms of different types (built in step order)
    private sealed class MyDoubleWrapTransformMessageMapperAsync : IAmAMessageMapperAsync<MyTransformableCommand>
    {
        public IRequestContext? Context { get; set; }

        [MySimpleWrapWith(2)]                        // higher step: built first, factory succeeds
        [MyParameterizedWrapWith(1, "unused")]       // lower step: built second, factory returns null -> throws
        public Task<Message> MapToMessageAsync(MyTransformableCommand request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND),
                new MessageBody("test")));

        public Task<MyTransformableCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
            => Task.FromResult(new MyTransformableCommand());
    }

    private sealed class RecordingTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public List<IAmAMessageTransformAsync> Created { get; } = new();
        public List<IAmAMessageTransformAsync> Released { get; } = new();

        public IAmAMessageTransformAsync? Create(Type transformerType)
        {
            //the factory can satisfy the first transform but not the second
            if (transformerType == typeof(MyParameterizedTransformAsync))
                return null;

            var transform = new MySimpleTransformAsync();
            Created.Add(transform);
            return transform;
        }

        public void Release(IAmAMessageTransformAsync transformer) => Released.Add(transformer);
    }
}

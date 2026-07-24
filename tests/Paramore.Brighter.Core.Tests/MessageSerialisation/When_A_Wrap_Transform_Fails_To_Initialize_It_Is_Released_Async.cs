using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class AsyncTransformerFactoryInitializeFailureReleaseTests
{
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly RecordingTransformerFactoryAsync _transformerFactory;

    public AsyncTransformerFactoryInitializeFailureReleaseTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();

        _transformerFactory = new RecordingTransformerFactoryAsync();
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, _transformerFactory, InstrumentationOptions.All);
    }

    [Fact]
    public void When_A_Wrap_Transform_Fails_To_Initialize_It_Is_Released_Async()
    {
        //act
        //the factory creates the transform, but initialising it from the attribute params throws;
        //the transform exists but was never returned to the builder, so only the factory can release it
        var exception = Catch.Exception(() => _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert
        Assert.IsType<ConfigurationException>(exception);
        Assert.Single(_transformerFactory.Created);
        //the created-but-uninitialised transform must be released back to the factory, not leaked
        Assert.Equal(_transformerFactory.Created, _transformerFactory.Released);
    }

    // a transform that is created successfully but throws while being initialised from its attribute params
    private sealed class MyInitializeThrowsTransformAsync : IAmAMessageTransformAsync
    {
        public IRequestContext? Context { get; set; }

        public void Dispose() { }

        public void InitializeWrapFromAttributeParams(params object?[] initializerList)
            => throw new InvalidOperationException("transform cannot be initialised");

        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
            => throw new InvalidOperationException("transform cannot be initialised");

        public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
            => Task.FromResult(message);

        public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
            => Task.FromResult(message);
    }

    private sealed class RecordingTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public List<IAmAMessageTransformAsync> Created { get; } = new();
        public List<IAmAMessageTransformAsync> Released { get; } = new();

        public IAmAMessageTransformAsync? Create(Type transformerType)
        {
            var transform = new MyInitializeThrowsTransformAsync();
            Created.Add(transform);
            return transform;
        }

        public void Release(IAmAMessageTransformAsync transformer) => Released.Add(transformer);
    }
}

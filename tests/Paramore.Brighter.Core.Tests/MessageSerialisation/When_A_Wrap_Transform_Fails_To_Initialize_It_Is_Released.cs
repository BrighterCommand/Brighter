using System;
using System.Collections.Generic;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;


namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformerFactoryInitializeFailureReleaseTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly RecordingTransformerFactory _transformerFactory;

    public TransformerFactoryInitializeFailureReleaseTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();

        _transformerFactory = new RecordingTransformerFactory();
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, _transformerFactory);
    }

    [Test]
    public async System.Threading.Tasks.Task When_A_Wrap_Transform_Fails_To_Initialize_It_Is_Released()
    {
        //act
        //the factory creates the transform, but initialising it from the attribute params throws;
        //the transform exists but was never returned to the builder, so only the factory can release it
        var exception = Catch.Exception(() => _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert
        await Assert.That(exception).IsTypeOf<ConfigurationException>();
        await Assert.That(_transformerFactory.Created).HasSingleItem();
        //the created-but-uninitialised transform must be released back to the factory, not leaked
        await Assert.That(_transformerFactory.Released).IsEquivalentTo(_transformerFactory.Created, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    // a transform that is created successfully but throws while being initialised from its attribute params
    private sealed class MyInitializeThrowsTransform : IAmAMessageTransform
    {
        public IRequestContext? Context { get; set; }

        public void Dispose() { }

        public void InitializeWrapFromAttributeParams(params object?[] initializerList)
            => throw new InvalidOperationException("transform cannot be initialised");

        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
            => throw new InvalidOperationException("transform cannot be initialised");

        public Message Wrap(Message message, Publication publication) => message;

        public Message Unwrap(Message message) => message;
    }

    private sealed class RecordingTransformerFactory : IAmAMessageTransformerFactory
    {
        public List<IAmAMessageTransform> Created { get; } = new();
        public List<IAmAMessageTransform> Released { get; } = new();

        public IAmAScope? CreatePipelineScope() => null;

        public Lease<IAmAMessageTransform>? Create(Type transformerType, IAmAScope? scope = null)
        {
            var transform = new MyInitializeThrowsTransform();
            Created.Add(transform);
            return new Lease<IAmAMessageTransform>(transform);
        }

        public void Release(Lease<IAmAMessageTransform>? lease) => Released.Add(lease!.Instance);
    }
}

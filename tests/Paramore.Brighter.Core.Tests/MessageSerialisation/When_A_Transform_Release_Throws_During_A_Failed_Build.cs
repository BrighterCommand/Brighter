#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// A failed pipeline build releases the mapper and transforms it created directly, because no pipeline was
// returned to the caller to own them. This PR lets Release/Dispose surface exceptions, so that cleanup can
// now throw. These tests pin two consequences: the cleanup must still release every transform (a throw on one
// must not skip the rest — there is no finalizer to retry a partial build), and a cleanup failure must not
// replace the original configuration error the caller needs to see.
public class TransformPipelineFailedBuildReleaseThrowTests
{
    [Fact]
    public void When_a_transform_release_throws_during_a_partial_build_the_others_are_released_and_the_error_is_not_masked()
    {
        //arrange — a mapper declaring three wrap transforms; the factory builds the first two but cannot
        //build the third, so the build fails part-way and the two already-built transforms must be released.
        //Releasing the first one throws (MS DI's synchronous scope Dispose throws for an IAsyncDisposable-only
        //transform); that must neither skip the second transform's release nor become the exception the caller sees.
        TransformPipelineBuilder.ClearPipelineCache();

        var transformerFactory = new ThrowingReleaseTransformerFactory();
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyThreeWrapTransformMessageMapper()),
            null);
        mapperRegistry.Register<MyTransformableCommand, MyThreeWrapTransformMessageMapper>();
        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, transformerFactory);

        //act
        var exception = Catch.Exception(() => pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert — two transforms were built before the third failed to build
        Assert.Equal(2, transformerFactory.Created.Count);
        //the second (non-throwing) transform is released even though releasing the first threw: the failed-build
        //cleanup drains every transform rather than stopping at the first throw. There is no pipeline and no
        //finalizer here, so a skipped release would leak that transform's DI scope permanently.
        Assert.Contains(transformerFactory.Created[1], transformerFactory.Released);
        //the caller still sees the real build error (the transform that could not be created), not the release
        //failure: cleanup must not mask the configuration error the user needs to fix
        var configException = Assert.IsType<ConfigurationException>(exception);
        var inner = Assert.IsType<ConfigurationException>(configException.InnerException);
        Assert.Contains("Could not create transformer", inner.Message);
    }

    [Fact]
    public void When_cleanup_of_a_failed_build_throws_the_original_error_is_not_masked()
    {
        //arrange — the wrap pipeline (and its one transform) is constructed, then unwrap discovery throws
        //because MapToRequest is an explicit interface implementation. Cleanup disposes the constructed
        //pipeline, whose transform release throws; that disposal failure must not replace the build error.
        TransformPipelineBuilder.ClearPipelineCache();

        var transformerFactory = new ThrowingReleaseTransformerFactory();
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyExplicitUnwrapWrapMessageMapper()),
            null);
        mapperRegistry.Register<MyTransformableCommand, MyExplicitUnwrapWrapMessageMapper>();
        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, transformerFactory);

        //act
        var exception = Catch.Exception(() => pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert — the caller sees the wrap-build ConfigurationException whose inner is the unwrap-discovery
        //failure, not the release InvalidOperationException raised while disposing the discarded pipeline
        var configException = Assert.IsType<ConfigurationException>(exception);
        var inner = Assert.IsType<ConfigurationException>(configException.InnerException);
        Assert.Contains("No MapToRequest", inner.Message);
    }

    // a mapper declaring three wrap transforms, built in descending step order; the third cannot be built
    private sealed class MyThreeWrapTransformMessageMapper : IAmAMessageMapper<MyTransformableCommand>
    {
        public IRequestContext? Context { get; set; }

        [FirstWrapWith(3)]      // built first, factory succeeds; its release throws
        [SecondWrapWith(2)]     // built second, factory succeeds; must still be released
        [FailingWrapWith(1)]    // built third, factory returns null -> the build fails here
        public Message MapToMessage(MyTransformableCommand request, Publication publication)
            => new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

        public MyTransformableCommand MapToRequest(Message message) => new();
    }

    // a mapper whose MapToMessage is discoverable (so a wrap transform is built and the pipeline constructed)
    // but whose MapToRequest is an explicit implementation, so unwrap discovery throws after construction
    private sealed class MyExplicitUnwrapWrapMessageMapper : IAmAMessageMapper<MyTransformableCommand>
    {
        public IRequestContext? Context { get; set; }

        [FirstWrapWith(1)]
        public Message MapToMessage(MyTransformableCommand request, Publication publication)
            => new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

        MyTransformableCommand IAmAMessageMapper<MyTransformableCommand>.MapToRequest(Message message) => new();
    }

    private sealed class FirstWrapWith(int step) : WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(RecordingTransform);
    }

    private sealed class SecondWrapWith(int step) : WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(RecordingTransform);
    }

    private sealed class FailingWrapWith(int step) : WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(UnbuildableTransform);
    }

    private sealed class RecordingTransform : IAmAMessageTransform
    {
        public IRequestContext? Context { get; set; }
        public void Dispose() { }
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }
        public Message Wrap(Message message, Publication publication) => message;
        public Message Unwrap(Message message) => message;
    }

    // a marker type the factory refuses to build, so it is never instantiated
    private sealed class UnbuildableTransform;

    private sealed class ThrowingReleaseTransformerFactory : IAmAMessageTransformerFactory
    {
        public List<IAmAMessageTransform> Created { get; } = new();
        public List<IAmAMessageTransform> Released { get; } = new();
        private IAmAMessageTransform? _throwOnRelease;

        public Lease<IAmAMessageTransform>? Create(Type transformerType)
        {
            //the failing transform cannot be built, so the build throws when it is reached
            if (transformerType == typeof(UnbuildableTransform)) return null;

            var transform = new RecordingTransform();
            Created.Add(transform);
            //the first transform built is the one whose release throws
            _throwOnRelease ??= transform;
            return new Lease<IAmAMessageTransform>(transform);
        }

        public void Release(Lease<IAmAMessageTransform> lease)
        {
            //record the attempt before a possible throw, so the test can see which transforms cleanup reached
            Released.Add(lease.Instance);
            if (ReferenceEquals(lease.Instance, _throwOnRelease))
                throw new InvalidOperationException("release failed");
        }
    }
}

public class AsyncTransformPipelineFailedBuildReleaseThrowTests
{
    [Fact]
    public void When_a_transform_release_throws_during_a_partial_build_the_others_are_released_and_the_error_is_not_masked_async()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var transformerFactory = new ThrowingReleaseTransformerFactoryAsync();
        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyThreeWrapTransformMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyThreeWrapTransformMessageMapperAsync>();
        var pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactory, InstrumentationOptions.All);

        //act
        var exception = Catch.Exception(() => pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert
        Assert.Equal(2, transformerFactory.Created.Count);
        Assert.Contains(transformerFactory.Created[1], transformerFactory.Released);
        var configException = Assert.IsType<ConfigurationException>(exception);
        var inner = Assert.IsType<ConfigurationException>(configException.InnerException);
        Assert.Contains("Could not create transformer", inner.Message);
    }

    [Fact]
    public void When_cleanup_of_a_failed_build_throws_the_original_error_is_not_masked_async()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var transformerFactory = new ThrowingReleaseTransformerFactoryAsync();
        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyExplicitUnwrapWrapMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyExplicitUnwrapWrapMessageMapperAsync>();
        var pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactory, InstrumentationOptions.All);

        //act
        var exception = Catch.Exception(() => pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());

        //assert
        var configException = Assert.IsType<ConfigurationException>(exception);
        var inner = Assert.IsType<ConfigurationException>(configException.InnerException);
        Assert.Contains("No MapToRequestAsync", inner.Message);
    }

    private sealed class MyThreeWrapTransformMessageMapperAsync : IAmAMessageMapperAsync<MyTransformableCommand>
    {
        public IRequestContext? Context { get; set; }

        [FirstWrapWith(3)]
        [SecondWrapWith(2)]
        [FailingWrapWith(1)]
        public Task<Message> MapToMessageAsync(MyTransformableCommand request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND),
                new MessageBody("test")));

        public Task<MyTransformableCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
            => Task.FromResult(new MyTransformableCommand());
    }

    private sealed class MyExplicitUnwrapWrapMessageMapperAsync : IAmAMessageMapperAsync<MyTransformableCommand>
    {
        public IRequestContext? Context { get; set; }

        [FirstWrapWith(1)]
        public Task<Message> MapToMessageAsync(MyTransformableCommand request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND),
                new MessageBody("test")));

        Task<MyTransformableCommand> IAmAMessageMapperAsync<MyTransformableCommand>.MapToRequestAsync(Message message, CancellationToken cancellationToken)
            => Task.FromResult(new MyTransformableCommand());
    }

    private sealed class FirstWrapWith(int step) : WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(RecordingTransformAsync);
    }

    private sealed class SecondWrapWith(int step) : WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(RecordingTransformAsync);
    }

    private sealed class FailingWrapWith(int step) : WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(UnbuildableTransformAsync);
    }

    private sealed class RecordingTransformAsync : IAmAMessageTransformAsync
    {
        public IRequestContext? Context { get; set; }
        public void Dispose() { }
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }
        public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken) => Task.FromResult(message);
        public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken) => Task.FromResult(message);
    }

    private sealed class UnbuildableTransformAsync;

    private sealed class ThrowingReleaseTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public List<IAmAMessageTransformAsync> Created { get; } = new();
        public List<IAmAMessageTransformAsync> Released { get; } = new();
        private IAmAMessageTransformAsync? _throwOnRelease;

        public Lease<IAmAMessageTransformAsync>? Create(Type transformerType)
        {
            if (transformerType == typeof(UnbuildableTransformAsync)) return null;

            var transform = new RecordingTransformAsync();
            Created.Add(transform);
            _throwOnRelease ??= transform;
            return new Lease<IAmAMessageTransformAsync>(transform);
        }

        //the failed-build cleanup drains through the synchronous Release, so that is the one that throws
        public void Release(Lease<IAmAMessageTransformAsync> lease)
        {
            Released.Add(lease.Instance);
            if (ReferenceEquals(lease.Instance, _throwOnRelease))
                throw new InvalidOperationException("release failed");
        }

        public ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync> lease)
        {
            Release(lease);
            return default;
        }
    }
}

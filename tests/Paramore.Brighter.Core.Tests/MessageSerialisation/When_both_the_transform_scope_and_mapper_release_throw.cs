using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

/// <summary>
/// Regression for PR #4254 review finding 6. A pipeline disposes the transform scope (<c>InstanceScope</c>) in
/// a <c>try</c> and releases its mapper in the <c>finally</c>, so the mapper is reclaimed even when transform-
/// scope disposal throws (pinned separately by <see cref="TransformPipelineMapperReleaseOnScopeThrowTests"/>).
/// But if the mapper release <b>also</b> throws, the <c>finally</c>'s exception replaces the <c>try</c>'s and
/// the transform-scope failure vanishes — the same "cleanup must not mask the real error" hazard the transform
/// build guards were added for. Both failures must surface. These pin all three production disposal paths; each
/// is proved RED by reverting the collect-both change in the hunk it pins.
/// </summary>
public class TransformPipelineBothReleasesThrowTests
{
    private const string TransformFailure = "transform release failed";
    private const string MapperFailure = "mapper release failed";

    [Fact]
    public void When_a_sync_pipelines_transform_scope_and_mapper_release_both_throw_both_surface()
    {
        var pipeline = new WrapPipeline<MinimalCommand>(
            Lease<IAmAMessageMapper<MinimalCommand>>.ForSharedInstance(new MinimalMapper()),
            messageTransformerFactory: new ThrowingOnReleaseTransformerFactory(),
            transformLeases: new Lease<IAmAMessageTransform>[] { Lease<IAmAMessageTransform>.ForSharedInstance(new NoOpTransform()) },
            instrumentationOptions: InstrumentationOptions.All,
            mapperRegistry: new ThrowingOnReleaseRegistry());

        var aggregate = Assert.Throws<AggregateException>(() => pipeline.Dispose());

        AssertBothSurface(aggregate);
    }

    [Fact]
    public async Task When_an_async_pipelines_transform_scope_and_mapper_release_both_throw_both_surface()
    {
        var pipeline = new WrapPipelineAsync<MinimalCommand>(
            Lease<IAmAMessageMapperAsync<MinimalCommand>>.ForSharedInstance(new MinimalMapperAsync()),
            messageTransformerFactoryAsync: new ThrowingOnReleaseTransformerFactoryAsync(),
            transformLeases: new Lease<IAmAMessageTransformAsync>[] { Lease<IAmAMessageTransformAsync>.ForSharedInstance(new NoOpTransformAsync()) },
            instrumentationOptions: InstrumentationOptions.All,
            mapperRegistry: new ThrowingOnReleaseRegistryAsync());

        var aggregate = await Assert.ThrowsAsync<AggregateException>(async () => await pipeline.DisposeAsync());

        AssertBothSurface(aggregate);
    }

    [Fact]
    public void When_an_async_pipelines_synchronous_disposal_transform_scope_and_mapper_release_both_throw_both_surface()
    {
        var pipeline = new WrapPipelineAsync<MinimalCommand>(
            Lease<IAmAMessageMapperAsync<MinimalCommand>>.ForSharedInstance(new MinimalMapperAsync()),
            messageTransformerFactoryAsync: new ThrowingOnReleaseTransformerFactoryAsync(),
            transformLeases: new Lease<IAmAMessageTransformAsync>[] { Lease<IAmAMessageTransformAsync>.ForSharedInstance(new NoOpTransformAsync()) },
            instrumentationOptions: InstrumentationOptions.All,
            mapperRegistry: new ThrowingOnReleaseRegistryAsync());

        var aggregate = Assert.Throws<AggregateException>(() => pipeline.Dispose());

        AssertBothSurface(aggregate);
    }

    //both the transform-scope failure and the mapper-release failure must be reachable from the surfaced
    //aggregate; flattening tolerates the transform scope's own drain-aggregate nesting
    private static void AssertBothSurface(AggregateException aggregate)
    {
        var messages = aggregate.Flatten().InnerExceptions.Select(e => e.Message).ToArray();
        Assert.Contains(TransformFailure, messages);
        Assert.Contains(MapperFailure, messages);
    }

    private sealed class MinimalCommand() : Command(Guid.NewGuid());

    private sealed class MinimalMapper : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }

    private sealed class MinimalMapperAsync : IAmAMessageMapperAsync<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }
        public Task<Message> MapToMessageAsync(MinimalCommand request, Publication publication,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MinimalCommand> MapToRequestAsync(Message message,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoOpTransform : IAmAMessageTransform
    {
        public IRequestContext? Context { get; set; }
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }
        public Message Wrap(Message message, Publication publication) => throw new NotImplementedException();
        public Message Unwrap(Message message) => throw new NotImplementedException();
        public void Dispose() { }
    }

    private sealed class NoOpTransformAsync : IAmAMessageTransformAsync
    {
        public IRequestContext? Context { get; set; }
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }
        public Task<Message> WrapAsync(Message message, Publication publication,
            CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public void Dispose() { }
    }

    //releasing the tracked transform throws, so InstanceScope disposal throws
    private sealed class ThrowingOnReleaseTransformerFactory : IAmAMessageTransformerFactory
    {
        public Lease<IAmAMessageTransform>? Create(Type transformerType) => throw new NotImplementedException();
        public void Release(Lease<IAmAMessageTransform>? lease) => throw new InvalidOperationException(TransformFailure);
    }

    private sealed class ThrowingOnReleaseTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public Lease<IAmAMessageTransformAsync>? Create(Type transformerType) => throw new NotImplementedException();
        public void Release(Lease<IAmAMessageTransformAsync>? lease) => throw new InvalidOperationException(TransformFailure);
        public ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync>? lease) => throw new InvalidOperationException(TransformFailure);
    }

    //releasing the mapper also throws, so the finally's exception would otherwise mask the transform failure
    private sealed class ThrowingOnReleaseRegistry : IAmAMessageMapperRegistry
    {
        public Lease<IAmAMessageMapper<T>>? Get<T>() where T : class, IRequest => null;
        public (Type? MapperType, bool IsDefault) ResolveMapperInfo(Type requestType) => (null, false);
        public void Release<T>(Lease<IAmAMessageMapper<T>>? lease) where T : class, IRequest =>
            throw new InvalidOperationException(MapperFailure);
        public void Register<TRequest, TMessageMapper>()
            where TRequest : class, IRequest
            where TMessageMapper : class, IAmAMessageMapper<TRequest> { }
        public void Register(Type request, Type mapper) { }
    }

    private sealed class ThrowingOnReleaseRegistryAsync : IAmAMessageMapperRegistryAsync
    {
        public Lease<IAmAMessageMapperAsync<T>>? GetAsync<T>() where T : class, IRequest => null;
        public (Type? MapperType, bool IsDefault) ResolveAsyncMapperInfo(Type requestType) => (null, false);
        public void Release<T>(Lease<IAmAMessageMapperAsync<T>>? lease) where T : class, IRequest =>
            throw new InvalidOperationException(MapperFailure);
        public ValueTask ReleaseAsync<T>(Lease<IAmAMessageMapperAsync<T>>? lease) where T : class, IRequest =>
            throw new InvalidOperationException(MapperFailure);
        public void RegisterAsync<TRequest, TMessageMapper>()
            where TRequest : class, IRequest
            where TMessageMapper : class, IAmAMessageMapperAsync<TRequest> { }
        public void RegisterAsync(Type request, Type mapper) { }
    }
}

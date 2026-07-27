using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

/// <summary>
/// Regression for the mapper-leak-on-scope-throw hazard raised in the PR #4254 review. A pipeline releases
/// its mapper <b>after</b> disposing the transform scope (<c>InstanceScope</c>), and the release-once guard
/// (<c>_released</c>) is set at the top of the disposal. So if transform-scope disposal throws — the same
/// synchronous-scope-throw shape this PR is careful about elsewhere — the mapper release is skipped, and
/// because the guard is already set neither the finalizer nor a later dispose retries it. The mapper's DI
/// scope is then permanently orphaned: the exact leak class this PR closes.
///
/// The mapper must be released in a <c>finally</c>, best-effort, even when transform-scope disposal throws.
/// The scope-disposal exception must still surface to the owner (only the finalizer swallows), so these
/// assert both that it propagates and that the mapper was released anyway. One fact per production disposal
/// path; each is proved RED by reverting the <c>try/finally</c> in the hunk it pins.
/// </summary>
public class TransformPipelineMapperReleaseOnScopeThrowTests
{
    [Fact]
    public void When_a_sync_pipelines_transform_scope_disposal_throws_the_mapper_is_still_released()
    {
        //arrange — a sync pipeline with a transform whose factory Release throws (so InstanceScope.Dispose
        //throws) and a registry that records the mapper release
        var mapper = new ReleaseProbe();
        var pipeline = new WrapPipeline<MinimalCommand>(
            new MinimalMapper(),
            messageTransformerFactory: new ThrowingOnReleaseTransformerFactory(),
            transforms: new IAmAMessageTransform[] { new NoOpTransform() },
            instrumentationOptions: InstrumentationOptions.All,
            mapperRegistry: new RecordingReleaseRegistry(mapper));

        //act — the transform-scope disposal exception still surfaces to the owner
        Assert.Throws<InvalidOperationException>(() => pipeline.Dispose());

        //assert — but the mapper was released anyway (sync Release), not orphaned
        Assert.True(mapper.WasReleased, "the mapper was orphaned when transform-scope disposal threw");
    }

    [Fact]
    public async Task When_an_async_pipelines_transform_scope_disposal_throws_the_mapper_is_still_released()
    {
        //arrange — the async DisposeAsync path: an async transform whose factory ReleaseAsync throws
        var mapper = new ReleaseProbe();
        var pipeline = new WrapPipelineAsync<MinimalCommand>(
            new MinimalMapperAsync(),
            messageTransformerFactoryAsync: new ThrowingOnReleaseTransformerFactoryAsync(),
            transforms: new IAmAMessageTransformAsync[] { new NoOpTransformAsync() },
            instrumentationOptions: InstrumentationOptions.All,
            mapperRegistry: new RecordingReleaseRegistryAsync(mapper));

        //act
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.DisposeAsync());

        //assert — the mapper was released via ReleaseAsync
        Assert.True(mapper.WasReleased, "the mapper was orphaned when async transform-scope disposal threw");
    }

    [Fact]
    public void When_an_async_pipelines_synchronous_disposal_throws_the_mapper_is_still_released()
    {
        //arrange — the async pipeline's synchronous Dispose (the finalizer's fallback path) releases the
        //transform scope through sync Release, which throws here
        var mapper = new ReleaseProbe();
        var pipeline = new WrapPipelineAsync<MinimalCommand>(
            new MinimalMapperAsync(),
            messageTransformerFactoryAsync: new ThrowingOnReleaseTransformerFactoryAsync(),
            transforms: new IAmAMessageTransformAsync[] { new NoOpTransformAsync() },
            instrumentationOptions: InstrumentationOptions.All,
            mapperRegistry: new RecordingReleaseRegistryAsync(mapper));

        //act
        Assert.Throws<InvalidOperationException>(() => pipeline.Dispose());

        //assert — the mapper was released via sync Release
        Assert.True(mapper.WasReleased, "the mapper was orphaned when the async pipeline's sync disposal threw");
    }

    private sealed class ReleaseProbe
    {
        private int _released;
        public bool WasReleased => Volatile.Read(ref _released) != 0;
        public void Mark() => Interlocked.Exchange(ref _released, 1);
    }

    private sealed class MinimalCommand() : Command(Guid.NewGuid());

    private sealed class MinimalMapper : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }

        public Message MapToMessage(MinimalCommand request, Publication publication) =>
            throw new NotImplementedException();

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

    //a transform that is never run; only its release matters, and that is made to throw via the factory
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

    //releasing a tracked transform throws, standing in for MS DI's sync scope Dispose throwing for an
    //IAsyncDisposable-only service
    private sealed class ThrowingOnReleaseTransformerFactory : IAmAMessageTransformerFactory
    {
        public IAmAMessageTransform Create(Type transformerType) => throw new NotImplementedException();
        public void Release(IAmAMessageTransform transformer) =>
            throw new InvalidOperationException("transform release failed");
    }

    private sealed class ThrowingOnReleaseTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public IAmAMessageTransformAsync Create(Type transformerType) => throw new NotImplementedException();
        public void Release(IAmAMessageTransformAsync transformer) =>
            throw new InvalidOperationException("transform release failed");
        public ValueTask ReleaseAsync(IAmAMessageTransformAsync transformer) =>
            throw new InvalidOperationException("transform release failed");
    }

    //records that the mapper was returned to its factory, so the test can prove the release still happened
    //despite the transform-scope disposal throwing
    private sealed class RecordingReleaseRegistry(ReleaseProbe probe) : IAmAMessageMapperRegistry
    {
        public IAmAMessageMapper<T>? Get<T>() where T : class, IRequest => null;
        public (Type? MapperType, bool IsDefault) ResolveMapperInfo(Type requestType) => (null, false);
        public void Release(IAmAMessageMapper mapper) => probe.Mark();
        public void Register<TRequest, TMessageMapper>()
            where TRequest : class, IRequest
            where TMessageMapper : class, IAmAMessageMapper<TRequest> { }
        public void Register(Type request, Type mapper) { }
    }

    private sealed class RecordingReleaseRegistryAsync(ReleaseProbe probe) : IAmAMessageMapperRegistryAsync
    {
        public IAmAMessageMapperAsync<T>? GetAsync<T>() where T : class, IRequest => null;
        public (Type? MapperType, bool IsDefault) ResolveAsyncMapperInfo(Type requestType) => (null, false);
        public void Release(IAmAMessageMapperAsync mapper) => probe.Mark();
        public ValueTask ReleaseAsync(IAmAMessageMapperAsync mapper)
        {
            probe.Mark();
            return default;
        }
        public void RegisterAsync<TRequest, TMessageMapper>()
            where TRequest : class, IRequest
            where TMessageMapper : class, IAmAMessageMapperAsync<TRequest> { }
        public void RegisterAsync(Type request, Type mapper) { }
    }
}

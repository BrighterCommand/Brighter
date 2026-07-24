using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformPipelineFinalizerReleaseTests
{
    [Fact]
    public void When_a_pipeline_finalizer_release_throws_it_should_not_escape()
    {
        //arrange: a pipeline whose mapper registry throws when the mapper is released. This is the same
        //shape as MS DI's synchronous scope Dispose throwing for an IAsyncDisposable-only mapper. The
        //pipeline is never disposed, so only the finalizer releases it. Before the finalizer guard this
        //exception escaped ~TransformPipeline on the finalizer thread and terminated the process.
        //Revert the try/catch in ~TransformPipeline to prove RED: the test host crashes rather than
        //failing an assertion, because an exception escaping a finalizer tears down the process.
        CreateAndAbandonWrapPipeline();

        //act: force the finalizer to run
        CollectAndRunFinalizers();

        //assert: reaching here at all means the finalizer swallowed the release exception instead of
        //crashing the process
        Assert.True(true);
    }

    [Fact]
    public void When_an_async_pipeline_finalizer_release_throws_it_should_not_escape()
    {
        //arrange: the async pipeline's finalizer runs the same synchronous release path and carries the
        //same guard as the sync pipeline. Revert the try/catch in ~TransformPipelineAsync to prove RED.
        CreateAndAbandonWrapPipelineAsync();

        //act
        CollectAndRunFinalizers();

        //assert
        Assert.True(true);
    }

    private static void CollectAndRunFinalizers()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    //isolated and non-inlined so the pipeline is not kept alive on the test method's stack frame and can
    //be collected on the following GC
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndAbandonWrapPipeline()
    {
        //no transformer factory, so InstanceScope stays null and the finalizer's only work is the
        //mapper release, which throws
        _ = new WrapPipeline<MinimalCommand>(
            new MinimalMapper(),
            messageTransformerFactory: null,
            transforms: Array.Empty<IAmAMessageTransform>(),
            instrumentationOptions: InstrumentationOptions.All,
            mapperRegistry: new ThrowingOnReleaseRegistry());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndAbandonWrapPipelineAsync()
    {
        _ = new WrapPipelineAsync<MinimalCommand>(
            new MinimalMapperAsync(),
            messageTransformerFactoryAsync: null!,
            transforms: Array.Empty<IAmAMessageTransformAsync>(),
            instrumentationOptions: InstrumentationOptions.All,
            mapperRegistry: new ThrowingOnReleaseRegistryAsync());
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

    //stands in for a factory-backed registry whose synchronous release throws — e.g. releasing a scope
    //that holds an IAsyncDisposable-only mapper through MS DI's throwing sync Dispose
    private sealed class ThrowingOnReleaseRegistry : IAmAMessageMapperRegistry
    {
        public IAmAMessageMapper<T>? Get<T>() where T : class, IRequest => null;

        public void Release(IAmAMessageMapper mapper) =>
            throw new InvalidOperationException("release failed");

        public void Register<TRequest, TMessageMapper>()
            where TRequest : class, IRequest
            where TMessageMapper : class, IAmAMessageMapper<TRequest> { }

        public void Register(Type request, Type mapper) { }
    }

    private sealed class ThrowingOnReleaseRegistryAsync : IAmAMessageMapperRegistryAsync
    {
        public IAmAMessageMapperAsync<T>? GetAsync<T>() where T : class, IRequest => null;

        public void Release(IAmAMessageMapperAsync mapper) =>
            throw new InvalidOperationException("release failed");

        public ValueTask ReleaseAsync(IAmAMessageMapperAsync mapper) =>
            throw new InvalidOperationException("release failed");

        public void RegisterAsync<TRequest, TMessageMapper>()
            where TRequest : class, IRequest
            where TMessageMapper : class, IAmAMessageMapperAsync<TRequest> { }

        public void RegisterAsync(Type request, Type mapper) { }
    }
}

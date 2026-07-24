using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Regression for the sync-over-async release hazard raised in the PR #4254 code review: disposing an
/// async transform pipeline releases its mapper on the releasing thread. On the Proactor that thread is
/// the message pump, running under a single-threaded <see cref="BrighterSynchronizationContext"/>. A
/// transient mapper resolved from the DI container is disposed through the scope it was resolved from;
/// if that mapper is <see cref="IAsyncDisposable"/> and its <c>DisposeAsync</c> awaits without
/// <c>ConfigureAwait(false)</c>, the continuation is posted back to the pump context. Blocking the pump
/// thread on that disposal (<c>pending.AsTask().GetAwaiter().GetResult()</c>) would then deadlock — the
/// one thread that could run the continuation is the one we blocked.
///
/// Both disposal shapes must complete without deadlock:
/// - <c>await using</c> (Part A) awaits the release, so the pump thread is suspended, not blocked, and
///   drains the posted continuation.
/// - <c>using</c> (Part B) still blocks, but the scope's disposal is offloaded to a pool thread that has
///   no captured context, so the mapper's continuation resumes off-pump and the wait completes.
///
/// If either path regresses, the enclosing <see cref="BrighterAsyncContext.Run(Func{Task})"/> never
/// returns and the <c>Wait</c> below times out.
/// </summary>
public class ReleaseAsyncDisposableMapperOnPumpContextTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    static ReleaseAsyncDisposableMapperOnPumpContextTests()
    {
        //the synchronous-dispose case offloads the scope disposal to the thread pool (see
        //ServiceProviderLifetimeScope.DisposeScope); give the pool a floor of ready worker threads so
        //this measures deadlock-freedom rather than pool starvation when the whole suite runs in parallel.
        //Raising the minimum only makes the pool more eager, so it cannot destabilise other tests.
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(Math.Max(workerThreads, 16), completionPortThreads);
    }

    [Fact]
    public void When_releasing_an_async_disposable_mapper_via_await_using_it_should_not_deadlock()
    {
        //arrange
        var probe = BuildPipelineFactory(out var builder);

        //act — dispose asynchronously (await using), the Proactor path, on the single-threaded pump
        RunOnPumpThread(() => BrighterAsyncContext.Run(async () =>
            {
                await using var pipeline = builder.BuildUnwrapPipeline<MinimalEvent>();
                await Task.Yield();
            }),
            "await using disposal deadlocked the single-threaded pump context");

        //assert
        Assert.Equal(1, probe.DisposedCount);
    }

    [Fact]
    public void When_releasing_an_async_disposable_mapper_via_synchronous_dispose_it_should_not_deadlock()
    {
        //arrange
        var probe = BuildPipelineFactory(out var builder);

        //act — dispose synchronously (using) on the pump context; the offload in ServiceProviderLifetimeScope
        //must keep this from deadlocking even though a blocking wait is unavoidable here
        RunOnPumpThread(() => BrighterAsyncContext.Run(async () =>
            {
                using (builder.BuildUnwrapPipeline<MinimalEvent>())
                {
                    await Task.Yield();
                }
            }),
            "synchronous disposal deadlocked the single-threaded pump context");

        //assert
        Assert.Equal(1, probe.DisposedCount);
    }

    // Hosts the pump on a dedicated thread (not the thread pool) so a genuine deadlock is detected by a
    // Join timeout rather than by pool-thread availability — the whole suite shares the pool, and hosting
    // the blocking pump on it makes the timeout flaky under load.
    private static void RunOnPumpThread(Action pump, string deadlockMessage)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { pump(); }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true, Name = "brighter-test-pump" };

        thread.Start();

        Assert.True(thread.Join(Timeout), deadlockMessage);
        Assert.Null(failure);
    }

    private static DisposeProbe BuildPipelineFactory(out TransformPipelineBuilderAsync builder)
    {
        var probe = new DisposeProbe();
        var collection = new ServiceCollection();
        collection.AddSingleton(probe);
        collection.AddTransient<AsyncDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        var mapperFactory = new ServiceProviderMapperFactoryAsync(provider);
        var mapperRegistry = new MessageMapperRegistry(null, mapperFactory);
        mapperRegistry.RegisterAsync<MinimalEvent, AsyncDisposableMapper>();

        builder = new TransformPipelineBuilderAsync(
            mapperRegistry, new NoOpTransformerFactoryAsync(), InstrumentationOptions.None);
        return probe;
    }

    private sealed class DisposeProbe
    {
        private int _disposedCount;
        public int DisposedCount => Volatile.Read(ref _disposedCount);
        public void Increment() => Interlocked.Increment(ref _disposedCount);
    }

    private sealed class MinimalEvent : Event
    {
        public MinimalEvent() : base(Guid.NewGuid()) { }
    }

    // A transient, async-disposable mapper whose DisposeAsync awaits without ConfigureAwait(false), so its
    // continuation is posted back to whatever SynchronizationContext is current when it is released.
    private sealed class AsyncDisposableMapper(DisposeProbe probe) : IAmAMessageMapperAsync<MinimalEvent>, IAsyncDisposable
    {
        public IRequestContext? Context { get; set; }

        public Task<Message> MapToMessageAsync(MinimalEvent request, Publication publication,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MinimalEvent> MapToRequestAsync(Message message,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            probe.Increment();
        }
    }

    private sealed class NoOpTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public IAmAMessageTransformAsync? Create(Type transformerType) => null;
        public void Release(IAmAMessageTransformAsync transformer) { }
        public ValueTask ReleaseAsync(IAmAMessageTransformAsync transformer) => default;
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Groups the pump-context deadlock tests into a collection that does not run in parallel with the rest of
/// the suite. Each such test blocks a dedicated pump thread on an offloaded disposal that needs a free
/// thread-pool thread to complete; letting them contend with the whole parallel suite for the pool can
/// starve that offload and make the 30s deadlock guard trip spuriously. Serialising them removes the
/// contention without weakening what they assert.
/// </summary>
[CollectionDefinition(PumpContextDeadlockCollection.Name, DisableParallelization = true)]
public sealed class PumpContextDeadlockCollection
{
    public const string Name = "PumpContextDeadlock";
}

/// <summary>
/// Regression for the reply-path half of the sync-over-async release hazard raised in PR #4254.
/// <see cref="OutboxProducerMediator{TMessage,TTransaction}.CreateRequestFromMessage{TRequest}"/> builds an
/// <b>async</b> unwrap pipeline (<c>IAsyncDisposable</c>) but disposes it through a synchronous <c>using</c>
/// and blocks the unwrap on <c>GetAwaiter().GetResult()</c>. When that runs on the Proactor's single-threaded
/// <see cref="BrighterSynchronizationContext"/>, disposing releases a transient <see cref="IAsyncDisposable"/>
/// mapper on the pump thread; if the mapper's <c>DisposeAsync</c> awaits without <c>ConfigureAwait(false)</c>
/// its continuation is posted back to the pump. Blocking the pump on that disposal would deadlock — the one
/// thread that could run the continuation is the one we blocked.
///
/// The <c>send</c> path (<c>Proactor.TranslateMessage</c>) is covered by
/// <see cref="ReleaseAsyncDisposableMapperOnPumpContextTests"/>; this exercises the distinct <c>reply</c>
/// call shape. Deadlock-freedom rides on the offload in <c>ServiceProviderLifetimeScope.DisposeScope</c>:
/// the blocking wait is unavoidable here, but the scope's disposal is handed to a pool thread with no
/// captured context, so the mapper's continuation resumes off-pump and the wait completes.
///
/// The unwrap itself is genuinely sync-over-async and is <b>not</b> protected by that offload, so the mapper
/// keeps map/unwrap trivial (no continuation posted back to the pump) and confines the context-capturing
/// await to <c>DisposeAsync</c>, isolating the disposal path under test.
///
/// If the offload regresses, the enclosing <see cref="BrighterAsyncContext.Run(Func{Task})"/> never returns
/// and the <c>Join</c> below times out.
/// </summary>
[Collection(PumpContextDeadlockCollection.Name)]
public class CreateRequestFromReplyMessageOnPumpContextTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    static CreateRequestFromReplyMessageOnPumpContextTests()
    {
        //the disposal is offloaded to the thread pool (see ServiceProviderLifetimeScope.DisposeScope); give
        //the pool a floor of ready worker threads so this measures deadlock-freedom rather than pool
        //starvation when the whole suite runs in parallel. Raising the minimum only makes the pool more
        //eager, so it cannot destabilise other tests.
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(Math.Max(workerThreads, 16), completionPortThreads);
    }

    [Fact]
    public void When_creating_a_request_from_a_reply_message_on_the_pump_context_it_should_not_deadlock()
    {
        //arrange
        var probe = BuildMediator(out var mediator);

        //act — unwrap a reply into a request on the single-threaded pump; CreateRequestFromMessage disposes
        //the async unwrap pipeline synchronously, releasing the async-disposable mapper on the pump thread
        RunOnPumpThread(() => BrighterAsyncContext.Run(async () =>
            {
                mediator.CreateRequestFromMessage<MinimalCommand>(
                    new Message(), new RequestContext(), out _);
                await Task.Yield();
            }),
            "reply-path disposal deadlocked the single-threaded pump context");

        //assert — the mapper was released on the pump (so the offloaded disposal path really ran, not a
        //no-op; the primary signal is that the pump above did not deadlock).
        //
        //The count is 2, not 1, because CreateRequestFromMessage resolves and releases a mapper TWICE:
        // 1. HasPipeline<TRequest>() resolves a throwaway mapper purely to answer "is there a pipeline?"
        //    and releases it (TransformPipelineBuilderAsync.HasPipeline).
        // 2. BuildUnwrapPipeline<TRequest>() resolves a second mapper for the actual pipeline, which the
        //    `using` disposes.
        //Both are transient IAsyncDisposable instances, so both increment the probe. If a future refactor
        //removes the HasPipeline probe allocation (tracked as the deferred outbox hot-path change), this
        //will drop to 1 — that is the signal to update this expectation, not a regression.
        Assert.Equal(2, probe.DisposedCount);
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

    private static DisposeProbe BuildMediator(out OutboxProducerMediator<Message, CommittableTransaction> mediator)
    {
        var probe = new DisposeProbe();
        var collection = new ServiceCollection();
        collection.AddSingleton(probe);
        collection.AddTransient<AsyncDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        var mapperFactory = new ServiceProviderMapperFactoryAsync(provider);
        var mapperRegistry = new MessageMapperRegistry(null, mapperFactory);
        mapperRegistry.RegisterAsync<MinimalCommand, AsyncDisposableMapper>();

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>());

        mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            mapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            null,
            new FindPublicationByPublicationTopicOrRequestType());

        return probe;
    }

    private sealed class DisposeProbe
    {
        private int _disposedCount;
        public int DisposedCount => Volatile.Read(ref _disposedCount);
        public void Increment() => Interlocked.Increment(ref _disposedCount);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    // A transient, async-disposable mapper. Map/unwrap complete synchronously so no continuation is posted
    // back to the pump by the (genuinely sync-over-async) unwrap; only DisposeAsync awaits without
    // ConfigureAwait(false), so its continuation is posted back to whatever SynchronizationContext is current
    // when the pipeline is released — isolating the disposal path under test.
    private sealed class AsyncDisposableMapper(DisposeProbe probe) : IAmAMessageMapperAsync<MinimalCommand>, IAsyncDisposable
    {
        public IRequestContext? Context { get; set; }

        public Task<Message> MapToMessageAsync(MinimalCommand request, Publication publication,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MinimalCommand> MapToRequestAsync(Message message,
            CancellationToken cancellationToken = default) => Task.FromResult(new MinimalCommand());

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            probe.Increment();
        }
    }
}

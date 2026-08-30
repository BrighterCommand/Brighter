using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScopedMapperPerPipelineTests
{
    [Fact]
    public void When_consuming_two_messages_a_scoped_mapper_should_not_be_reused()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped
        TrackingMapper.Events.Clear();
        TransformPipelineBuilder.ClearPipelineCache();

        var scopeTracker = BuildScopeTracker(out var trackingProvider);
        using var mapperFactory = new ServiceProviderMapperFactory(trackingProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<MinimalCommand, TrackingMapper>();

        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, new EmptyMessageTransformerFactory());

        //act — consume message N: build and dispose its pipeline, which constructs the Scoped mapper
        //for this pipeline and, on Dispose, releases the pipeline's owned DI scope
        var pipelineForMessageN = pipelineBuilder.BuildWrapPipeline<MinimalCommand>();
        pipelineForMessageN.Dispose();

        //act — consume message N+1: a second, independent pipeline
        var pipelineForMessageNPlus1 = pipelineBuilder.BuildWrapPipeline<MinimalCommand>();
        pipelineForMessageNPlus1.Dispose();

        //assert — two distinct mapper instances, the first disposed strictly before the second was
        //constructed (the ordering, not merely the distinctness), and no Brighter-created scope left live
        Assert.Equal(new[] { "Constructed:1", "Disposed:1", "Constructed:2", "Disposed:2" }, TrackingMapper.Events);
        Assert.Equal(0, scopeTracker.OutstandingCount);
    }

    private static ScopeTracker BuildScopeTracker(out IServiceProvider trackingProvider)
    {
        var collection = new ServiceCollection();
        collection.AddScoped<TrackingMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);
        return scopeTracker;
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    //records construction/disposal identity and order, so the test can assert one instance per
    //pipeline and that the first is torn down before the next is built — not merely that they differ
    private sealed class TrackingMapper : IAmAMessageMapper<MinimalCommand>, IDisposable
    {
        public static readonly List<string> Events = new();
        private static int s_nextId;
        private readonly int _id = Interlocked.Increment(ref s_nextId);

        public TrackingMapper() => Events.Add($"Constructed:{_id}");

        public IRequestContext? Context { get; set; }

        public Message MapToMessage(MinimalCommand request, Publication publication) =>
            new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

        public MinimalCommand MapToRequest(Message message) => new();

        public void Dispose() => Events.Add($"Disposed:{_id}");
    }

    // Wraps the real IServiceScopeFactory and counts every scope creation/disposal, so the test can
    // assert no Brighter-created scope is left live once both pipelines have been disposed (NFR-5)
    private sealed class ScopeTracker(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _createdCount;
        private int _disposedCount;

        public int OutstandingCount => Volatile.Read(ref _createdCount) - Volatile.Read(ref _disposedCount);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _createdCount);
            return new TrackingScope(inner.CreateScope(), () => Interlocked.Increment(ref _disposedCount));
        }

        private sealed class TrackingScope(IServiceScope inner, Action onDispose) : IServiceScope, IAsyncDisposable
        {
            private int _disposed;

            public IServiceProvider ServiceProvider => inner.ServiceProvider;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) onDispose();
                inner.Dispose();
            }

            //Production on net8+ disposes an IAsyncDisposable scope through DisposeAsync, so count the
            //disposal in whichever shape the caller uses.
            public async ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) onDispose();
                if (inner is IAsyncDisposable asyncInner)
                    await asyncInner.DisposeAsync().ConfigureAwait(false);
                else
                    inner.Dispose();
            }
        }
    }

    private sealed class TrackingServiceProvider(IServiceProvider inner, ScopeTracker scopeTracker) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceScopeFactory) ? scopeTracker : inner.GetService(serviceType);
    }
}

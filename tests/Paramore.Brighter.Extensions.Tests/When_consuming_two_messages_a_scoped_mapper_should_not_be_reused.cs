using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScopedMapperPerPipelineTests
{
    [Fact]
    public void When_consuming_two_messages_a_scoped_mapper_should_not_be_reused()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped
        TransformPipelineBuilder.ClearPipelineCache();

        var recorder = new ConstructionOrderRecorder();
        var scopeTracker = BuildScopeTracker(recorder, out var trackingProvider);
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
        Assert.Equal(new[] { "Constructed:1", "Disposed:1", "Constructed:2", "Disposed:2" }, recorder.Events);
        Assert.Equal(0, scopeTracker.OutstandingCount);
    }

    private static ScopeTracker BuildScopeTracker(ConstructionOrderRecorder recorder, out IServiceProvider trackingProvider)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton(recorder);
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

    //records construction/disposal identity and order via the injected recorder, so the test can
    //assert one instance per pipeline and that the first is torn down before the next is built —
    //not merely that they differ
    private sealed class TrackingMapper : IAmAMessageMapper<MinimalCommand>, IDisposable
    {
        private readonly ConstructionOrderRecorder _recorder;
        private readonly int _id;

        public TrackingMapper(ConstructionOrderRecorder recorder)
        {
            _recorder = recorder;
            _id = recorder.RecordConstruction();
        }

        public IRequestContext? Context { get; set; }

        public Message MapToMessage(MinimalCommand request, Publication publication) =>
            new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

        public MinimalCommand MapToRequest(Message message) => new();

        public void Dispose() => _recorder.RecordDisposal(_id);
    }
}

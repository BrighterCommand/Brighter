using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;


namespace Paramore.Brighter.Extensions.Tests;

public class ScopedTransformPerPipelineTests
{
    [Test]
    public async System.Threading.Tasks.Task When_consuming_two_messages_a_scoped_transform_should_not_be_reused_by_the_sync_builder()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped. The mapper factory offers no
        //pipeline scope of its own (SimpleMessageMapperFactory), so CreatePipelineScope() has only the
        //transformer factory's scope to fall back to — the v9 compatibility path
        TransformPipelineBuilder.ClearPipelineCache();

        var recorder = new ConstructionOrderRecorder();
        var scopeTracker = BuildScopeTracker(recorder, out var trackingProvider);
        using var transformerFactory = new ServiceProviderTransformerFactory(trackingProvider);
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MinimalMapper()), null);
        mapperRegistry.Register<MinimalCommand, MinimalMapper>();

        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, transformerFactory);

        //act — consume message N: build and dispose its pipeline, which constructs the Scoped unwrap
        //transform for this pipeline and, on Dispose, releases the pipeline's owned DI scope
        var pipelineForMessageN = pipelineBuilder.BuildUnwrapPipeline<MinimalCommand>();
        pipelineForMessageN.Dispose();

        //act — consume message N+1: a second, independent pipeline
        var pipelineForMessageNPlus1 = pipelineBuilder.BuildUnwrapPipeline<MinimalCommand>();
        pipelineForMessageNPlus1.Dispose();

        //assert — two distinct transform instances, the first disposed strictly before the second was
        //constructed (the ordering, not merely the distinctness), and no Brighter-created scope left live
        await Assert.That(recorder.Events).IsEquivalentTo(new[] { "Constructed:1", "Disposed:1", "Constructed:2", "Disposed:2" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scopeTracker.OutstandingCount).IsEqualTo(0);
    }

    [Test]
    public async Task When_consuming_two_messages_a_scoped_transform_should_not_be_reused_by_the_async_builder()
    {
        //arrange — the async/Proactor twin of the fact above, over TransformPipelineBuilderAsync
        TransformPipelineBuilderAsync.ClearPipelineCache();

        var recorder = new ConstructionOrderRecorder();
        var scopeTracker = BuildScopeTracker(recorder, out var trackingProvider);
        using var transformerFactory = new ServiceProviderTransformerFactoryAsync(trackingProvider);
        var mapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MinimalMapperAsync()));
        mapperRegistry.RegisterAsync<MinimalCommand, MinimalMapperAsync>();

        var pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactory, InstrumentationOptions.All);

        //act — consume message N, then message N+1: two independent pipelines
        var pipelineForMessageN = pipelineBuilder.BuildUnwrapPipeline<MinimalCommand>();
        await pipelineForMessageN.DisposeAsync();

        var pipelineForMessageNPlus1 = pipelineBuilder.BuildUnwrapPipeline<MinimalCommand>();
        await pipelineForMessageNPlus1.DisposeAsync();

        //assert
        await Assert.That(recorder.Events).IsEquivalentTo(new[] { "Constructed:1", "Disposed:1", "Constructed:2", "Disposed:2" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scopeTracker.OutstandingCount).IsEqualTo(0);
    }

    private static ScopeTracker BuildScopeTracker(ConstructionOrderRecorder recorder, out IServiceProvider trackingProvider)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton(recorder);
        collection.AddScoped<TrackingTransform>();
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
}

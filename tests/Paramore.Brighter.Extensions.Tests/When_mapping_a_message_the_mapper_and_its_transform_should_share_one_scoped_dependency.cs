using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class SharedScopedDependencyPerPipelineTests
{
    [Fact]
    public void When_mapping_a_message_the_mapper_and_its_transform_should_share_one_scoped_dependency()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped. IMarker is registered
        //AddScoped and injected into both the mapper and its [UnwrapWith] transform
        TransformPipelineBuilder.ClearPipelineCache();

        var log = new MarkerLog();
        var collection = new ServiceCollection();
        collection.AddSingleton(log);
        collection.AddScoped<IMarker, Marker>();
        collection.AddScoped<MarkerMapper>();
        collection.AddScoped<MarkerTransform>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = collection.BuildServiceProvider();

        using var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        using var transformerFactory = new ServiceProviderTransformerFactory(rootProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<MarkerCommand, MarkerMapper>();

        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, transformerFactory);

        //act — map message N: building the pipeline constructs both the mapper and its unwrap
        //transform, each recording the IMarker it was resolved with
        var pipelineForMessageN = pipelineBuilder.BuildUnwrapPipeline<MarkerCommand>();

        //assert — the mapper's IMarker and the transform's IMarker are reference-equal for message N
        Assert.Same(log.MapperMarkers[0], log.TransformMarkers[0]);

        //act — disposing the pipeline closes its DI scope
        pipelineForMessageN.Dispose();

        //assert — message N's IMarker was disposed at the end of its pipeline
        Assert.True(log.MapperMarkers[0].IsDisposed);

        //act — map message N+1: a second, independent pipeline
        var pipelineForMessageNPlus1 = pipelineBuilder.BuildUnwrapPipeline<MarkerCommand>();
        pipelineForMessageNPlus1.Dispose();

        //assert — message N+1's mapper and transform shared a second IMarker, distinct from message N's
        Assert.Same(log.MapperMarkers[1], log.TransformMarkers[1]);
        Assert.NotSame(log.MapperMarkers[0], log.MapperMarkers[1]);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransformPipelineBuilderFailureReleaseTests
{
    [Fact]
    public void When_building_a_wrap_pipeline_throws_should_release_the_mapper()
    {
        //arrange
        var scopeTracker = BuildScopeTracker(out var trackingProvider);
        using var mapperFactory = new ServiceProviderMapperFactory(trackingProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<MinimalCommand, MapperWithUncreatableTransform>();

        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, new NullTransformerFactory());

        //act — the mapper is created, then building the transforms fails, so no pipeline is ever
        //constructed to take ownership of it
        Assert.Throws<ConfigurationException>(() => pipelineBuilder.BuildWrapPipeline<MinimalCommand>());

        //assert
        Assert.Equal(1, scopeTracker.DisposedCount);
    }

    [Fact]
    public void When_building_an_unwrap_pipeline_throws_should_release_the_mapper()
    {
        //arrange
        var scopeTracker = BuildScopeTracker(out var trackingProvider);
        using var mapperFactory = new ServiceProviderMapperFactory(trackingProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<MinimalCommand, MapperWithUncreatableTransform>();

        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, new NullTransformerFactory());

        //act
        Assert.Throws<ConfigurationException>(() => pipelineBuilder.BuildUnwrapPipeline<MinimalCommand>());

        //assert
        Assert.Equal(1, scopeTracker.DisposedCount);
    }

    [Fact]
    public void When_building_an_async_wrap_pipeline_throws_should_release_the_mapper()
    {
        //arrange
        var scopeTracker = BuildScopeTracker(out var trackingProvider, async: true);
        using var mapperFactory = new ServiceProviderMapperFactoryAsync(trackingProvider);
        var mapperRegistry = new MessageMapperRegistry(null, mapperFactory);
        mapperRegistry.RegisterAsync<MinimalCommand, AsyncMapperWithUncreatableTransform>();

        var pipelineBuilder = new TransformPipelineBuilderAsync(
            mapperRegistry, new NullTransformerFactoryAsync(), InstrumentationOptions.None);

        //act
        Assert.Throws<ConfigurationException>(() => pipelineBuilder.BuildWrapPipeline<MinimalCommand>());

        //assert
        Assert.Equal(1, scopeTracker.DisposedCount);
    }

    [Fact]
    public void When_building_an_async_unwrap_pipeline_throws_should_release_the_mapper()
    {
        //arrange
        var scopeTracker = BuildScopeTracker(out var trackingProvider, async: true);
        using var mapperFactory = new ServiceProviderMapperFactoryAsync(trackingProvider);
        var mapperRegistry = new MessageMapperRegistry(null, mapperFactory);
        mapperRegistry.RegisterAsync<MinimalCommand, AsyncMapperWithUncreatableTransform>();

        var pipelineBuilder = new TransformPipelineBuilderAsync(
            mapperRegistry, new NullTransformerFactoryAsync(), InstrumentationOptions.None);

        //act
        Assert.Throws<ConfigurationException>(() => pipelineBuilder.BuildUnwrapPipeline<MinimalCommand>());

        //assert
        Assert.Equal(1, scopeTracker.DisposedCount);
    }

    private static ScopeTracker BuildScopeTracker(out IServiceProvider trackingProvider, bool async = false)
    {
        var collection = new ServiceCollection();
        if (async)
            collection.AddTransient<AsyncMapperWithUncreatableTransform>();
        else
            collection.AddTransient<MapperWithUncreatableTransform>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);
        return scopeTracker;
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    // A transform the factory below deliberately refuses to create, so BuildTransformPipeline throws
    // after the mapper has already been resolved. Never instantiated — the factory returns null
    // before the type is used for anything but the exception message
    private sealed class UncreatableTransform;

    private sealed class UncreatableWrapWith(int step) : WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(UncreatableTransform);
    }

    private sealed class UncreatableUnwrapWith(int step) : UnwrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(UncreatableTransform);
    }

    private sealed class MapperWithUncreatableTransform : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }

        [UncreatableWrapWith(0)]
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();

        [UncreatableUnwrapWith(0)]
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }

    private sealed class AsyncMapperWithUncreatableTransform : IAmAMessageMapperAsync<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }

        [UncreatableWrapWith(0)]
        public Task<Message> MapToMessageAsync(MinimalCommand request, Publication publication,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        [UncreatableUnwrapWith(0)]
        public Task<MinimalCommand> MapToRequestAsync(Message message,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NullTransformerFactory : IAmAMessageTransformerFactory
    {
        public IAmAMessageTransform? Create(Type transformerType) => null;
        public void Release(IAmAMessageTransform transformer) { }
    }

    private sealed class NullTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public IAmAMessageTransformAsync? Create(Type transformerType) => null;
        public void Release(IAmAMessageTransformAsync transformer) { }
    }

    // Wraps the real IServiceScopeFactory and counts every scope disposal
    private sealed class ScopeTracker(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _disposedCount;

        public int DisposedCount => _disposedCount;

        public IServiceScope CreateScope() =>
            new TrackingScope(inner.CreateScope(), () => Interlocked.Increment(ref _disposedCount));

        private sealed class TrackingScope(IServiceScope inner, Action onDispose) : IServiceScope
        {
            public IServiceProvider ServiceProvider => inner.ServiceProvider;

            public void Dispose()
            {
                onDispose();
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

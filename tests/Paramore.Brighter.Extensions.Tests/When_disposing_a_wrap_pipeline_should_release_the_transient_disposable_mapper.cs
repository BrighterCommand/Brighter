using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransformPipelineMapperReleaseTests
{
    [Fact]
    public void When_disposing_a_wrap_pipeline_should_release_the_transient_disposable_mapper()
    {
        //arrange
        var disposals = new MapperDisposalLog();

        var collection = new ServiceCollection();
        collection.AddSingleton(disposals);
        collection.AddTransient<DisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        using var mapperFactory = new ServiceProviderMapperFactory(provider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<MinimalCommand, DisposableMapper>();

        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, new EmptyMessageTransformerFactory());

        //act
        using (pipelineBuilder.BuildWrapPipeline<MinimalCommand>())
        {
        }

        var disposalsAfterPipelineDisposed = disposals.Count;

        // A mapper still retained by the factory's per-instance service scope is disposed a
        // second time when the factory itself is disposed at shutdown.
        mapperFactory.Dispose();

        //assert
        Assert.Equal(1, disposalsAfterPipelineDisposed);
        Assert.Equal(1, disposals.Count);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    // A mapper that records its disposal against a shared log, so that the disposal of an
    // instance the test never holds a reference to is still observable
    private sealed class DisposableMapper : IAmAMessageMapper<MinimalCommand>, IDisposable
    {
        private readonly MapperDisposalLog _disposals;

        public DisposableMapper(MapperDisposalLog disposals) => _disposals = disposals;

        public IRequestContext? Context { get; set; }

        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();

        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();

        public void Dispose() => _disposals.Record();
    }

    private sealed class MapperDisposalLog
    {
        private int _count;

        public int Count => _count;

        public void Record() => Interlocked.Increment(ref _count);
    }
}

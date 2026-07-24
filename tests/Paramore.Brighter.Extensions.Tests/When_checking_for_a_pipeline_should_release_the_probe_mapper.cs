using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransformPipelineBuilderProbeReleaseTests
{
    [Fact]
    public void When_checking_for_a_pipeline_should_release_the_probe_mapper()
    {
        //arrange
        const int messageCount = 10;
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

        //act — HasPipeline creates a mapper only to answer the question; no pipeline ever owns it,
        //so nothing else can release it. The mediator asks this once per message.
        for (var i = 0; i < messageCount; i++)
            Assert.True(pipelineBuilder.HasPipeline<MinimalCommand>());

        //assert
        Assert.Equal(messageCount, disposals.Count);
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

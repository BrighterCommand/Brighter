using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransformPipelineBuilderHasPipelineForUnregisteredTypeTests
{
    [Fact]
    public void When_checking_for_a_pipeline_for_an_unregistered_type_it_should_return_false()
    {
        //arrange
        var collection = new ServiceCollection().AddLogging();
        collection.AddSingleton(new MapperDisposalLog());
        collection.AddTransient<DisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        using var mapperFactory = new ServiceProviderMapperFactory(provider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        //no Register call for MinimalCommand

        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, new EmptyMessageTransformerFactory(), loggerFactory: global::Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        //act + assert — no mapper registered and no default, so no pipeline
        Assert.False(pipelineBuilder.HasPipeline<MinimalCommand>());
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

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

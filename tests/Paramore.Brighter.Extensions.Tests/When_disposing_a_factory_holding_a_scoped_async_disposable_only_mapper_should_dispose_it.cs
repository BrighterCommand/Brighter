using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScopedAsyncDisposableMapperDisposalTests
{
    [Fact]
    public void When_disposing_a_factory_holding_a_scoped_async_disposable_only_mapper_should_dispose_it()
    {
        //arrange
        var disposals = new MapperDisposalLog();

        var collection = new ServiceCollection();
        collection.AddSingleton(disposals);
        collection.AddScoped<AsyncDisposableOnlyMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Scoped });
        var provider = collection.BuildServiceProvider();

        var mapperFactory = new ServiceProviderMapperFactory(provider);

        //act — the Scoped path keeps one long-lived scope for the factory rather than a scope per
        //instance, but it is disposed by the same Dispose and hits the same MS DI restriction: a scope
        //holding an IAsyncDisposable-only service cannot be disposed synchronously
        mapperFactory.Create(typeof(AsyncDisposableOnlyMapper));
        mapperFactory.Dispose();

        //assert
        Assert.Equal(1, disposals.Count);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    private sealed class AsyncDisposableOnlyMapper : IAmAMessageMapper<MinimalCommand>, IAsyncDisposable
    {
        private readonly MapperDisposalLog _disposals;

        public AsyncDisposableOnlyMapper(MapperDisposalLog disposals) => _disposals = disposals;

        public IRequestContext? Context { get; set; }

        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();

        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();

        public ValueTask DisposeAsync()
        {
            _disposals.Record();
            return default;
        }
    }

    private sealed class MapperDisposalLog
    {
        private int _count;

        public int Count => _count;

        public void Record() => Interlocked.Increment(ref _count);
    }
}

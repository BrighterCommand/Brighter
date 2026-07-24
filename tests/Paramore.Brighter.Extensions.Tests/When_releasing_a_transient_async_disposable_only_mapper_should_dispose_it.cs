using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransientAsyncDisposableMapperReleaseTests
{
    [Fact]
    public void When_releasing_a_transient_async_disposable_only_mapper_should_dispose_it()
    {
        //arrange
        var disposals = new MapperDisposalLog();
        using var mapperFactory = new ServiceProviderMapperFactory(BuildProvider(disposals));

        //act — the mapper implements IAsyncDisposable but *not* IDisposable, so the MS DI scope it
        //was resolved from refuses a synchronous Dispose(); neither Create nor Release may fall back
        //to IServiceScope.Dispose()
        var mapper = mapperFactory.Create(typeof(AsyncDisposableOnlyMapper));
        mapperFactory.Release(mapper!);

        //assert
        Assert.Equal(1, disposals.Count);
    }

    [Fact]
    public void When_disposing_a_factory_holding_a_transient_async_disposable_only_mapper_should_dispose_it()
    {
        //arrange
        var disposals = new MapperDisposalLog();
        var mapperFactory = new ServiceProviderMapperFactory(BuildProvider(disposals));

        //act — a mapper that is created but never released is drained at factory shutdown instead,
        //which is the second site that must not dispose the scope synchronously
        mapperFactory.Create(typeof(AsyncDisposableOnlyMapper));
        mapperFactory.Dispose();

        //assert
        Assert.Equal(1, disposals.Count);
    }

    private static IServiceProvider BuildProvider(MapperDisposalLog disposals)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton(disposals);
        collection.AddTransient<AsyncDisposableOnlyMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        return collection.BuildServiceProvider();
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    // A mapper that implements IAsyncDisposable and deliberately NOT IDisposable, recording its
    // disposal against a shared log so that an instance the test never holds is still observable
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

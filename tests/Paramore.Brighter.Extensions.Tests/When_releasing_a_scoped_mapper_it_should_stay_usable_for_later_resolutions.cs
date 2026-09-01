using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScopedMapperReleaseReuseTests
{
    [Fact]
    public void When_releasing_a_scoped_mapper_it_should_stay_usable_for_later_resolutions()
    {
        // Arrange — MapperLifetime.Scoped, a supported configuration. Under Scoped the factory shares one
        // long-lived IServiceScope and caches the resolved mapper, so every message reuses one instance
        // until the factory itself is disposed. Releasing a mapper per message must therefore be a no-op:
        // disposing the cached instance would hand message #2 a disposed mapper.
        var disposals = new DisposalLog();

        var collection = new ServiceCollection().AddLogging();
        collection.AddSingleton(disposals);
        collection.AddScoped<DisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Scoped });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderMapperFactory(provider);

        // Act — resolve, release (as a pipeline now does every message), then resolve again.
        var first = factory.Create(typeof(DisposableMapper));
        factory.Release(first!);
        var second = factory.Create(typeof(DisposableMapper));

        // Assert — the release did not dispose the cached scoped mapper, and the second resolution returns
        // the same live instance. A Scoped resolution carries a null release token, so releasing its lease
        // drains nothing.
        Assert.Equal(0, disposals.Count);
        Assert.Same(first!.Instance, second!.Instance);

        // The factory still owns the scoped instance and disposes it exactly once at shutdown.
        factory.Dispose();
        Assert.Equal(1, disposals.Count);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    private sealed class DisposableMapper : IAmAMessageMapper<MinimalCommand>, IDisposable
    {
        private readonly DisposalLog _disposals;

        public DisposableMapper(DisposalLog disposals) => _disposals = disposals;

        public IRequestContext? Context { get; set; }

        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();

        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();

        public void Dispose() => _disposals.Record();
    }

    private sealed class DisposalLog
    {
        private int _count;

        public int Count => _count;

        public void Record() => Interlocked.Increment(ref _count);
    }
}

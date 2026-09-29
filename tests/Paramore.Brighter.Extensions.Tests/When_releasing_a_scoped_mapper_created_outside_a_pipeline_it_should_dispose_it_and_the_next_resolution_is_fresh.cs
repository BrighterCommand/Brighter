using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;


namespace Paramore.Brighter.Extensions.Tests;

public class ScopedMapperDirectReleaseTests
{
    [Test]
    public async System.Threading.Tasks.Task When_releasing_a_scoped_mapper_created_outside_a_pipeline_it_should_dispose_it_and_the_next_resolution_is_fresh()
    {
        // Arrange — MapperLifetime.Scoped, called directly with no pipeline scope (T1.14, ADR 0070 step 9:
        // the factory-wide Scoped cache no longer serves a direct call — each resolution is isolated in
        // its own DI scope, reclaimed only when the caller releases it).
        var disposals = new DisposalLog();

        var collection = new ServiceCollection();
        collection.AddSingleton(disposals);
        collection.AddScoped<DisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Scoped });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderMapperFactory(provider);

        // Act — resolve, release, then resolve again.
        var first = factory.Create(typeof(DisposableMapper));
        factory.Release(first!);
        var second = factory.Create(typeof(DisposableMapper));

        // Assert — releasing the first isolated resolution disposed it immediately, and the second
        // resolution is a fresh, distinct instance rather than a cached one.
        await Assert.That(disposals.Count).IsEqualTo(1);
        await Assert.That(second!.Instance).IsNotSameReferenceAs(first!.Instance);

        // The factory disposes only what it still holds outstanding at shutdown — the released first
        // resolution is already gone, so disposing the factory only reclaims the still-live second.
        factory.Dispose();
        await Assert.That(disposals.Count).IsEqualTo(2);
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

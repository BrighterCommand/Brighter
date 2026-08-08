using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class SharedInstanceLeaseReleaseTests
{
    [Fact]
    public void When_releasing_one_lease_of_a_shared_mapper_the_other_resolution_stays_usable()
    {
        // Arrange — the mapper is registered as a SINGLETON, so the container returns the same reference for
        // every resolution, while MapperLifetime is Transient, so each Create opens its own IServiceScope.
        // Two resolutions therefore share one instance but hold two distinct scopes. Keying release on the
        // instance (the pre-redesign model) could not tell the two resolutions apart, so releasing one could
        // pop and dispose the other still-live resolution's scope — a use-after-dispose. Keying on the lease
        // fixes that: each Create returns its own lease over its own scope.
        var collection = new ServiceCollection();
        collection.AddSingleton<SharedMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);

        using var factory = new ServiceProviderMapperFactory(trackingProvider);

        // Act/Assert — two resolutions of the one shared instance, each with its own scope/lease
        var first = factory.Create(typeof(SharedMapper))!;
        var second = factory.Create(typeof(SharedMapper))!;
        Assert.Same(first.Instance, second.Instance);

        var firstScope = (TrackingScope)first.ReleaseToken!;
        var secondScope = (TrackingScope)second.ReleaseToken!;
        Assert.NotSame(firstScope, secondScope);

        // release the FIRST lease — it disposes exactly the first resolution's scope, and the second
        // resolution's scope stays live and usable (the exact use-after-dispose the old instance keying risked)
        factory.Release(first);
        Assert.True(firstScope.IsDisposed);
        Assert.False(secondScope.IsDisposed);
        Assert.Equal(1, scopeTracker.DisposedCount);

        // over-release the SAME (first) lease — an idempotent no-op, not a pop of the second's scope
        factory.Release(first);
        Assert.False(secondScope.IsDisposed);
        Assert.Equal(1, scopeTracker.DisposedCount);

        // releasing the second lease disposes exactly its own scope
        factory.Release(second);
        Assert.True(secondScope.IsDisposed);
        Assert.Equal(2, scopeTracker.DisposedCount);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    // Registered as a singleton: every resolution returns this same reference, so two transient resolutions
    // share one instance but have distinct scopes.
    private sealed class SharedMapper : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }

    // Wraps the real IServiceScopeFactory and hands out TrackingScopes so each resolution's scope can be
    // identified (it is the lease's release token) and its disposal observed.
    private sealed class ScopeTracker : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private int _disposedCount;

        public ScopeTracker(IServiceScopeFactory inner) => _inner = inner;

        public int DisposedCount => _disposedCount;

        public IServiceScope CreateScope() =>
            new TrackingScope(_inner.CreateScope(), () => Interlocked.Increment(ref _disposedCount));
    }

    internal sealed class TrackingScope : IServiceScope, IAsyncDisposable
    {
        private readonly IServiceScope _inner;
        private readonly Action _onDispose;
        private int _disposed;

        public TrackingScope(IServiceScope inner, Action onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public IServiceProvider ServiceProvider => _inner.ServiceProvider;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) _onDispose();
            _inner.Dispose();
        }

        //Production on net8+ disposes an IAsyncDisposable scope through DisposeAsync — real MS DI scopes have
        //implemented it since .NET Core 3.0 — so record the disposal in whichever shape the caller uses.
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) _onDispose();
            if (_inner is IAsyncDisposable asyncInner)
                await asyncInner.DisposeAsync().ConfigureAwait(false);
            else
                _inner.Dispose();
        }
    }

    private sealed class TrackingServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly ScopeTracker _scopeTracker;

        public TrackingServiceProvider(IServiceProvider inner, ScopeTracker scopeTracker)
        {
            _inner = inner;
            _scopeTracker = scopeTracker;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceScopeFactory) ? _scopeTracker : _inner.GetService(serviceType);
    }
}

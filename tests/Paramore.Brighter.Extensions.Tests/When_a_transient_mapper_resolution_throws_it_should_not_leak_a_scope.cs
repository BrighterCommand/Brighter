using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransientMapperResolutionThrowsScopeTests
{
    [Fact]
    public void When_a_transient_mapper_resolution_throws_it_should_not_leak_a_scope()
    {
        // Arrange — the mapper is registered but its constructor dependency is NOT, so the
        // container throws while activating it (the most common DI misconfiguration). This is the
        // failure path GetTransient creates a scope for before resolution succeeds.
        var collection = new ServiceCollection().AddLogging();
        collection.AddTransient<MapperWithUnregisteredDependency>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);

        using var factory = new ServiceProviderMapperFactory(trackingProvider);

        // Act — resolution throws because the constructor dependency is unregistered. The scope
        // created for this resolution is never handed back to the caller, so nothing but
        // GetTransient itself can dispose it.
        var thrown = Record.Exception(() => factory.Create(typeof(MapperWithUnregisteredDependency)));

        // Assert — resolution threw (the misconfiguration), the scope was created, and it was
        // disposed rather than orphaned. Before the fix the scope was created (CreatedCount == 1)
        // but neither tracked in _outstandingScopes (the TryAdd never ran) nor disposed
        // (DisposedCount == 0), so it leaked: Release has no key to find it and Dispose only drains
        // what was tracked.
        Assert.NotNull(thrown);
        Assert.Equal(1, scopeTracker.CreatedCount);
        Assert.Equal(scopeTracker.CreatedCount, scopeTracker.DisposedCount);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    // An unregistered service — nothing adds it to the container
    private interface IUnregisteredDependency { }

    // Registered mapper whose constructor asks for an unregistered service, so the container
    // throws InvalidOperationException while activating it
    private sealed class MapperWithUnregisteredDependency : IAmAMessageMapper<MinimalCommand>
    {
        public MapperWithUnregisteredDependency(IUnregisteredDependency dependency) { }
        public IRequestContext? Context { get; set; }
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }

    // Wraps the real IServiceScopeFactory and counts every scope created and disposed
    private sealed class ScopeTracker : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private int _createdCount;
        private int _disposedCount;

        public ScopeTracker(IServiceScopeFactory inner) => _inner = inner;

        public int CreatedCount => _createdCount;
        public int DisposedCount => _disposedCount;

        public IServiceScope CreateScope()
        {
            var scope = _inner.CreateScope();
            Interlocked.Increment(ref _createdCount);
            return new TrackingScope(scope, () => Interlocked.Increment(ref _disposedCount));
        }

        private sealed class TrackingScope : IServiceScope, IAsyncDisposable
        {
            private readonly IServiceScope _inner;
            private readonly Action _onDispose;
            private int _disposed;

            public TrackingScope(IServiceScope inner, Action onDispose)
            {
                _inner = inner;
                _onDispose = onDispose;
            }

            public IServiceProvider ServiceProvider => _inner.ServiceProvider;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) _onDispose();
                _inner.Dispose();
            }

            //Production on net8+ disposes an IAsyncDisposable scope through DisposeAsync — real MS DI scopes
            //have implemented it since .NET Core 3.0 — so DisposeScope takes the async branch, not
            //scope.Dispose(). Implement it here so the scope accounting runs against the branch a real app
            //takes; count the disposal in whichever shape the caller uses.
            public async ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) _onDispose();
                if (_inner is IAsyncDisposable asyncInner)
                    await asyncInner.DisposeAsync().ConfigureAwait(false);
                else
                    _inner.Dispose();
            }
        }
    }

    // Delegates all GetService calls to the root provider except IServiceScopeFactory,
    // which is redirected to our ScopeTracker so CreateScope() calls are intercepted
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

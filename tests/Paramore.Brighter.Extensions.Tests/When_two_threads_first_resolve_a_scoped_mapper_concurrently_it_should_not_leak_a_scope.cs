using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScopedMapperFirstResolutionRaceTests
{
    [Fact]
    public void When_two_threads_first_resolve_a_scoped_mapper_concurrently_it_should_not_leak_a_scope()
    {
        // Arrange
        var collection = new ServiceCollection();
        collection.AddScoped<NonDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Scoped });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new GatedScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);

        var factory = new ServiceProviderMapperFactory(trackingProvider);

        // Act — two threads race the FIRST scoped resolution. The tracker rendezvouses the first two
        // CreateScope() calls, so the thread that creates first is held inside CreateScope() — it has not
        // yet published its scope — while the second thread also observes the null _scope field and
        // creates. The non-atomic `_scope ??= CreateScope()` therefore has both create, and one
        // assignment overwrites the other. The overwritten (loser) scope is what must not be orphaned.
        void Resolve() => factory.Create(typeof(NonDisposableMapper));

        // dedicated threads (not the pool) so both are guaranteed to run at once and reach the null-check
        // together; the CreateScope() gate then holds the first inside creation until the second arrives
        var workers = new[] { new Thread(Resolve), new Thread(Resolve) };
        foreach (var worker in workers) worker.Start();
        foreach (var worker in workers) Assert.True(worker.Join(TimeSpan.FromSeconds(10)), "resolution deadlocked");

        // both threads created a scope under the race — the precondition the test is proving cleanup for
        Assert.Equal(2, scopeTracker.CreatedCount);

        // Assert — once the factory is disposed every scope it created has been disposed exactly as many
        // times as it was created. With the non-atomic assignment the loser scope is neither the retained
        // _scope nor in _transientScopes, so Dispose() never drains it and DisposedCount stays at 1.
        factory.Dispose();
        Assert.Equal(scopeTracker.CreatedCount, scopeTracker.DisposedCount);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    // Non-IDisposable mapper — current in-tree mappers are all non-disposable
    private sealed class NonDisposableMapper : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }

    // Wraps the real IServiceScopeFactory, counts every scope creation and disposal, and holds the first
    // two CreateScope() callers at a barrier so the first-resolution race is forced rather than hoped for.
    private sealed class GatedScopeTracker : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private readonly Barrier _firstResolutionGate = new(2);
        private int _arrivals;
        private int _createdCount;
        private int _disposedCount;

        public GatedScopeTracker(IServiceScopeFactory inner) => _inner = inner;

        public int CreatedCount => _createdCount;
        public int DisposedCount => _disposedCount;

        public IServiceScope CreateScope()
        {
            // rendezvous only the first two callers; a timeout (rather than an infinite wait) keeps a
            // future single-create path from hanging the test instead of failing it
            if (Interlocked.Increment(ref _arrivals) <= 2)
                _firstResolutionGate.SignalAndWait(TimeSpan.FromSeconds(10));

            Interlocked.Increment(ref _createdCount);
            var scope = _inner.CreateScope();
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
    // which is redirected to our GatedScopeTracker so CreateScope() calls are intercepted
    private sealed class TrackingServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly GatedScopeTracker _scopeTracker;

        public TrackingServiceProvider(IServiceProvider inner, GatedScopeTracker scopeTracker)
        {
            _inner = inner;
            _scopeTracker = scopeTracker;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceScopeFactory) ? _scopeTracker : _inner.GetService(serviceType);
    }
}

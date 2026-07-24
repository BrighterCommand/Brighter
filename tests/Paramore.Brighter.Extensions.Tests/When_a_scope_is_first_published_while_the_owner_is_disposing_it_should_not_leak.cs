using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScopedFirstResolutionVsDisposeRaceTests
{
    [Fact]
    public void When_a_scope_is_first_published_while_the_owner_is_disposing_it_should_not_leak()
    {
        // Arrange
        var collection = new ServiceCollection();
        collection.AddScoped<NonDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Scoped });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new HeldCreateScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);

        var factory = new ServiceProviderMapperFactory(trackingProvider);

        // Act — a worker begins the FIRST scoped resolution and is held inside CreateScope(): it has
        // passed the disposed-guard and created a scope, but has not yet published it to _scope. While it
        // is held there we Dispose() the factory — Dispose drains _scope (still null) and disposes nothing.
        // Only then do we release the worker, so its publish of _scope lands *after* Dispose has run. That
        // scope is what must not be orphaned.
        Exception? unexpected = null;
        var worker = new Thread(() =>
        {
            try
            {
                factory.Create(typeof(NonDisposableMapper));
            }
            catch (ObjectDisposedException)
            {
                //expected once fixed: the resolution lost the race to Dispose, so it reclaims the scope it
                //published and throws rather than handing back an instance from an owner that is gone
            }
            catch (Exception e)
            {
                unexpected = e;
            }
        });
        worker.Start();

        Assert.True(scopeTracker.EnteredCreate.Wait(TimeSpan.FromSeconds(10)), "worker never entered CreateScope");
        factory.Dispose();
        scopeTracker.ReleaseCreate.Set();

        Assert.True(worker.Join(TimeSpan.FromSeconds(10)), "worker deadlocked");
        Assert.Null(unexpected);

        // Assert — the scope created during the race is disposed exactly as many times as it was created.
        // Before the fix the publish landed after Dispose drained _scope, so nothing ever disposed it and
        // DisposedCount stayed at 0.
        Assert.Equal(1, scopeTracker.CreatedCount);
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

    // Wraps the real IServiceScopeFactory and holds only the first CreateScope() caller: it signals that it
    // is inside CreateScope (scope not yet published) and waits until the test has run Dispose(), forcing
    // the publish to land after disposal. Counts every scope creation and disposal.
    private sealed class HeldCreateScopeTracker : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        public readonly ManualResetEventSlim EnteredCreate = new(false);
        public readonly ManualResetEventSlim ReleaseCreate = new(false);
        private int _arrivals;
        private int _createdCount;
        private int _disposedCount;

        public HeldCreateScopeTracker(IServiceScopeFactory inner) => _inner = inner;

        public int CreatedCount => _createdCount;
        public int DisposedCount => _disposedCount;

        public IServiceScope CreateScope()
        {
            if (Interlocked.Increment(ref _arrivals) == 1)
            {
                EnteredCreate.Set();
                ReleaseCreate.Wait(TimeSpan.FromSeconds(10));
            }

            Interlocked.Increment(ref _createdCount);
            var scope = _inner.CreateScope();
            return new TrackingScope(scope, () => Interlocked.Increment(ref _disposedCount));
        }

        private sealed class TrackingScope : IServiceScope
        {
            private readonly IServiceScope _inner;
            private readonly Action _onDispose;

            public TrackingScope(IServiceScope inner, Action onDispose)
            {
                _inner = inner;
                _onDispose = onDispose;
            }

            public IServiceProvider ServiceProvider => _inner.ServiceProvider;

            public void Dispose()
            {
                _onDispose();
                _inner.Dispose();
            }
        }
    }

    // Delegates all GetService calls to the root provider except IServiceScopeFactory,
    // which is redirected to our tracker so CreateScope() calls are intercepted
    private sealed class TrackingServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly HeldCreateScopeTracker _scopeTracker;

        public TrackingServiceProvider(IServiceProvider inner, HeldCreateScopeTracker scopeTracker)
        {
            _inner = inner;
            _scopeTracker = scopeTracker;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceScopeFactory) ? _scopeTracker : _inner.GetService(serviceType);
    }
}

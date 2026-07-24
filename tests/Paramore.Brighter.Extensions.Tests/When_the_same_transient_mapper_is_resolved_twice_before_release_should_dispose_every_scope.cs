using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransientScopeKeyCollisionTests
{
    [Fact]
    public void When_the_same_transient_mapper_is_resolved_twice_before_release_should_dispose_every_scope()
    {
        // Arrange — the mapper is registered as a SINGLETON, so the container returns the same
        // reference every resolution, while MapperLifetime is configured Transient. Each Create
        // opens its own transient IServiceScope, but both scopes are keyed by the one shared
        // instance in the factory's reference-keyed tracking dictionary.
        var collection = new ServiceCollection();
        collection.AddSingleton<SharedMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);

        using var factory = new ServiceProviderMapperFactory(trackingProvider);

        // Act — two pipelines resolve the same mapper before either releases it, then each releases.
        // The second Create must not overwrite (and orphan) the scope opened by the first.
        var first = factory.Create(typeof(SharedMapper));
        var second = factory.Create(typeof(SharedMapper));

        factory.Release(first!);
        factory.Release(second!);

        // Assert — both resolutions returned the one shared instance (the key collision), and every
        // scope opened has been disposed: scopes created == scopes disposed, nothing orphaned.
        Assert.Same(first, second);
        Assert.Equal(scopeTracker.CreatedCount, scopeTracker.DisposedCount);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    // A mapper registered as a singleton: every resolution returns this same reference, so both
    // transient scopes collide on one key in the tracking dictionary.
    private sealed class SharedMapper : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }

    // Wraps the real IServiceScopeFactory and counts every scope created and every scope disposed.
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
    // which is redirected to our ScopeTracker so CreateScope() calls are intercepted.
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

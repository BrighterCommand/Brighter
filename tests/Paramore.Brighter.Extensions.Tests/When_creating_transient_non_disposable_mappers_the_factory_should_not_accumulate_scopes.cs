using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransientMapperScopeAccumulationTests
{
    [Fact]
    public void When_creating_transient_non_disposable_mappers_the_factory_should_not_accumulate_scopes()
    {
        // Arrange
        const int messageCount = 10;

        var collection = new ServiceCollection();
        collection.AddTransient<NonDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);

        using var factory = new ServiceProviderMapperFactory(trackingProvider);

        // Act — simulate N messages, releasing each mapper as the pipeline that owns it finishes.
        // Release is the signal that the instance is done with; it is what drains the scope.
        for (var i = 0; i < messageCount; i++)
        {
            var mapper = factory.Create(typeof(NonDisposableMapper));
            factory.Release(mapper!);
        }

        var disposedAfterCreates = scopeTracker.DisposedCount;

        // Assert — all N scopes disposed before factory.Dispose() is ever called, so nothing is
        // retained from one message to the next
        Assert.Equal(messageCount, disposedAfterCreates);
    }

    [Fact]
    public void When_creating_a_transient_mapper_the_scope_should_survive_until_release()
    {
        // Arrange
        var collection = new ServiceCollection();
        collection.AddTransient<NonDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);

        using var factory = new ServiceProviderMapperFactory(trackingProvider);

        // Act — the counterpart to the test above, and the reason the scope cannot simply be disposed
        // as soon as Create returns: an instance may still need services from the scope it came from
        var mapper = factory.Create(typeof(NonDisposableMapper));
        var disposedBeforeRelease = scopeTracker.DisposedCount;

        factory.Release(mapper!);

        // Assert
        Assert.Equal(0, disposedBeforeRelease);
        Assert.Equal(1, scopeTracker.DisposedCount);
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

    // Wraps the real IServiceScopeFactory and counts every scope disposal
    private sealed class ScopeTracker : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private int _disposedCount;

        public ScopeTracker(IServiceScopeFactory inner) => _inner = inner;

        public int DisposedCount => _disposedCount;

        public IServiceScope CreateScope()
        {
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

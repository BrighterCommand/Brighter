using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ReleaseScopeDisposalThrowRetentionTests
{
    [Fact]
    public void When_a_release_scope_disposal_throws_it_should_not_retain_the_instance()
    {
        // Arrange — a transient mapper resolved through a scope whose Dispose throws (as MS DI's sync scope
        // Dispose does for an IAsyncDisposable-only service). The scope is tracked against the instance.
        var collection = new ServiceCollection();
        collection.AddTransient<NonDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var trackingProvider = new TrackingServiceProvider(
            rootProvider, new ThrowingDisposeScopeFactory(rootProvider.GetRequiredService<IServiceScopeFactory>()));

        var factory = new ServiceProviderMapperFactory(trackingProvider);
        var mapper = factory.Create(typeof(NonDisposableMapper))!;

        // Act — releasing pops the scope and disposes it; the disposal throws. The bookkeeping that stops
        // retaining the instance as a key must still have completed before the disposal was attempted.
        Assert.Throws<InvalidOperationException>(() => factory.Release(mapper));

        // Assert — the instance is no longer tracked. Before the fix, CollectScopesToRelease was a lazy
        // iterator: the DisposeScope throw unwound the foreach before the key-removal step ran, so the
        // now-empty stack stayed keyed by the instance, retaining it until factory disposal.
        Assert.False(StillTracks(factory, mapper), "the released instance is still retained as a scope key");
    }

    private static bool StillTracks(ServiceProviderMapperFactory factory, object instance)
    {
        var lifetimeScope = factory.GetType()
            .GetField("_lifetimeScope", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(factory)!;
        var transientScopes = (IDictionary)lifetimeScope.GetType()
            .GetField("_transientScopes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(lifetimeScope)!;
        return transientScopes.Contains(instance);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    private sealed class NonDisposableMapper : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }

    // Returns scopes whose Dispose throws — the synchronous disposal failure the release path must tolerate
    private sealed class ThrowingDisposeScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;

        public ThrowingDisposeScopeFactory(IServiceScopeFactory inner) => _inner = inner;

        public IServiceScope CreateScope() => new ThrowingDisposeScope(_inner.CreateScope());

        private sealed class ThrowingDisposeScope : IServiceScope, IAsyncDisposable
        {
            private readonly IServiceScope _inner;

            public ThrowingDisposeScope(IServiceScope inner) => _inner = inner;

            public IServiceProvider ServiceProvider => _inner.ServiceProvider;

            public void Dispose()
            {
                _inner.Dispose();
                throw new InvalidOperationException("scope disposal failed");
            }

            //Production on net8+ disposes an IAsyncDisposable scope through DisposeAsync, so the disposal
            //failure the release path must tolerate has to surface here — not just from the synchronous
            //Dispose that only netstandard2.0 consumers reach
            public async ValueTask DisposeAsync()
            {
                if (_inner is IAsyncDisposable asyncInner)
                    await asyncInner.DisposeAsync().ConfigureAwait(false);
                else
                    _inner.Dispose();
                throw new InvalidOperationException("scope disposal failed");
            }
        }
    }

    private sealed class TrackingServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly IServiceScopeFactory _scopeFactory;

        public TrackingServiceProvider(IServiceProvider inner, IServiceScopeFactory scopeFactory)
        {
            _inner = inner;
            _scopeFactory = scopeFactory;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceScopeFactory) ? _scopeFactory : _inner.GetService(serviceType);
    }
}

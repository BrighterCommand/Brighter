using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class LifetimeScopeDisposalThrowDrainTests
{
    [Fact]
    public void When_disposing_and_a_scope_disposal_throws_should_still_dispose_remaining_scopes()
    {
        // Arrange — three transient mappers, each resolved through its own scope whose disposal throws (as
        // MS DI's scope Dispose does for an IAsyncDisposable-only service, and as a user Dispose may). Each
        // scope is tracked as a separate entry in _transientScopes, and none is released, so factory
        // disposal is the only thing that drains them.
        var disposalAttempts = new StrongBox<int>(0);

        var collection = new ServiceCollection();
        collection.AddTransient<NonDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var trackingProvider = new TrackingServiceProvider(
            rootProvider,
            new CountingThrowingScopeFactory(rootProvider.GetRequiredService<IServiceScopeFactory>(), disposalAttempts));

        var factory = new ServiceProviderMapperFactory(trackingProvider);
        for (var i = 0; i < 3; i++)
            factory.Create(typeof(NonDisposableMapper));

        // Act — disposing the factory drains all three tracked scopes; the first disposal throws. A drain
        // that skipped the rest would attempt only one and would propagate the throw out of Dispose.
        factory.Dispose();

        // Assert — every tracked scope's disposal was attempted (the throw did not skip the rest), Dispose
        // did not propagate the throw, and the tracking dictionary was cleared. Before the fix the drain
        // loop's DisposeScope was unguarded: the first throw unwound Dispose, leaving the other two scopes
        // undisposed and the dictionary un-cleared.
        Assert.Equal(3, disposalAttempts.Value);
        Assert.Empty(TransientScopes(factory));
    }

    private static IDictionary TransientScopes(ServiceProviderMapperFactory factory)
    {
        var lifetimeScope = factory.GetType()
            .GetField("_lifetimeScope", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(factory)!;
        return (IDictionary)lifetimeScope.GetType()
            .GetField("_transientScopes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(lifetimeScope)!;
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

    // Every scope it hands out counts its disposal and then throws — the multi-scope drain must attempt
    // every one of them despite each failing.
    private sealed class CountingThrowingScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private readonly StrongBox<int> _attempts;

        public CountingThrowingScopeFactory(IServiceScopeFactory inner, StrongBox<int> attempts)
        {
            _inner = inner;
            _attempts = attempts;
        }

        public IServiceScope CreateScope() => new CountingThrowingScope(_inner.CreateScope(), _attempts);

        private sealed class CountingThrowingScope : IServiceScope, IAsyncDisposable
        {
            private readonly IServiceScope _inner;
            private readonly StrongBox<int> _attempts;

            public CountingThrowingScope(IServiceScope inner, StrongBox<int> attempts)
            {
                _inner = inner;
                _attempts = attempts;
            }

            public IServiceProvider ServiceProvider => _inner.ServiceProvider;

            public void Dispose()
            {
                Interlocked.Increment(ref _attempts.Value);
                _inner.Dispose();
                throw new InvalidOperationException("scope disposal failed");
            }

            //Production on net8+ disposes an IAsyncDisposable scope through DisposeAsync, so the disposal
            //failure the drain must tolerate surfaces here, not only from the synchronous Dispose that only
            //netstandard2.0 consumers reach.
            public async ValueTask DisposeAsync()
            {
                Interlocked.Increment(ref _attempts.Value);
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

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Regression for PR #4254 review finding 6. The factory tracks every transient resolution's scope in a
/// dictionary and releases by that scope, and the whole release model depends on the key being the scope's own
/// <em>reference</em> identity. For MS DI's own scope the default comparer happens to be reference equality, but
/// that is a property of the implementation, not of <see cref="IServiceScope"/>: a custom scope (or a test double
/// declared as a <c>record</c>) overriding <c>Equals</c>/<c>GetHashCode</c> would let two distinct resolutions
/// collide on one key. The second <c>TryAdd</c> then silently fails, that scope is never tracked, and its release
/// finds nothing to remove — so it leaks, the exact hazard the lease design exists to eliminate. Tracking must be
/// keyed on reference identity so value-equal scopes stay distinct.
/// </summary>
public class TransientScopeReferenceIdentityTests
{
    [Fact]
    public void When_two_transient_scopes_are_value_equal_releasing_one_should_not_reclaim_the_other()
    {
        // Arrange — a SINGLETON mapper resolved under a Transient MapperLifetime, so each Create opens its own
        // scope over the one shared instance. The scope factory hands back scopes that are VALUE-equal (every
        // instance Equals every other, same hash) rather than reference-distinct.
        var collection = new ServiceCollection().AddLogging();
        collection.AddSingleton<SharedMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var rootProvider = collection.BuildServiceProvider();

        var scopeFactory = new CollidingScopeFactory(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var provider = new RedirectingServiceProvider(rootProvider, scopeFactory);

        using var factory = new ServiceProviderMapperFactory(provider);

        // Act — two resolutions before either releases, then release each lease.
        var first = factory.Create(typeof(SharedMapper));
        var second = factory.Create(typeof(SharedMapper));

        factory.Release(first!);
        factory.Release(second!);

        // Assert — both scopes opened must be disposed. Under a value-based comparer the second scope collides
        // with the first on its key, is never tracked, and its release reclaims nothing: created != disposed.
        Assert.Equal(2, scopeFactory.CreatedCount);
        Assert.Equal(scopeFactory.CreatedCount, scopeFactory.DisposedCount);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    private sealed class SharedMapper : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }

    // Hands back scopes that collide by value: every CollidingScope is equal to every other, with one shared
    // hash code, modelling a scope type that overrides Equals/GetHashCode (e.g. a record).
    private sealed class CollidingScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private int _createdCount;
        private int _disposedCount;

        public CollidingScopeFactory(IServiceScopeFactory inner) => _inner = inner;

        public int CreatedCount => _createdCount;
        public int DisposedCount => _disposedCount;

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _createdCount);
            return new CollidingScope(_inner.CreateScope(), () => Interlocked.Increment(ref _disposedCount));
        }

        private sealed class CollidingScope : IServiceScope, IAsyncDisposable
        {
            private readonly IServiceScope _inner;
            private readonly Action _onDispose;
            private int _disposed;

            public CollidingScope(IServiceScope inner, Action onDispose)
            {
                _inner = inner;
                _onDispose = onDispose;
            }

            public IServiceProvider ServiceProvider => _inner.ServiceProvider;

            // Value identity: any two colliding scopes are "equal" and share a hash, so a value-based
            // comparer folds them onto one dictionary key.
            public override bool Equals(object? obj) => obj is CollidingScope;
            public override int GetHashCode() => 0;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) _onDispose();
                _inner.Dispose();
            }

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

    private sealed class RedirectingServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly IServiceScopeFactory _scopeFactory;

        public RedirectingServiceProvider(IServiceProvider inner, IServiceScopeFactory scopeFactory)
        {
            _inner = inner;
            _scopeFactory = scopeFactory;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceScopeFactory) ? _scopeFactory : _inner.GetService(serviceType);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// Pins the two handler-lifetime scope semantics across a call chain that crosses more than one
// handler (a target handler and, e.g., its middleware), resolved under one IAmALifetime:
//  * Scoped   — the DI scope IS the call chain, so a scoped dependency is one shared instance
//               across every handler in the chain (the unit-of-work / transaction-provider pattern).
//  * Transient — each resolution gets its own independent DI scope, so a scoped dependency is a
//               distinct instance per handler (this is what closes the per-message scope leak: a
//               transient's scope is its own to release).
// Existing FactoryLifetimeTests only assert handler-instance identity, or resolve the same handler
// type twice; neither pins whether a scoped *dependency* is shared across two *different* handlers.
public class HandlerLifetimeCallChainScopeTests
{
    [Fact]
    public void When_two_handlers_share_a_lifetime_the_scoped_lifetime_shares_a_dependency()
    {
        //arrange
        var collection = new ServiceCollection().AddLogging();
        collection.AddScoped<ScopedDependency>();
        collection.AddTransient<FirstHandler>();
        collection.AddTransient<SecondHandler>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        //act — two different handlers resolved through the same lifetime (one call chain)
        var first = (FirstHandler)((IAmAHandlerFactorySync)factory).Create(typeof(FirstHandler), lifetime)!;
        var second = (SecondHandler)((IAmAHandlerFactorySync)factory).Create(typeof(SecondHandler), lifetime)!;

        //assert — the scope is the call chain, so the scoped dependency is the same instance for both
        Assert.Same(first.Dependency, second.Dependency);
    }

    [Fact]
    public void When_two_handlers_share_a_lifetime_the_transient_lifetime_isolates_a_dependency()
    {
        //arrange
        var collection = new ServiceCollection().AddLogging();
        collection.AddScoped<ScopedDependency>();
        collection.AddTransient<FirstHandler>();
        collection.AddTransient<SecondHandler>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        //act — two different handlers resolved through the same lifetime (one call chain)
        var first = (FirstHandler)((IAmAHandlerFactorySync)factory).Create(typeof(FirstHandler), lifetime)!;
        var second = (SecondHandler)((IAmAHandlerFactorySync)factory).Create(typeof(SecondHandler), lifetime)!;

        //assert — each transient resolution has its own independent scope, so the dependency differs
        Assert.NotSame(first.Dependency, second.Dependency);
    }

    [Fact]
    public void When_transient_handlers_opt_out_of_scope_isolation_they_share_a_dependency()
    {
        //arrange — Transient handlers, but IsolateHandlerScope turned off (the pre-#4254 model):
        //the pipeline shares one DI scope, so a scoped dependency is one instance across the chain
        var collection = new ServiceCollection().AddLogging();
        collection.AddScoped<ScopedDependency>();
        collection.AddTransient<FirstHandler>();
        collection.AddTransient<SecondHandler>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient,
            IsolateTransientHandlerScope = false
        });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        //act — two different handlers resolved through the same lifetime (one call chain)
        var first = (FirstHandler)((IAmAHandlerFactorySync)factory).Create(typeof(FirstHandler), lifetime)!;
        var second = (SecondHandler)((IAmAHandlerFactorySync)factory).Create(typeof(SecondHandler), lifetime)!;

        //assert — the flag reverts to a shared per-pipeline scope, so the dependency is the same instance
        Assert.Same(first.Dependency, second.Dependency);
    }

    [Theory]
    [InlineData(true)]   // new default — each transient handler isolated in its own scope
    [InlineData(false)]  // opt-out — the transient handlers share one per-pipeline scope
    public void When_a_transient_handler_pipeline_is_released_it_disposes_every_scope_it_created(bool isolate)
    {
        //arrange — count scopes created and disposed so we can tell "restored the old scoping" apart from
        //"restored the old leak": in either mode, releasing the pipeline must dispose exactly what it created
        var collection = new ServiceCollection().AddLogging();
        collection.AddScoped<ScopedDependency>();
        collection.AddTransient<FirstHandler>();
        collection.AddTransient<SecondHandler>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient,
            IsolateTransientHandlerScope = isolate
        });
        var rootProvider = collection.BuildServiceProvider();

        var tracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, tracker);

        var factory = new ServiceProviderHandlerFactory(trackingProvider);
        var lifetime = new TestLifetimeScope();

        var first = (FirstHandler)((IAmAHandlerFactorySync)factory).Create(typeof(FirstHandler), lifetime)!;
        var second = (SecondHandler)((IAmAHandlerFactorySync)factory).Create(typeof(SecondHandler), lifetime)!;

        //act — release the pipeline the way HandlerLifetimeScope.Dispose() does at end of message
        ((IAmAHandlerFactorySync)factory).Release(first, lifetime);
        ((IAmAHandlerFactorySync)factory).Release(second, lifetime);

        //assert — a scope was created (isolate=true makes one per handler, isolate=false shares one), and
        //release disposed every one of them, so the opt-out is a lifetime change and not a leak
        Assert.True(tracker.CreatedCount > 0);
        Assert.Equal(tracker.CreatedCount, tracker.DisposedCount);
    }

    private sealed class TestCommand : Command
    {
        public TestCommand() : base(Guid.NewGuid()) { }
    }

    private sealed class TestLifetimeScope : IAmALifetime
    {
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() { }
    }

    private sealed class ScopedDependency
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class FirstHandler : RequestHandler<TestCommand>
    {
        public ScopedDependency Dependency { get; }
        public FirstHandler(ScopedDependency dependency) => Dependency = dependency;
        public override TestCommand Handle(TestCommand command) => command;
    }

    private sealed class SecondHandler : RequestHandler<TestCommand>
    {
        public ScopedDependency Dependency { get; }
        public SecondHandler(ScopedDependency dependency) => Dependency = dependency;
        public override TestCommand Handle(TestCommand command) => command;
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

            //Production on net8+ disposes an IAsyncDisposable scope through DisposeAsync, so count the
            //disposal in whichever shape the caller uses.
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

    // Delegates every GetService call to the root provider except IServiceScopeFactory, which is
    // redirected to the ScopeTracker so every CreateScope() call is intercepted and counted
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

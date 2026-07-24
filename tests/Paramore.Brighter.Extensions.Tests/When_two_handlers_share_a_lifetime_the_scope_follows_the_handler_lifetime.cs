using System;
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
        var collection = new ServiceCollection();
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
        var collection = new ServiceCollection();
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
}

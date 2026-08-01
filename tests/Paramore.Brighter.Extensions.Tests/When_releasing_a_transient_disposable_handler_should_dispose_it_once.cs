using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class HandlerFactoryReleaseDisposalTests
{
    [Fact]
    public void When_releasing_a_transient_disposable_handler_should_dispose_it_once()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddTransient<DisposableHandler>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        //act — the container owns a transient handler through the scope it was resolved from, so
        //releasing must let that scope do the disposing rather than also disposing the handler directly
        var handler = (DisposableHandler)
            ((IAmAHandlerFactorySync)factory).Create(typeof(DisposableHandler), lifetime)!;
        factory.Release(handler, lifetime);

        //assert
        Assert.Equal(1, handler.DisposeCount);
    }

    [Fact]
    public void When_releasing_a_scoped_disposable_handler_should_dispose_it_once()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddScoped<DisposableHandler>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        //act — same for a scoped handler: the lifetime scope disposes it when it is released
        var handler = (DisposableHandler)
            ((IAmAHandlerFactorySync)factory).Create(typeof(DisposableHandler), lifetime)!;
        factory.Release(handler, lifetime);

        //assert
        Assert.Equal(1, handler.DisposeCount);
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

    // Counts disposals rather than latching a bool, so a second Dispose is visible
    private sealed class DisposableHandler : RequestHandler<TestCommand>, IDisposable
    {
        private int _disposeCount;

        public int DisposeCount => _disposeCount;

        public override TestCommand Handle(TestCommand command) => command;

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }
}

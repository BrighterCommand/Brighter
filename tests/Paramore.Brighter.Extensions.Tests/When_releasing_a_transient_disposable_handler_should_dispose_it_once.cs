using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class HandlerFactoryReleaseDisposalTests
{
    [Fact]
    public void When_disposing_the_pipeline_scope_a_transient_disposable_handler_should_be_disposed_once()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddTransient<DisposableHandler>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope(factory.CreatePipelineScope());

        //act — Release is now a no-op; a transient handler's own per-resolution scope is reclaimed only
        //when the pipeline scope itself is disposed, the way HandlerLifetimeScope.Dispose() does at end
        //of message
        var handler = (DisposableHandler)
            ((IAmAHandlerFactorySync)factory).Create(typeof(DisposableHandler), lifetime)!;
        factory.Release(handler, lifetime);
        lifetime.Dispose();

        //assert
        Assert.Equal(1, handler.DisposeCount);
    }

    [Fact]
    public void When_disposing_the_pipeline_scope_a_scoped_disposable_handler_should_be_disposed_once()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddScoped<DisposableHandler>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope(factory.CreatePipelineScope());

        //act — same for a scoped handler: Release is a no-op, the pipeline scope disposes it when the
        //pipeline scope itself is disposed
        var handler = (DisposableHandler)
            ((IAmAHandlerFactorySync)factory).Create(typeof(DisposableHandler), lifetime)!;
        factory.Release(handler, lifetime);
        lifetime.Dispose();

        //assert
        Assert.Equal(1, handler.DisposeCount);
    }

    private sealed class TestCommand : Command
    {
        public TestCommand() : base(Guid.NewGuid()) { }
    }

    private sealed class TestLifetimeScope : IAmALifetime
    {
        public TestLifetimeScope(IAmAScope? pipelineScope = null) => PipelineScope = pipelineScope;
        public IAmAScope? PipelineScope { get; }
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() => PipelineScope?.Dispose();
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

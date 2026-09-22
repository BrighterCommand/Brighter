using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransientHandlerCapturedProviderTests
{
    [Fact]
    public void When_a_transient_handler_captures_the_service_provider_should_resolve_after_create()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddTransient<IDependencyService, DependencyService>();
        collection.AddTransient<HandlerCapturingServiceProvider>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        //act — the handler is not IDisposable and resolves its collaborator lazily from the injected
        //IServiceProvider, exactly as FluentValidationRequestHandler does. That provider belongs to the
        //scope the handler was resolved from, so the scope has to outlive Create and stay usable until
        //the handler is released.
        var handler = (HandlerCapturingServiceProvider)
            ((IAmAHandlerFactorySync)factory).Create(typeof(HandlerCapturingServiceProvider), lifetime)!;

        //assert
        Assert.NotNull(handler.ResolveDependency());
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

    private interface IDependencyService;

    private sealed class DependencyService : IDependencyService;

    // Deliberately NOT IDisposable, and deliberately resolving from the captured provider *after*
    // construction rather than taking the dependency directly — this is the shape that breaks
    // when the resolving scope is disposed eagerly
    private sealed class HandlerCapturingServiceProvider : RequestHandler<TestCommand>
    {
        private readonly IServiceProvider _serviceProvider;

        public HandlerCapturingServiceProvider(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public IDependencyService? ResolveDependency() =>
            (IDependencyService?)_serviceProvider.GetService(typeof(IDependencyService));

        public override TestCommand Handle(TestCommand command) => command;
    }
}

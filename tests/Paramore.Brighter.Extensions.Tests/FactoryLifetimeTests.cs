#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.Tests;

public class FactoryLifetimeTests
{
    [Test]
    public async Task Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);
        var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);

        // Assert - Same scope should return same instance
        await Assert.That(handler2).IsSameReferenceAs(handler1);
    }

    [Test]
    public async Task Factory_WithScopedLifetime_ReturnsDifferentInstancesAcrossScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime1 = new TestLifetimeScope();
        var lifetime2 = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime1);
        var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime2);

        // Assert - Different scopes should return different instances
        await Assert.That(handler2).IsNotSameReferenceAs(handler1);
    }

    [Test]
    public async Task Factory_WithTransientLifetime_ReturnsDifferentInstancesEachTime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);
        var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);

        // Assert - Transient should return new instance each time
        await Assert.That(handler2).IsNotSameReferenceAs(handler1);
    }

    [Test]
    public async Task Factory_WithSingletonLifetime_ReturnsSameInstanceAcrossScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<TestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Singleton
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime1 = new TestLifetimeScope();
        var lifetime2 = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime1);
        var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime2);

        // Assert - Singleton should return same instance regardless of scope
        await Assert.That(handler2).IsSameReferenceAs(handler1);
    }

    [Test]
    public async Task AsyncFactory_WithSingletonLifetime_ReturnsSameInstanceAcrossScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<TestAsyncHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Singleton
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime1 = new TestLifetimeScope();
        var lifetime2 = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime1);
        var handler2 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime2);

        // Assert - Singleton should return same instance regardless of scope
        await Assert.That(handler2).IsSameReferenceAs(handler1);
    }

    [Test]
    public async Task AsyncFactory_WithScopedLifetime_ReturnsSameInstanceWithinScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestAsyncHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime);
        var handler2 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime);

        // Assert
        await Assert.That(handler2).IsSameReferenceAs(handler1);
    }

    [Test]
    public async Task AsyncFactory_WithScopedLifetime_ReturnsDifferentInstancesAcrossScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestAsyncHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime1 = new TestLifetimeScope();
        var lifetime2 = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime1);
        var handler2 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime2);

        // Assert
        await Assert.That(handler2).IsNotSameReferenceAs(handler1);
    }

    [Test]
    public async Task AsyncFactory_WithTransientLifetime_ReturnsDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestAsyncHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime);
        var handler2 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime);

        // Assert
        await Assert.That(handler2).IsNotSameReferenceAs(handler1);
    }

    [Test]
    public async Task Factory_HandlerWithDependency_ResolvesBothCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDependencyService, DependencyService>();
        services.AddTransient<HandlerWithDependency>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act
        var handler = (HandlerWithDependency)((IAmAHandlerFactorySync)factory).Create(typeof(HandlerWithDependency), lifetime)!;

        // Assert
        await Assert.That(handler).IsNotNull();
        await Assert.That(handler.Dependency).IsNotNull();
    }

    [Test]
    public async Task Factory_Release_ClearsHandlerFromCache()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act - Create handler, then release, then create again
        var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);
        ((IAmAHandlerFactorySync)factory).Release(handler1, lifetime);
        var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);

        // Assert - After release, we should get a new handler instance
        await Assert.That(handler2).IsNotNull();
        await Assert.That(handler2).IsNotSameReferenceAs(handler1);
    }

    [Test]
    public async Task Factory_WithScopedLifetime_TracksDisposableHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<DisposableTestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act
        var handler = (DisposableTestHandler)((IAmAHandlerFactorySync)factory).Create(typeof(DisposableTestHandler), lifetime)!;

        // Assert - Handler should be created and not disposed initially
        await Assert.That(handler).IsNotNull();
        await Assert.That(handler.IsDisposed).IsFalse();
    }

    private class TestHandler : RequestHandler<TestCommand>
    {
        public override TestCommand Handle(TestCommand command) => command;
    }

    private class TestAsyncHandler : RequestHandlerAsync<TestCommand>
    {
        public override Task<TestCommand> HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(command);
    }

    private class TestCommand : Command
    {
        public TestCommand() : base(Guid.NewGuid()) { }
    }

    private class TestLifetimeScope : IAmALifetime
    {
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() { }
    }

    private interface IDependencyService { }

    private class DependencyService : IDependencyService { }

    private class HandlerWithDependency : RequestHandler<TestCommand>
    {
        public IDependencyService Dependency { get; }

        public HandlerWithDependency(IDependencyService dependency)
        {
            Dependency = dependency;
        }

        public override TestCommand Handle(TestCommand command) => command;
    }

    private class DisposableTestHandler : RequestHandler<TestCommand>, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public override TestCommand Handle(TestCommand command) => command;

        public void Dispose() => IsDisposed = true;
    }
}

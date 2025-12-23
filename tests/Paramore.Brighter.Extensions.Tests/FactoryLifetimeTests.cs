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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class FactoryLifetimeTests
{
    [Fact]
    public void Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope()
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
        Assert.Same(handler1, handler2);
    }

    [Fact]
    public void Factory_WithScopedLifetime_ReturnsDifferentInstancesAcrossScopes()
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
        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public void Factory_WithTransientLifetime_ReturnsDifferentInstancesEachTime()
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
        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public void Factory_WithSingletonLifetime_ReturnsSameInstanceAcrossScopes()
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
        Assert.Same(handler1, handler2);
    }

    [Fact]
    public void AsyncFactory_WithSingletonLifetime_ReturnsSameInstanceAcrossScopes()
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
        Assert.Same(handler1, handler2);
    }

    [Fact]
    public void AsyncFactory_WithScopedLifetime_ReturnsSameInstanceWithinScope()
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
        Assert.Same(handler1, handler2);
    }

    [Fact]
    public void AsyncFactory_WithScopedLifetime_ReturnsDifferentInstancesAcrossScopes()
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
        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public void AsyncFactory_WithTransientLifetime_ReturnsDifferentInstances()
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
        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public void Factory_HandlerWithDependency_ResolvesBothCorrectly()
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
        Assert.NotNull(handler);
        Assert.NotNull(handler.Dependency);
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
}

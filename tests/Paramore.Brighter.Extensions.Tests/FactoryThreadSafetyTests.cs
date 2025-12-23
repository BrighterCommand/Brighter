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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Tests that verify thread safety of the ServiceProviderHandlerFactory.
/// </summary>
public class FactoryThreadSafetyTests
{
    [Fact]
    public async Task ConcurrentSingletonResolution_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ThreadSafetyTestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Singleton
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var handlers = new ConcurrentBag<IHandleRequests>();

        // Act - Resolve singleton from multiple threads simultaneously
        var tasks = new Task[100];
        for (int i = 0; i < 100; i++)
        {
            var lifetime = new TestLifetimeScope();
            tasks[i] = Task.Run(() =>
            {
                var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(ThreadSafetyTestHandler), lifetime);
                handlers.Add(handler!);
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All should be the same instance
        var distinctHandlers = handlers.Distinct().ToList();
        Assert.Single(distinctHandlers);
    }

    [Fact]
    public async Task ConcurrentScopedResolution_SameScopeReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ThreadSafetyTestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var sharedLifetime = new TestLifetimeScope();
        var handlers = new ConcurrentBag<IHandleRequests>();

        // Act - Resolve scoped from multiple threads with SAME lifetime
        var tasks = new Task[50];
        for (int i = 0; i < 50; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(ThreadSafetyTestHandler), sharedLifetime);
                handlers.Add(handler!);
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All should be the same instance (same scope)
        var distinctHandlers = handlers.Distinct().ToList();
        Assert.Single(distinctHandlers);
    }

    [Fact]
    public async Task ConcurrentTransientResolution_ReturnsDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ThreadSafetyTestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var handlers = new ConcurrentBag<IHandleRequests>();

        // Act - Resolve transient from multiple threads
        var tasks = new Task[50];
        for (int i = 0; i < 50; i++)
        {
            var lifetime = new TestLifetimeScope();
            tasks[i] = Task.Run(() =>
            {
                var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(ThreadSafetyTestHandler), lifetime);
                handlers.Add(handler!);
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All should be different instances
        var distinctHandlers = handlers.Distinct().ToList();
        Assert.Equal(50, distinctHandlers.Count);
    }

    [Fact]
    public async Task ConcurrentSingletonResolution_OnlyCreatesOneInstance()
    {
        // Arrange - Use a handler that tracks instantiation count
        CountingHandler.ResetCount();

        var services = new ServiceCollection();
        services.AddTransient<CountingHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Singleton
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var handlers = new ConcurrentBag<IHandleRequests>();

        // Act - Resolve singleton from multiple threads simultaneously
        var barrier = new Barrier(100);
        var tasks = new Task[100];
        for (int i = 0; i < 100; i++)
        {
            var lifetime = new TestLifetimeScope();
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait(); // Maximize contention
                var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(CountingHandler), lifetime);
                handlers.Add(handler!);
            });
        }

        await Task.WhenAll(tasks);

        // Assert - Only ONE instance should have been created
        Assert.Equal(1, CountingHandler.InstanceCount);
        Assert.Single(handlers.Distinct());
    }

    private class ThreadSafetyTestHandler : RequestHandler<ThreadSafetyTestCommand>
    {
        public override ThreadSafetyTestCommand Handle(ThreadSafetyTestCommand command) => command;
    }

    private class ThreadSafetyTestCommand : Command
    {
        public ThreadSafetyTestCommand() : base(Guid.NewGuid()) { }
    }

    private class CountingHandler : RequestHandler<CountingCommand>
    {
        private static int _instanceCount;
        public static int InstanceCount => _instanceCount;
        public static void ResetCount() => _instanceCount = 0;

        public CountingHandler() => Interlocked.Increment(ref _instanceCount);
        public override CountingCommand Handle(CountingCommand command) => command;
    }

    private class CountingCommand : Command
    {
        public CountingCommand() : base(Guid.NewGuid()) { }
    }

    private class TestLifetimeScope : IAmALifetime
    {
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() { }
    }
}

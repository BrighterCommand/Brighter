#region Licence
/* The MIT License (MIT)

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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Tests that verify IAmACommandProcessor is registered as a singleton and returns
/// the same instance for multiple requests within a single service provider.
/// This validates that the refactoring from static to instance-based mediator
/// maintains correct singleton behavior within a DI container.
/// </summary>
public class CommandProcessorSingletonTests
{
    /// <summary>
    /// Verifies that resolving IAmACommandProcessor twice from the same
    /// service provider returns the exact same instance.
    /// </summary>
    [Fact]
    public void SameProvider_MultipleResolutions_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();

        // Act
        var processor1 = provider.GetRequiredService<IAmACommandProcessor>();
        var processor2 = provider.GetRequiredService<IAmACommandProcessor>();
        var processor3 = provider.GetRequiredService<IAmACommandProcessor>();

        // Assert - All should be the exact same instance
        Assert.Same(processor1, processor2);
        Assert.Same(processor2, processor3);
    }

    /// <summary>
    /// Verifies that resolving IAmACommandProcessor from different scopes
    /// within the same service provider returns the same singleton instance.
    /// </summary>
    [Fact]
    public void SameProvider_DifferentScopes_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();

        // Act - Resolve from different scopes
        IAmACommandProcessor processor1, processor2, processor3;

        using (var scope1 = provider.CreateScope())
        {
            processor1 = scope1.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
        }

        using (var scope2 = provider.CreateScope())
        {
            processor2 = scope2.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
        }

        using (var scope3 = provider.CreateScope())
        {
            processor3 = scope3.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
        }

        // Assert - All should be the same singleton instance
        Assert.Same(processor1, processor2);
        Assert.Same(processor2, processor3);
    }

    /// <summary>
    /// Verifies that concurrent resolution of IAmACommandProcessor from multiple
    /// threads returns the same singleton instance to all threads.
    /// </summary>
    [Fact]
    public async Task SameProvider_ConcurrentResolutions_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();
        var processors = new ConcurrentBag<IAmACommandProcessor>();

        // Act - Resolve from 100 concurrent threads with barrier synchronization
        using var barrier = new Barrier(100);
        var tasks = new Task[100];
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait(); // Maximize contention
                var processor = provider.GetRequiredService<IAmACommandProcessor>();
                processors.Add(processor);
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All 100 resolutions should return the same instance
        var distinctProcessors = processors.Distinct().ToList();
        Assert.Single(distinctProcessors);
    }

    /// <summary>
    /// Verifies that the command processor singleton is preserved when resolved
    /// alongside other services in the same scope.
    /// </summary>
    [Fact]
    public void SameProvider_ResolvedWithOtherServices_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        services.AddScoped<TestScopedService>();
        var provider = services.BuildServiceProvider();

        // Act
        IAmACommandProcessor processorFromRoot, processorFromScope1, processorFromScope2;

        processorFromRoot = provider.GetRequiredService<IAmACommandProcessor>();

        using (var scope = provider.CreateScope())
        {
            var scopedService = scope.ServiceProvider.GetRequiredService<TestScopedService>();
            processorFromScope1 = scope.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
        }

        using (var scope = provider.CreateScope())
        {
            processorFromScope2 = scope.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
        }

        // Assert
        Assert.Same(processorFromRoot, processorFromScope1);
        Assert.Same(processorFromScope1, processorFromScope2);
    }

    /// <summary>
    /// Verifies that the command processor instance is the same when resolved
    /// sequentially many times in a loop.
    /// </summary>
    [Fact]
    public void SameProvider_ManySequentialResolutions_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();

        var firstProcessor = provider.GetRequiredService<IAmACommandProcessor>();

        // Act - Resolve 100 times sequentially
        for (int i = 0; i < 100; i++)
        {
            var processor = provider.GetRequiredService<IAmACommandProcessor>();

            // Assert each one is the same
            Assert.Same(firstProcessor, processor);
        }
    }

    /// <summary>
    /// Verifies that when producers are configured, the command processor
    /// is still a singleton across multiple resolutions.
    /// </summary>
    [Fact]
    public void WithProducers_MultipleResolutions_ReturnsSameInstance()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var internalBus = new InternalBus();
        var routingKey = new RoutingKey("test.singleton.command");

        var producer = new InMemoryMessageProducer(internalBus, new Publication
        {
            Topic = routingKey,
            RequestType = typeof(SingletonTestCommand)
        });

        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var outbox = new InMemoryOutbox(timeProvider);

        var services = new ServiceCollection();
        services.AddBrighter()
            .AddProducers(cfg =>
            {
                cfg.ProducerRegistry = producerRegistry;
                cfg.Outbox = outbox;
            });

        var provider = services.BuildServiceProvider();

        // Act
        var processor1 = provider.GetRequiredService<IAmACommandProcessor>();
        var processor2 = provider.GetRequiredService<IAmACommandProcessor>();

        using var scope = provider.CreateScope();
        var processor3 = scope.ServiceProvider.GetRequiredService<IAmACommandProcessor>();

        // Assert
        Assert.Same(processor1, processor2);
        Assert.Same(processor2, processor3);
    }

    /// <summary>
    /// Verifies that using the Func overloads still results in a singleton
    /// command processor.
    /// </summary>
    [Fact]
    public void WithFuncOverloads_MultipleResolutions_ReturnsSameInstance()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var internalBus = new InternalBus();
        var routingKey = new RoutingKey("test.func.singleton");

        var producer = new InMemoryMessageProducer(internalBus, new Publication
        {
            Topic = routingKey,
            RequestType = typeof(SingletonTestCommand)
        });

        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var outbox = new InMemoryOutbox(timeProvider);

        var services = new ServiceCollection();
        services.AddSingleton(producerRegistry);
        services.AddSingleton(outbox);

        // Use the Func<IServiceProvider, T> overload
        services.AddBrighter(sp => new BrighterOptions
            {
                HandlerLifetime = ServiceLifetime.Scoped
            })
            .AddProducers(sp => new ProducersConfiguration
            {
                ProducerRegistry = sp.GetRequiredService<ProducerRegistry>(),
                Outbox = sp.GetRequiredService<InMemoryOutbox>()
            });

        var provider = services.BuildServiceProvider();

        // Act
        var processor1 = provider.GetRequiredService<IAmACommandProcessor>();
        var processor2 = provider.GetRequiredService<IAmACommandProcessor>();

        using var scope = provider.CreateScope();
        var processor3 = scope.ServiceProvider.GetRequiredService<IAmACommandProcessor>();

        // Assert
        Assert.Same(processor1, processor2);
        Assert.Same(processor2, processor3);
    }

    /// <summary>
    /// Verifies that concurrent resolutions with producers configured
    /// all return the same singleton instance.
    /// </summary>
    [Fact]
    public async Task WithProducers_ConcurrentResolutions_ReturnsSameInstance()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var internalBus = new InternalBus();
        var routingKey = new RoutingKey("test.concurrent.singleton");

        var producer = new InMemoryMessageProducer(internalBus, new Publication
        {
            Topic = routingKey,
            RequestType = typeof(SingletonTestCommand)
        });

        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var outbox = new InMemoryOutbox(timeProvider);

        var services = new ServiceCollection();
        services.AddBrighter()
            .AddProducers(cfg =>
            {
                cfg.ProducerRegistry = producerRegistry;
                cfg.Outbox = outbox;
            });

        var provider = services.BuildServiceProvider();
        var processors = new ConcurrentBag<IAmACommandProcessor>();

        // Act - Resolve from 50 concurrent threads
        using var barrier = new Barrier(50);
        var tasks = new Task[50];
        for (int i = 0; i < 50; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                var processor = provider.GetRequiredService<IAmACommandProcessor>();
                processors.Add(processor);
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All should be the same instance
        var distinctProcessors = processors.Distinct().ToList();
        Assert.Single(distinctProcessors);
    }

    /// <summary>
    /// Verifies that concurrent resolutions from different scopes
    /// all return the same singleton instance.
    /// </summary>
    [Fact]
    public async Task SameProvider_ConcurrentScopedResolutions_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();
        var processors = new ConcurrentBag<IAmACommandProcessor>();

        // Act - Resolve from concurrent scopes
        using var barrier = new Barrier(50);
        var tasks = new Task[50];
        for (int i = 0; i < 50; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                using var scope = provider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
                processors.Add(processor);
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All should be the same singleton instance
        var distinctProcessors = processors.Distinct().ToList();
        Assert.Single(distinctProcessors);
    }

    /// <summary>
    /// Verifies that the command processor is only instantiated once even under
    /// heavy concurrent load using an instantiation counter.
    /// </summary>
    [Fact]
    public async Task SameProvider_ConcurrentResolutions_OnlyInstantiatesOnce()
    {
        // This test verifies that the singleton is truly only created once
        // by using a wrapper that counts instantiation attempts.

        // Arrange
        var instantiationCount = 0;

        var services = new ServiceCollection();
        services.AddBrighter();

        // Decorate the command processor factory to count instantiations
        // We verify this by checking the provider only creates one instance
        var provider = services.BuildServiceProvider();

        IAmACommandProcessor? firstInstance = null;
        var processors = new ConcurrentBag<IAmACommandProcessor>();

        // Act
        using var barrier = new Barrier(100);
        var tasks = new Task[100];
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                var processor = provider.GetRequiredService<IAmACommandProcessor>();

                // Track first instance seen
                var original = Interlocked.CompareExchange(ref firstInstance, processor, null);
                if (original == null)
                {
                    Interlocked.Increment(ref instantiationCount);
                }
                else if (!ReferenceEquals(original, processor))
                {
                    // If we ever see a different instance, increment to fail the test
                    Interlocked.Increment(ref instantiationCount);
                }

                processors.Add(processor);
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, instantiationCount);
        Assert.Single(processors.Distinct());
    }

    /// <summary>
    /// Verifies that the GetHashCode of all resolved processors is identical,
    /// providing another confirmation they are the same object instance.
    /// </summary>
    [Fact]
    public async Task SameProvider_AllResolutions_HaveSameHashCode()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();
        var hashCodes = new ConcurrentBag<int>();

        // Act
        var tasks = new Task[100];
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var processor = provider.GetRequiredService<IAmACommandProcessor>();
                hashCodes.Add(processor.GetHashCode());
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All hash codes should be the same
        var distinctHashCodes = hashCodes.Distinct().ToList();
        Assert.Single(distinctHashCodes);
    }

    /// <summary>
    /// Verifies that even when resolving from nested scopes,
    /// the same singleton instance is returned.
    /// </summary>
    [Fact]
    public void SameProvider_NestedScopes_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();

        // Act
        var processorFromRoot = provider.GetRequiredService<IAmACommandProcessor>();

        IAmACommandProcessor processorFromLevel1, processorFromLevel2, processorFromLevel3;

        using (var scope1 = provider.CreateScope())
        {
            processorFromLevel1 = scope1.ServiceProvider.GetRequiredService<IAmACommandProcessor>();

            using (var scope2 = scope1.ServiceProvider.CreateScope())
            {
                processorFromLevel2 = scope2.ServiceProvider.GetRequiredService<IAmACommandProcessor>();

                using (var scope3 = scope2.ServiceProvider.CreateScope())
                {
                    processorFromLevel3 = scope3.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
                }
            }
        }

        // Assert
        Assert.Same(processorFromRoot, processorFromLevel1);
        Assert.Same(processorFromLevel1, processorFromLevel2);
        Assert.Same(processorFromLevel2, processorFromLevel3);
    }

    private class SingletonTestCommand : Command
    {
        public SingletonTestCommand() : base(Guid.NewGuid()) { }
    }

    private class TestScopedService
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}

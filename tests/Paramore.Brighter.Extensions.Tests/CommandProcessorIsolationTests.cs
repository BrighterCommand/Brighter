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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.JsonConverters;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Tests that verify CommandProcessor instances are properly isolated,
/// enabling parallel test execution without [Collection] serialization.
/// </summary>
public class CommandProcessorIsolationTests
{
    [Fact]
    public void TwoCommandProcessors_HaveIsolatedState()
    {
        // Arrange - Create two independent service providers
        var services1 = new ServiceCollection();
        services1.AddBrighter();
        var provider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddBrighter();
        var provider2 = services2.BuildServiceProvider();

        // Act
        var cp1 = provider1.GetRequiredService<IAmACommandProcessor>();
        var cp2 = provider2.GetRequiredService<IAmACommandProcessor>();

        // Assert - They should be different instances
        Assert.NotSame(cp1, cp2);
    }

    [Fact]
    public async Task ParallelTests_DoNotInterfere()
    {
        // This test demonstrates that parallel execution is safe
        var tasks = new Task[10];
        var processors = new IAmACommandProcessor[10];

        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                var services = new ServiceCollection();
                services.AddBrighter();
                var provider = services.BuildServiceProvider();
                processors[index] = provider.GetRequiredService<IAmACommandProcessor>();
            });
        }

        await Task.WhenAll(tasks);

        // All processors should be unique instances
        for (int i = 0; i < 10; i++)
        {
            for (int j = i + 1; j < 10; j++)
            {
                Assert.NotSame(processors[i], processors[j]);
            }
        }
    }

    /// <summary>
    /// This test simulates the real-world scenario that was broken before the fix:
    /// Multiple test classes creating full Brighter setups with producers and outboxes in parallel.
    /// Previously, the static singleton pattern caused all tests to share the same outbox.
    /// </summary>
    [Fact]
    public async Task ParallelBrighterSetups_WithProducers_HaveIsolatedOutboxes()
    {
        // Arrange - simulate 5 parallel test setups
        const int parallelTests = 5;
        var outboxes = new InMemoryOutbox[parallelTests];
        var commandProcessors = new IAmACommandProcessor[parallelTests];
        var messageIds = new string[parallelTests];

        var tasks = new Task[parallelTests];
        for (int i = 0; i < parallelTests; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                // Each "test" creates its own complete Brighter setup
                var timeProvider = new FakeTimeProvider();
                var routingKey = new RoutingKey($"test.command.{index}");
                var internalBus = new InternalBus();

                var producer = new InMemoryMessageProducer(internalBus, timeProvider, new Publication
                {
                    Topic = routingKey,
                    RequestType = typeof(IsolationTestCommand)
                });

                var producerRegistry = new ProducerRegistry(
                    new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

                // Each test has its OWN outbox - this is the key isolation requirement
                outboxes[index] = new InMemoryOutbox(timeProvider);

                var services = new ServiceCollection();
                services.AddBrighter()
                    .AddProducers(cfg =>
                    {
                        cfg.ProducerRegistry = producerRegistry;
                        cfg.Outbox = outboxes[index];
                    });

                var provider = services.BuildServiceProvider();
                commandProcessors[index] = provider.GetRequiredService<IAmACommandProcessor>();

                // Each test deposits a unique message
                messageIds[index] = $"msg-{index}-{Guid.NewGuid()}";
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All command processors should be different instances
        for (int i = 0; i < parallelTests; i++)
        {
            for (int j = i + 1; j < parallelTests; j++)
            {
                Assert.NotSame(commandProcessors[i], commandProcessors[j]);
            }
        }

        // Assert - All outboxes should be different instances (the original bug was shared outboxes)
        for (int i = 0; i < parallelTests; i++)
        {
            for (int j = i + 1; j < parallelTests; j++)
            {
                Assert.NotSame(outboxes[i], outboxes[j]);
            }
        }
    }

    /// <summary>
    /// Simulates multiple test classes running in parallel, each with their own
    /// Brighter configuration using the new Func overloads.
    /// This is the recommended pattern for test isolation.
    /// </summary>
    [Fact]
    public async Task ParallelBrighterSetups_WithFuncOverloads_AreIsolated()
    {
        const int parallelTests = 5;
        var results = new (IAmACommandProcessor Processor, InMemoryOutbox Outbox)[parallelTests];

        var tasks = new Task[parallelTests];
        for (int i = 0; i < parallelTests; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                var timeProvider = new FakeTimeProvider();
                var outbox = new InMemoryOutbox(timeProvider);
                var routingKey = new RoutingKey($"test.func.{index}");
                var internalBus = new InternalBus();

                var producer = new InMemoryMessageProducer(internalBus, timeProvider, new Publication
                {
                    Topic = routingKey,
                    RequestType = typeof(IsolationTestCommand)
                });

                var producerRegistry = new ProducerRegistry(
                    new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

                // Using the new Func<IServiceProvider, T> overload pattern
                var services = new ServiceCollection();
                services.AddSingleton(outbox);
                services.AddSingleton(producerRegistry);

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
                results[index] = (
                    provider.GetRequiredService<IAmACommandProcessor>(),
                    outbox
                );
            });
        }

        await Task.WhenAll(tasks);

        // Verify all are isolated
        for (int i = 0; i < parallelTests; i++)
        {
            for (int j = i + 1; j < parallelTests; j++)
            {
                Assert.NotSame(results[i].Processor, results[j].Processor);
                Assert.NotSame(results[i].Outbox, results[j].Outbox);
            }
        }
    }

    private class IsolationTestCommand : Command
    {
        public string Value { get; set; } = string.Empty;
        public IsolationTestCommand() : base(Guid.NewGuid()) { }
    }
}

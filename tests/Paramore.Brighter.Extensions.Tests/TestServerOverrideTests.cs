using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests
{
    public class TestServerOverrideTests
    {
        [Fact]
        public void ServiceProvider_AllRegisteredServicesAvailable()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Register dependencies first
            var customFactory = new TestRequestContextFactory();
            var customOutbox = new InMemoryOutbox(TimeProvider.System);
            serviceCollection.AddSingleton<IAmARequestContextFactory>(customFactory);
            serviceCollection.AddSingleton<IAmAnOutbox>(customOutbox);

            var routingKey = new RoutingKey("TestTopic");
            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication { Topic = routingKey }) }
                });

            // Configure Brighter with ServiceProvider overload
            serviceCollection.AddBrighter((options, sp) =>
            {
                options.RequestContextFactory = sp.GetRequiredService<IAmARequestContextFactory>();
            }).AddProducers((config, sp) =>
            {
                config.ProducerRegistry = producerRegistry;
                config.Outbox = sp.GetRequiredService<IAmAnOutbox>();
            }, ServiceLifetime.Singleton).AutoFromAssemblies();

            // Act
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var producersConfig = serviceProvider.GetService<IAmProducersConfiguration>();

            // Assert
            Assert.NotNull(commandProcessor);
            Assert.NotNull(producersConfig);
            Assert.Same(customOutbox, producersConfig.Outbox);
        }

        [Fact]
        public void TestOverride_CanReplaceRegistrations()
        {
            // Arrange - Simulating "production" configuration
            var serviceCollection = new ServiceCollection();

            var productionOutbox = new InMemoryOutbox(TimeProvider.System);
            serviceCollection.AddSingleton<IAmAnOutbox>(productionOutbox);

            var routingKey = new RoutingKey("TestTopic");
            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication { Topic = routingKey }) }
                });

            serviceCollection.AddBrighter()
                .AddProducers((config, sp) =>
                {
                    config.ProducerRegistry = producerRegistry;
                    config.Outbox = sp.GetRequiredService<IAmAnOutbox>();
                }, ServiceLifetime.Singleton)
                .AutoFromAssemblies();

            // Act - Simulating "test" override
            var testOutbox = new InMemoryOutbox(TimeProvider.System);

            // Replace the outbox registration
            var descriptor = new ServiceDescriptor(typeof(IAmAnOutbox), testOutbox);
            var existingDescriptor = serviceCollection.FirstOrDefault(d => d.ServiceType == typeof(IAmAnOutbox));
            if (existingDescriptor != null)
            {
                serviceCollection.Remove(existingDescriptor);
            }
            serviceCollection.Add(descriptor);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var resolvedOutbox = serviceProvider.GetService<IAmAnOutbox>();
            var producersConfig = serviceProvider.GetService<IAmProducersConfiguration>();

            // Assert
            Assert.NotNull(resolvedOutbox);
            Assert.Same(testOutbox, resolvedOutbox);
            Assert.NotSame(productionOutbox, resolvedOutbox);
            Assert.Same(testOutbox, producersConfig.Outbox);
        }

        [Fact]
        public void Configuration_CanAccessLateRegisteredServices()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            var routingKey = new RoutingKey("TestTopic");
            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication { Topic = routingKey }) }
                });

            // Configure Brighter first
            var builder = serviceCollection.AddBrighter((options, sp) =>
            {
                // This will try to resolve a service that's registered later
                var factory = sp.GetService<IAmARequestContextFactory>();
                options.RequestContextFactory = factory ?? new InMemoryRequestContextFactory();
            });

            // Register dependency AFTER AddBrighter
            var lateFactory = new TestRequestContextFactory();
            serviceCollection.AddSingleton<IAmARequestContextFactory>(lateFactory);

            builder.AddProducers((config, sp) =>
            {
                config.ProducerRegistry = producerRegistry;
            }, ServiceLifetime.Singleton).AutoFromAssemblies();

            // Act
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var brighterOptions = serviceProvider.GetService<IBrighterOptions>();

            // Assert
            Assert.NotNull(brighterOptions);
            Assert.Same(lateFactory, brighterOptions.RequestContextFactory);
        }

        [Fact]
        public void MultipleServices_CanBeResolvedInConfiguration()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Register multiple services
            var factory1 = new TestRequestContextFactory();
            var factory2 = new TestRequestContextFactory();
            var outbox = new InMemoryOutbox(TimeProvider.System);

            serviceCollection.AddSingleton<IAmARequestContextFactory>(factory1);
            serviceCollection.AddSingleton<IAmAnOutbox>(outbox);

            var routingKey = new RoutingKey("TestTopic");
            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication { Topic = routingKey }) }
                });

            // Act - Configure with multiple service resolutions
            serviceCollection.AddBrighter((options, sp) =>
            {
                options.RequestContextFactory = sp.GetRequiredService<IAmARequestContextFactory>();
            }).AddProducers((config, sp) =>
            {
                config.ProducerRegistry = producerRegistry;
                config.Outbox = sp.GetRequiredService<IAmAnOutbox>();
                config.DistributedLock = new InMemoryLock();
            }, ServiceLifetime.Singleton).AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var brighterOptions = serviceProvider.GetService<IBrighterOptions>();
            var producersConfig = serviceProvider.GetService<IAmProducersConfiguration>();

            // Assert
            Assert.NotNull(brighterOptions);
            Assert.NotNull(producersConfig);
            Assert.Same(factory1, brighterOptions.RequestContextFactory);
            Assert.Same(outbox, producersConfig.Outbox);
            Assert.NotNull(producersConfig.DistributedLock);
        }
    }
}

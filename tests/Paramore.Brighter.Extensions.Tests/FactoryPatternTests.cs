using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests
{
    public class FactoryPatternTests
    {
        [Fact]
        public void AddBrighter_WithServiceProvider_ResolvesFactoryFromDI()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var customFactory = new TestRequestContextFactory();
            serviceCollection.AddSingleton<IAmARequestContextFactory>(customFactory);

            // Act
            serviceCollection.AddBrighter((options, sp) =>
            {
                options.RequestContextFactory = sp.GetRequiredService<IAmARequestContextFactory>();
            }).AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var resolvedOptions = serviceProvider.GetService<IBrighterOptions>();

            // Assert
            Assert.NotNull(commandProcessor);
            Assert.NotNull(resolvedOptions);
            Assert.Same(customFactory, resolvedOptions.RequestContextFactory);
        }

        [Fact]
        public void AddProducers_WithServiceProvider_ResolvesOutboxFromDI()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var customOutbox = new InMemoryOutbox(TimeProvider.System);
            serviceCollection.AddSingleton<IAmAnOutbox>(customOutbox);

            var routingKey = new RoutingKey("TestTopic");
            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication { Topic = routingKey }) }
                });

            // Act
            serviceCollection.AddBrighter()
                .AddProducers((config, sp) =>
                {
                    config.ProducerRegistry = producerRegistry;
                    config.Outbox = sp.GetRequiredService<IAmAnOutbox>();
                }, ServiceLifetime.Singleton)
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var resolvedConfig = serviceProvider.GetService<IAmProducersConfiguration>();

            // Assert
            Assert.NotNull(resolvedConfig);
            Assert.Same(customOutbox, resolvedConfig.Outbox);
        }

        [Fact]
        public void AddConsumers_WithServiceProvider_ResolvesInboxFromDI()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var customInbox = new InMemoryInbox(TimeProvider.System);
            serviceCollection.AddSingleton<IAmAnInbox>(customInbox);

            // Act
            serviceCollection.AddConsumers((config, sp) =>
            {
                var inbox = sp.GetRequiredService<IAmAnInbox>();
                config.InboxConfiguration = new InboxConfiguration(inbox);
                config.Subscriptions = new List<Subscription>();
            }).AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var resolvedConfig = serviceProvider.GetService<IAmConsumerOptions>();

            // Assert
            Assert.NotNull(resolvedConfig);
            Assert.Same(customInbox, resolvedConfig.InboxConfiguration.Inbox);
        }

        [Fact]
        public void DeferredConfiguration_ExecutesDuringServiceResolution()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var executionFlag = false;

            // Act
            serviceCollection.AddBrighter((options, sp) =>
            {
                executionFlag = true;
                options.RequestContextFactory = new InMemoryRequestContextFactory();
            }).AutoFromAssemblies();

            Assert.False(executionFlag);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            // Assert
            Assert.True(executionFlag);
            Assert.NotNull(commandProcessor);
        }

        [Fact]
        public void FactoryIsThreadSafe_ExecutesOnlyOnce()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var executionCounter = 0;

            serviceCollection.AddBrighter((options, sp) =>
            {
                System.Threading.Interlocked.Increment(ref executionCounter);
                options.RequestContextFactory = new InMemoryRequestContextFactory();
            }).AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act
            var tasks = new System.Threading.Tasks.Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    var options = serviceProvider.GetService<IBrighterOptions>();
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);

            // Assert
            Assert.Equal(1, executionCounter);
        }

        [Fact]
        public void OldAndNewPatterns_CanUseEitherOverload()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Act
            serviceCollection.AddBrighter(options =>
            {
                options.RequestContextFactory = new InMemoryRequestContextFactory();
            });

            var routingKey = new RoutingKey("TestTopic");
            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication { Topic = routingKey }) }
                });

            var builder = serviceCollection.AddBrighter();
            builder.AddProducers((config, sp) =>
            {
                config.ProducerRegistry = producerRegistry;
                config.Outbox = new InMemoryOutbox(TimeProvider.System);
            }, ServiceLifetime.Singleton);

            builder.AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            // Assert
            Assert.NotNull(commandProcessor);
        }
    }
}

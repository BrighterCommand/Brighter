using System;
using System.Collections.Generic;
using System.Linq;
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

        [Fact]
        public void AddBrighter_SimpleOverload_RespectsHandlerLifetimeConfiguration()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Act - use simple overload with Scoped lifetime
            serviceCollection.AddBrighter(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
            }).AutoFromAssemblies();

            // Assert - verify TestEventHandler is registered as Scoped
            var handlerDescriptor = serviceCollection.FirstOrDefault(
                sd => sd.ServiceType == typeof(TestEventHandler));

            Assert.NotNull(handlerDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, handlerDescriptor.Lifetime);
        }

        [Fact]
        public void AddBrighter_SimpleOverload_RespectsMapperLifetimeConfiguration()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Act - use simple overload with Singleton mapper lifetime
            serviceCollection.AddBrighter(options =>
            {
                options.MapperLifetime = ServiceLifetime.Singleton;
            }).AutoFromAssemblies();

            // Assert - verify TestEventMessageMapper is registered as Singleton
            var mapperDescriptor = serviceCollection.FirstOrDefault(
                sd => sd.ServiceType == typeof(TestEventMessageMapper));

            Assert.NotNull(mapperDescriptor);
            Assert.Equal(ServiceLifetime.Singleton, mapperDescriptor.Lifetime);
        }

        [Fact]
        public void AddBrighter_SimpleOverload_RespectsTransformerLifetimeConfiguration()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Act - use simple overload with Scoped transformer lifetime
            serviceCollection.AddBrighter(options =>
            {
                options.TransformerLifetime = ServiceLifetime.Scoped;
            }).AutoFromAssemblies();

            // Assert - verify TestTransform is registered as Scoped
            var transformDescriptor = serviceCollection.FirstOrDefault(
                sd => sd.ServiceType == typeof(TestTransform));

            Assert.NotNull(transformDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, transformDescriptor.Lifetime);
        }

        [Fact]
        public void AddBrighter_ServiceProviderOverload_UsesDefaultLifetimes()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Act - use SP overload and try to set Scoped lifetime
            // Note: SP overload cannot honor lifetime settings because they are needed
            // at registration time, but the callback is deferred to resolution time
            serviceCollection.AddBrighter((options, sp) =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped; // This will NOT be honored
            }).AutoFromAssemblies();

            // Assert - handler should be registered with default (Transient) lifetime
            var handlerDescriptor = serviceCollection.FirstOrDefault(
                sd => sd.ServiceType == typeof(TestEventHandler));

            Assert.NotNull(handlerDescriptor);
            Assert.Equal(ServiceLifetime.Transient, handlerDescriptor.Lifetime);
        }

        [Fact]
        public void AddConsumers_SimpleOverload_RespectsHandlerLifetimeConfiguration()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Act - use simple overload with Scoped lifetime
            serviceCollection.AddConsumers(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.Subscriptions = new List<Subscription>();
            }).AutoFromAssemblies();

            // Assert - verify TestEventHandler is registered as Scoped
            var handlerDescriptor = serviceCollection.FirstOrDefault(
                sd => sd.ServiceType == typeof(TestEventHandler));

            Assert.NotNull(handlerDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, handlerDescriptor.Lifetime);
        }

        [Fact]
        public void AddConsumers_ServiceProviderOverload_UsesDefaultLifetimes()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            // Act - use SP overload and try to set Scoped lifetime
            serviceCollection.AddConsumers((options, sp) =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped; // This will NOT be honored
                options.Subscriptions = new List<Subscription>();
            }).AutoFromAssemblies();

            // Assert - handler should be registered with default (Transient) lifetime
            var handlerDescriptor = serviceCollection.FirstOrDefault(
                sd => sd.ServiceType == typeof(TestEventHandler));

            Assert.NotNull(handlerDescriptor);
            Assert.Equal(ServiceLifetime.Transient, handlerDescriptor.Lifetime);
        }

        [Fact]
        public void AddBrighter_SimpleOverload_UsesRequestContextFactoryDirectly()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var customFactory = new TestRequestContextFactory();

            // Act - use simple overload with custom factory
            serviceCollection.AddBrighter(options =>
            {
                options.RequestContextFactory = customFactory;
            }).AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var resolvedFactory = serviceProvider.GetService<IAmARequestContextFactory>();

            // Assert - factory should be the one we set directly
            Assert.NotNull(resolvedFactory);
            Assert.Same(customFactory, resolvedFactory);
        }

        [Fact]
        public void AddBrighter_ServiceProviderOverload_ResolvesRequestContextFactoryAtRuntime()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var customFactory = new TestRequestContextFactory();
            serviceCollection.AddSingleton<ICustomDependency>(new CustomDependency());

            // Act - use SP overload to resolve factory based on other DI services
            serviceCollection.AddBrighter((options, sp) =>
            {
                // Verify we can access other services from DI
                var dep = sp.GetRequiredService<ICustomDependency>();
                Assert.NotNull(dep);
                options.RequestContextFactory = customFactory;
            }).AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var resolvedOptions = serviceProvider.GetService<IBrighterOptions>();

            // Assert - factory should be resolved at runtime through the options
            Assert.NotNull(resolvedOptions);
            Assert.Same(customFactory, resolvedOptions.RequestContextFactory);
        }

        private interface ICustomDependency { }
        private sealed class CustomDependency : ICustomDependency { }
    }
}

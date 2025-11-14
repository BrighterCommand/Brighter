using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.FeatureSwitch.Providers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests
{
    public class ServiceProviderFactoryMethodTests
    {
        [Fact]
        public void UsePolicyRegistry_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            serviceCollection.AddSingleton(new TestDependency { Value = "PolicyRegistryFactory" });

            serviceCollection
                .AddBrighter()
                .UsePolicyRegistry(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    var retryPolicy = Policy.Handle<Exception>().Retry(3);
                    var circuitBreakerPolicy = Policy.NoOp();
                    var retryPolicyAsync = Policy.Handle<Exception>().RetryAsync(3);
                    var circuitBreakerPolicyAsync = Policy.NoOpAsync();

                    return new PolicyRegistry
                    {
                        { CommandProcessor.RETRYPOLICY, retryPolicy },
                        { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                        { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                        { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
                    };
                });

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var registry = serviceProvider.GetService<IPolicyRegistry<string>>();

            Assert.NotNull(registry);
            Assert.True(registry.ContainsKey(CommandProcessor.RETRYPOLICY));
        }

        [Fact]
        public void UseRequestContextFactory_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            serviceCollection.AddSingleton(new TestDependency { Value = "ContextFactory" });

            serviceCollection
                .AddBrighter()
                .UseRequestContextFactory(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    return new InMemoryRequestContextFactory();
                })
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var resolvedFactory = serviceProvider.GetService<IAmARequestContextFactory>();

            Assert.NotNull(commandProcessor);
            Assert.NotNull(resolvedFactory);
            Assert.IsType<InMemoryRequestContextFactory>(resolvedFactory);
        }

        [Fact]
        public void UseFeatureSwitchRegistry_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            serviceCollection.AddSingleton(new TestDependency { Value = "FeatureSwitchFactory" });

            serviceCollection
                .AddBrighter()
                .UseFeatureSwitchRegistry(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    return FluentConfigRegistryBuilder.With().Build();
                })
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var resolvedRegistry = serviceProvider.GetService<IAmAFeatureSwitchRegistry>();

            Assert.NotNull(commandProcessor);
            Assert.NotNull(resolvedRegistry);
        }

        [Fact]
        public void UseProducerRegistry_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            serviceCollection.AddSingleton(new TestDependency { Value = "ProducerRegistryFactory" });

            serviceCollection
                .AddBrighter()
                .AddProducers(config =>
                {
                    config.MessageMapperRegistry = new MessageMapperRegistry(
                        new SimpleMessageMapperFactory(type => new TestEventMessageMapper()),
                        new SimpleMessageMapperFactoryAsync(type => new TestEventMessageMapperAsync())
                    );
                })
                .UseProducerRegistry(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    const string mytopic = "MyTopic";
                    var routingKey = new RoutingKey(mytopic);

                    return new ProducerRegistry(
                        new Dictionary<RoutingKey, IAmAMessageProducer>
                        {
                            {
                                routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication{ Topic = routingKey})
                            },
                        });
                })
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var resolvedRegistry = serviceProvider.GetService<IAmAProducerRegistry>();

            Assert.NotNull(commandProcessor);
            Assert.NotNull(resolvedRegistry);
        }

        [Fact]
        public void UseOutbox_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            serviceCollection.AddSingleton(new TestDependency { Value = "OutboxFactory" });
            serviceCollection.AddSingleton<TimeProvider>(TimeProvider.System);

            const string mytopic = "MyTopic";
            var routingKey = new RoutingKey(mytopic);

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    {
                        routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication{ Topic = routingKey})
                    },
                });

            serviceCollection
                .AddBrighter()
                .AddProducers(config =>
                {
                    config.ProducerRegistry = producerRegistry;
                    config.MessageMapperRegistry = new MessageMapperRegistry(
                        new SimpleMessageMapperFactory(type => new TestEventMessageMapper()),
                        new SimpleMessageMapperFactoryAsync(type => new TestEventMessageMapperAsync())
                    );
                })
                .UseOutbox(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    var timeProvider = provider.GetRequiredService<TimeProvider>();
                    return new InMemoryOutbox(timeProvider);
                })
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            Assert.NotNull(commandProcessor);
        }

        [Fact]
        public void UseDistributedLock_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            const string mytopic = "MyTopic";
            var routingKey = new RoutingKey(mytopic);

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    {
                        routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication{ Topic = routingKey})
                    },
                });

            serviceCollection
                .AddBrighter()
                .AddProducers(config =>
                {
                    config.ProducerRegistry = producerRegistry;
                    config.MessageMapperRegistry = new MessageMapperRegistry(
                        new SimpleMessageMapperFactory(type => new TestEventMessageMapper()),
                        new SimpleMessageMapperFactoryAsync(type => new TestEventMessageMapperAsync())
                    );
                })
                .UseDistributedLock(provider => new InMemoryLock())
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var resolvedLock = serviceProvider.GetService<IDistributedLock>();

            Assert.NotNull(commandProcessor);
            Assert.NotNull(resolvedLock);
            Assert.IsType<InMemoryLock>(resolvedLock);
        }

        [Fact]
        public void MultipleFactoryMethods_ShouldChainCorrectly()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            serviceCollection.AddSingleton(new TestDependency { Value = "MultipleFactories" });

            serviceCollection
                .AddBrighter()
                .UsePolicyRegistry(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    var retryPolicy = Policy.Handle<Exception>().Retry(3);
                    var circuitBreakerPolicy = Policy.NoOp();
                    var retryPolicyAsync = Policy.Handle<Exception>().RetryAsync(3);
                    var circuitBreakerPolicyAsync = Policy.NoOpAsync();

                    return new PolicyRegistry
                    {
                        { CommandProcessor.RETRYPOLICY, retryPolicy },
                        { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                        { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                        { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
                    };
                })
                .UseRequestContextFactory(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    return new InMemoryRequestContextFactory();
                })
                .UseFeatureSwitchRegistry(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    return FluentConfigRegistryBuilder.With().Build();
                })
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            Assert.NotNull(commandProcessor);
        }

        [Fact]
        public void FactoryMethod_CanAccessOtherServicesFromProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

            serviceCollection.AddSingleton(new TestDependency { Value = "TestValue" });

            string? capturedValue = null;

            serviceCollection
                .AddBrighter()
                .UseRequestContextFactory(provider =>
                {
                    var dependency = provider.GetRequiredService<TestDependency>();
                    capturedValue = dependency.Value;
                    return new InMemoryRequestContextFactory();
                })
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            Assert.NotNull(commandProcessor);
            Assert.Equal("TestValue", capturedValue);
        }

        private class TestDependency
        {
            public string? Value { get; set; }
        }
    }
}

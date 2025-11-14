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
    [Collection("Sequential")]
    public class ServiceProviderFactoryMethodTests
    {
        [Fact]
        public void UsePolicyRegistry_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry([TimeSpan.FromMilliseconds(50)]);
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var retryPolicyAsync = Policy.Handle<Exception>().WaitAndRetryAsync([TimeSpan.FromMilliseconds(50)]);
            var circuitBreakerPolicyAsync = Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(500));

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
            };

            serviceCollection.AddSingleton<IPolicyRegistry<string>>(policyRegistry);

            serviceCollection
                .AddBrighter()
                .UsePolicyRegistry(provider => provider.GetRequiredService<IPolicyRegistry<string>>())
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            Assert.NotNull(commandProcessor);
        }

        [Fact]
        public void UseRequestContextFactory_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            var customContextFactory = new InMemoryRequestContextFactory();

            serviceCollection.AddSingleton<IAmARequestContextFactory>(customContextFactory);

            serviceCollection
                .AddBrighter()
                .UseRequestContextFactory(provider => provider.GetRequiredService<IAmARequestContextFactory>())
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var resolvedFactory = serviceProvider.GetService<IAmARequestContextFactory>();

            Assert.NotNull(commandProcessor);
            Assert.Same(customContextFactory, resolvedFactory);
        }

        [Fact]
        public void UseFeatureSwitchRegistry_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            var featureSwitchRegistry = FluentConfigRegistryBuilder.With().Build();

            serviceCollection.AddSingleton<IAmAFeatureSwitchRegistry>(featureSwitchRegistry);

            serviceCollection
                .AddBrighter()
                .UseFeatureSwitchRegistry(provider => provider.GetRequiredService<IAmAFeatureSwitchRegistry>())
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var resolvedRegistry = serviceProvider.GetService<IAmAFeatureSwitchRegistry>();

            Assert.NotNull(commandProcessor);
            Assert.Same(featureSwitchRegistry, resolvedRegistry);
        }

        [Fact]
        public void UseProducerRegistry_ShouldResolveFromServiceProvider()
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

            serviceCollection.AddSingleton<IAmAProducerRegistry>(producerRegistry);

            serviceCollection
                .AddBrighter()
                .AddProducers(config =>
                {
                    config.MessageMapperRegistry = new MessageMapperRegistry(
                        new SimpleMessageMapperFactory(type => new TestEventMessageMapper()),
                        new SimpleMessageMapperFactoryAsync(type => new TestEventMessageMapperAsync())
                    );
                })
                .UseProducerRegistry(provider => provider.GetRequiredService<IAmAProducerRegistry>())
                .AutoFromAssemblies();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var resolvedRegistry = serviceProvider.GetService<IAmAProducerRegistry>();

            Assert.NotNull(commandProcessor);
            Assert.Same(producerRegistry, resolvedRegistry);
        }

        [Fact]
        public void UseOutbox_ShouldResolveFromServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            var outbox = new InMemoryOutbox(TimeProvider.System);

            serviceCollection.AddSingleton<IAmAnOutbox>(outbox);

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
                .UseOutbox(provider => provider.GetRequiredService<IAmAnOutbox>())
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

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry([TimeSpan.FromMilliseconds(50)]);
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var retryPolicyAsync = Policy.Handle<Exception>().WaitAndRetryAsync([TimeSpan.FromMilliseconds(50)]);
            var circuitBreakerPolicyAsync = Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(500));

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
            };

            var customContextFactory = new InMemoryRequestContextFactory();
            var featureSwitchRegistry = FluentConfigRegistryBuilder.With().Build();

            serviceCollection.AddSingleton<IPolicyRegistry<string>>(policyRegistry);
            serviceCollection.AddSingleton<IAmARequestContextFactory>(customContextFactory);
            serviceCollection.AddSingleton<IAmAFeatureSwitchRegistry>(featureSwitchRegistry);

            serviceCollection
                .AddBrighter()
                .UsePolicyRegistry(provider => provider.GetRequiredService<IPolicyRegistry<string>>())
                .UseRequestContextFactory(provider => provider.GetRequiredService<IAmARequestContextFactory>())
                .UseFeatureSwitchRegistry(provider => provider.GetRequiredService<IAmAFeatureSwitchRegistry>())
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

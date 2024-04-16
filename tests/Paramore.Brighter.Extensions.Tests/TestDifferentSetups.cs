﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Xunit;

namespace Tests
{
    public class TestBrighterExtension
    {
        [Fact]
        public void BasicSetup()
        {
            var serviceCollection = new ServiceCollection();
            
            serviceCollection.AddBrighter().AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            
            Assert.NotNull(commandProcessor);
        }

        [Fact]
        public void WithExternalBus()
        {
            var serviceCollection = new ServiceCollection();
            const string mytopic = "MyTopic";
            var producerRegistry = new ProducerRegistry(
                new Dictionary<string, IAmAMessageProducer>
                {
                    { mytopic, new FakeProducer{ Publication = { Topic = new RoutingKey(mytopic)}} },
                });
            
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(type => new TestEventMessageMapper()), 
                new SimpleMessageMapperFactoryAsync(type => new TestEventMessageMapperAsync())
            );

            serviceCollection.AddSingleton<ILoggerFactory, LoggerFactory>();

            serviceCollection
                .AddBrighter()
                .UseExternalBus((config) =>
                {
                    config.ProducerRegistry = producerRegistry;
                    config.MessageMapperRegistry = messageMapperRegistry;
                })
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            
            Assert.NotNull(commandProcessor);
        }

        
        [Fact]
        public void WithCustomPolicy()
        {
            var serviceCollection = new ServiceCollection();

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var retryPolicyAsync = Policy.Handle<Exception>().WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicyAsync = Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(500));
            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
            };
            
            serviceCollection
                .AddBrighter(options => options.PolicyRegistry = policyRegistry)
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            
            Assert.NotNull(commandProcessor);
        }

        [Fact]
        public void WithScopedLifetime()
        {
            var serviceCollection = new ServiceCollection();
            
            serviceCollection.AddBrighter(options => options.CommandProcessorLifetime = ServiceLifetime.Scoped
                ).AutoFromAssemblies();

            Assert.Equal( ServiceLifetime.Scoped, serviceCollection.SingleOrDefault(x => x.ServiceType == typeof(IAmACommandProcessor))?.Lifetime);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            
            Assert.NotNull(commandProcessor);
        }

    }

    internal class FakeProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync
    {
        public List<Message> SentMessages { get; } = new();
        
        public Publication Publication { get; } = new();

        public void Dispose()
        {
            SentMessages.Clear();
        }

        public Task SendAsync(Message message)
        {
            var tcs = new TaskCompletionSource();
            Send(message);
            tcs.SetResult();
            return tcs.Task;
        }

        public void Send(Message message)
        {
            SentMessages.Add(message); 
        }

        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds)).Wait();
            Send(message);
        }
    }
}

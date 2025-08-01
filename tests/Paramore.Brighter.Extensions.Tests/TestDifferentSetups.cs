using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
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

        [Theory]
        [InlineData(typeof(SomeSqlConnectionProvider), typeof(SomeSqlUnitOfBox))]
        [InlineData(typeof(SomeSqlUnitOfBox), typeof(SomeSqlUnitOfBox))]
        public void WithExternalBus(Type connectionProvider, Type transactionProvider)
        {
            var serviceCollection = new ServiceCollection();
            const string mytopic = "MyTopic";
            var routingKey = new RoutingKey(mytopic);
            
            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { 
                        routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication{ Topic = routingKey})
                    },
                });
            
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(type => new TestEventMessageMapper()), 
                new SimpleMessageMapperFactoryAsync(type => new TestEventMessageMapperAsync())
            );

            serviceCollection.AddSingleton<ILoggerFactory, LoggerFactory>();

            serviceCollection
                .AddBrighter()
                .AddProducers(config =>
                {
                    config.ProducerRegistry = producerRegistry;
                    config.MessageMapperRegistry = messageMapperRegistry;
                    config.ConnectionProvider = connectionProvider;
                    config.TransactionProvider = transactionProvider;
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
        
        public class SomeSqlConnectionProvider : RelationalDbConnectionProvider
        {
            public override DbConnection GetConnection()
            {
                throw new NotImplementedException();
            }
        }
        
        
        public class SomeSqlUnitOfBox :  RelationalDbTransactionProvider
        {
            public override DbConnection GetConnection()
            {
                throw new NotImplementedException();
            }
        }

    }
}

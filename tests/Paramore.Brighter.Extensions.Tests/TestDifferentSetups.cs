using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
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
        [InlineData(typeof(SomeSqlConnectionProvider), typeof(StubSqlTransactionProvider))]
        [InlineData(typeof(StubSqlTransactionProvider), typeof(StubSqlTransactionProvider))]
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

            var outbox = new StubSqlOutbox(
                DbSystem.MySql,
                new StubSqlDbConfiguration(),
                new SomeSqlConnectionProvider(),
                new StubRelationDatabaseOutboxQueries(),
                new Logger<StubSqlOutbox>(new LoggerFactory()));

            serviceCollection.AddSingleton<ILoggerFactory, LoggerFactory>();

            serviceCollection
                .AddBrighter()
                .AddProducers(config =>
                {
                    config.ProducerRegistry = producerRegistry;
                    config.MessageMapperRegistry = messageMapperRegistry;
                    config.ConnectionProvider = connectionProvider;
                    config.TransactionProvider = transactionProvider;
                    config.Outbox = outbox;
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

            serviceCollection.AddBrighter(
                ).AutoFromAssemblies();

            Assert.Equal(ServiceLifetime.Singleton, serviceCollection.SingleOrDefault(x => x.ServiceType == typeof(IAmACommandProcessor))?.Lifetime);

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


        public class StubSqlTransactionProvider : RelationalDbTransactionProvider
        {
            public override DbConnection GetConnection()
            {
                throw new NotImplementedException();
            }
        }

        public class StubSqlOutbox : RelationDatabaseOutbox
        {
            public StubSqlOutbox(DbSystem dbSystem,
                IAmARelationalDatabaseConfiguration configuration,
                IAmARelationalDbConnectionProvider connectionProvider,
                IRelationDatabaseOutboxQueries queries,
                ILogger logger,
                InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
                : base(dbSystem, configuration, connectionProvider, queries, logger, instrumentationOptions)
            {
            }

            protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
            {
                throw new NotImplementedException();
            }

            protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
            {
                throw new NotImplementedException();
            }

            protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
            {
                throw new NotImplementedException();
            }
        }

        public class StubSqlDbConfiguration : IAmARelationalDatabaseConfiguration
        {
            public string ConnectionString => throw new NotImplementedException();
            public string OutBoxTableName => throw new NotImplementedException();
            public string InBoxTableName => throw new NotImplementedException();
            public bool BinaryMessagePayload => throw new NotImplementedException();

            public string DatabaseName => throw new NotImplementedException();

            public string QueueStoreTable => throw new NotImplementedException();
        }

        public class StubRelationDatabaseOutboxQueries : IRelationDatabaseOutboxQueries
        {
            public string PagedDispatchedCommand => throw new NotImplementedException();

            public string PagedReadCommand => throw new NotImplementedException();

            public string PagedOutstandingCommand => throw new NotImplementedException();

            public string PagedOutstandingCommandInStatement => throw new NotImplementedException();

            public string AddCommand => throw new NotImplementedException();

            public string BulkAddCommand => throw new NotImplementedException();

            public string MarkDispatchedCommand => throw new NotImplementedException();

            public string MarkMultipleDispatchedCommand => throw new NotImplementedException();

            public string GetMessageCommand => throw new NotImplementedException();

            public string GetMessagesCommand => throw new NotImplementedException();

            public string DeleteMessagesCommand => throw new NotImplementedException();

            public string GetNumberOfOutstandingMessagesCommand => throw new NotImplementedException();
        }
    }
}

using DbMaker;
using GreetingsApp.EntityGateway;
using GreetingsApp.Handlers;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using Microsoft.EntityFrameworkCore;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;
using TransportMaker;

namespace GreetingsWeb;

public static class Extensions
{
        public static void ConfigureBrighter(this IServiceCollection services,
            ConfigurationManager configuration)
        {
            var transport = configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
            if (string.IsNullOrWhiteSpace(transport))
                throw new InvalidOperationException("Transport is not set");
            
            MessagingTransport messagingTransport =
                ConfigureTransport.TransportType(transport);

            ConfigureTransport.AddSchemaRegistryMaybe(services, messagingTransport);
            
            var outboxConfiguration = new RelationalDatabaseConfiguration(
                configuration.GetConnectionString("Greetings"),
                "Greetings", 
                binaryMessagePayload:messagingTransport == MessagingTransport.Kafka
            );
            
            string dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
            if (string.IsNullOrWhiteSpace(dbType))
                throw new InvalidOperationException("DbType is not set");

            var rdbms = DbResolver.GetDatabaseType(dbType);
            (IAmAnOutbox outbox, Type transactionProvider, Type connectionProvider) = 
                OutboxFactory.MakeEfOutbox<GreetingsEntityGateway>(rdbms, outboxConfiguration);
            
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);
            
            IAmAProducerRegistry producerRegistry = ConfigureTransport.MakeProducerRegistry<GreetingMade>(messagingTransport, configuration.GetConnectionString("messaging"));

            services.AddBrighter(options =>
                {
                    //we want to use scoped, so make sure everything understands that which needs to
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                    options.MapperLifetime = ServiceLifetime.Singleton;
                    options.PolicyRegistry = new GreetingsPolicy();
                })
                .UseExternalBus((configure) =>
                    {
                        configure.ProducerRegistry = producerRegistry;
                        configure.Outbox = outbox;
                        configure.TransactionProvider = transactionProvider;
                        configure.ConnectionProvider = connectionProvider;
                        configure.MaxOutStandingMessages = 5;
                        configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
                    }
                )
                .AutoFromAssemblies();
        }
        
        public static void ConfigureDarker(this IServiceCollection services)
        {
            services.AddDarker(options =>
                {
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.QueryProcessorLifetime = ServiceLifetime.Scoped;
                })
                .AddHandlersFromAssemblies(typeof(FindPersonByNameHandlerAsync).Assembly)
                .AddJsonQueryLogging()
                .AddPolicies(new GreetingsPolicy());
        }

        public static void ConfigureEfCore(this IServiceCollection services, ConfigurationManager configuration)
        {
            string connectionString = configuration.GetConnectionString("Greetings");
            string configDbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
            
            if (string.IsNullOrWhiteSpace(configDbType))
                throw new InvalidOperationException("DbType is not set");
            
            var dbType = DbResolver.GetDatabaseType(configDbType);
        
            switch (dbType)
            {
                case Rdbms.Sqlite:
                    ConfigureSqlite(services, connectionString);
                    break;
                case Rdbms.MySql:
                    ConfigureMySql(services, connectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Database type {dbType} is not supported");
            }
        }

        private static void ConfigureMySql(IServiceCollection services, string connectionString)
        {
            services.AddDbContextPool<GreetingsEntityGateway>(builder =>
            {
                builder
                    .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            });
        }
        
        private static void ConfigureSqlite(IServiceCollection services, string connectionString)
        {
            services.AddDbContext<GreetingsEntityGateway>(
                builder =>
                {
                    builder.UseSqlite(connectionString)
                        .EnableDetailedErrors()
                        .EnableSensitiveDataLogging();
                });
        }

}

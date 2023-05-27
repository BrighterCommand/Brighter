using System;
using System.IO;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using DapperExtensions;
using DapperExtensions.Sql;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.MySql;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Sqlite;
using SalutationAnalytics.Database;
using SalutationPorts.EntityMappers;
using SalutationPorts.Policies;
using SalutationPorts.Requests;

namespace SalutationAnalytics
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            host.CheckDbIsUp();
            host.MigrateDatabase();
            host.CreateInbox();
            host.CreateOutbox();
            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(configurationBuilder =>
                {
                    configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
                    configurationBuilder.AddJsonFile("appsettings.json", optional: true);
                    configurationBuilder.AddJsonFile($"appsettings.{GetEnvironment()}.json", optional: true);
                    configurationBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_"); //NOTE: Although not web, we use this to grab the environment
                    configurationBuilder.AddEnvironmentVariables(prefix: "BRIGHTER_");
                    configurationBuilder.AddCommandLine(args);
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    ConfigureMigration(hostContext, services);
                    ConfigureDapper(hostContext, services);
                    ConfigureBrighter(hostContext, services);
                })
                .UseConsoleLifetime();

        private static void ConfigureBrighter(HostBuilderContext hostContext, IServiceCollection services)
        {
            var subscriptions = new KafkaSubscription[]
            {
                new KafkaSubscription<GreetingMade>(
                    new SubscriptionName("paramore.sample.salutationanalytics"),
                    channelName: new ChannelName("SalutationAnalytics"),
                    routingKey: new RoutingKey("greeting.event"),
                    groupId: "kafka-GreetingsReceiverConsole-Sample",
                    timeoutInMilliseconds: 100,
                    offsetDefault: AutoOffsetReset.Earliest,
                    commitBatchSize: 5,
                    sweepUncommittedOffsetsIntervalMs: 10000,
                    makeChannels: OnMissingChannel.Create)
            };
                    
            //We take a direct dependency on the schema registry in the message mapper
            var schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081"};
            var cachedSchemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
            services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);

            //create the gateway
            var consumerFactory = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration { Name = "paramore.brighter", BootStrapServers = new[] { "localhost:9092" } }
            );

            var relationalDatabaseConfiguration =
                new RelationalDatabaseConfiguration(
                    DbConnectionString(hostContext), 
                    SchemaCreation.INBOX_TABLE_NAME, 
                    binaryMessagePayload:true
                    );
            
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(relationalDatabaseConfiguration);

            var producerRegistry = new KafkaProducerRegistryFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "paramore.brighter.greetingsender",
                    BootStrapServers = new[] { "localhost:9092" }
                },
                new KafkaPublication[]
                {
                    new KafkaPublication
                    {
                        Topic = new RoutingKey("salutationrecieved.event"),
                        MessageSendMaxRetries = 3,
                        MessageTimeoutMs = 1000,
                        MaxInFlightRequestsPerConnection = 1,
                        MakeChannels = OnMissingChannel.Create
                    }
                }
            ).Create();

            services.AddServiceActivator(options =>
                {
                    options.Subscriptions = subscriptions;
                    options.UseScoped = true;
                    options.ChannelFactory = new ChannelFactory(consumerFactory);
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.MapperLifetime = ServiceLifetime.Singleton;
                    options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                    options.PolicyRegistry = new SalutationPolicy();
                    options.InboxConfiguration = new InboxConfiguration(
                        ConfigureInbox(hostContext, relationalDatabaseConfiguration),
                        scope: InboxScope.Commands,
                        onceOnly: true,
                        actionOnExists: OnceOnlyAction.Throw
                    );
                })
                .ConfigureJsonSerialisation((options) =>
                {
                    //We don't strictly need this, but added as an example
                    options.PropertyNameCaseInsensitive = true;
                })
                .UseExternalBus((configure) =>
                {
                    configure.ProducerRegistry = producerRegistry;
                })
                .AutoFromAssemblies();

            services.AddHostedService<ServiceActivatorHostedService>();
        }

        private static void ConfigureMigration(HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
            if (hostBuilderContext.HostingEnvironment.IsDevelopment())
            {
                services
                    .AddFluentMigratorCore()
                    .ConfigureRunner(c =>
                    {
                        c.AddSQLite()
                            .WithGlobalConnectionString(DbConnectionString(hostBuilderContext))
                            .ScanIn(typeof(Salutations_SqliteMigrations.Migrations.SqliteInitialCreate).Assembly).For.Migrations();
                    });
            }
            else
            {
                services
                    .AddFluentMigratorCore()
                    .ConfigureRunner(c => c.AddMySql5()
                        .WithGlobalConnectionString(DbConnectionString(hostBuilderContext))
                        .ScanIn(typeof(Salutations_mySqlMigrations.Migrations.MySqlInitialCreate).Assembly).For.Migrations()
                    );
            }
        }

        private static void ConfigureDapper(HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
            ConfigureDapperByHost(GetDatabaseType(hostBuilderContext), services);
            DapperExtensions.DapperExtensions.SetMappingAssemblies(new[] { typeof(SalutationMapper).Assembly });
            DapperAsyncExtensions.SetMappingAssemblies(new[] { typeof(SalutationMapper).Assembly });
        }

        private static void ConfigureDapperByHost(DatabaseType databaseType, IServiceCollection services)
        {
            switch (databaseType)
            {
                case DatabaseType.Sqlite:
                    ConfigureDapperSqlite(services);
                    break;
                case DatabaseType.MySql:
                    ConfigureDapperMySql(services);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
            }
        }

        private static void ConfigureDapperSqlite(IServiceCollection services)
        {
            DapperExtensions.DapperExtensions.SqlDialect = new SqliteDialect();
            DapperAsyncExtensions.SqlDialect = new SqliteDialect();
            services.AddScoped<IAmARelationalDbConnectionProvider, SqliteConnectionProvider>();
            services.AddScoped<IAmATransactionConnectionProvider, SqliteUnitOfWork>();
        }

        private static void ConfigureDapperMySql(IServiceCollection services)
        {
            DapperExtensions.DapperExtensions.SqlDialect = new MySqlDialect();
            DapperAsyncExtensions.SqlDialect = new MySqlDialect();
            services.AddScoped<IAmARelationalDbConnectionProvider, MySqlConnectionProvider>();
            services.AddScoped<IAmATransactionConnectionProvider, MySqlUnitOfWork>();
        }


        private static IAmAnInbox ConfigureInbox(HostBuilderContext hostContext, IAmARelationalDatabaseConfiguration configuration)
        {
            if (hostContext.HostingEnvironment.IsDevelopment())
            {
                return new SqliteInbox(new RelationalDatabaseConfiguration(DbConnectionString(hostContext), SchemaCreation.INBOX_TABLE_NAME));
            }

            return new MySqlInbox(new RelationalDatabaseConfiguration(DbConnectionString(hostContext), SchemaCreation.INBOX_TABLE_NAME));
        }

        private static string DbConnectionString(HostBuilderContext hostContext)
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return hostContext.HostingEnvironment.IsDevelopment()
                ? "Filename=Salutations.db;Cache=Shared"
                : hostContext.Configuration.GetConnectionString("Salutations");
        }

        private static DatabaseType GetDatabaseType(HostBuilderContext hostContext)
        {
            return hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV] switch

            {
                DatabaseGlobals.MYSQL => DatabaseType.MySql,
                DatabaseGlobals.MSSQL => DatabaseType.MsSql,
                DatabaseGlobals.POSTGRESSQL => DatabaseType.Postgres,
                DatabaseGlobals.SQLITE => DatabaseType.Sqlite,
                _ => throw new InvalidOperationException("Could not determine the database type")
            };
        }


        private static string GetEnvironment()
        {
            //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }
    }
}

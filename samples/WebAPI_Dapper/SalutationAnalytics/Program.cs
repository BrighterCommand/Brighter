using System;
using System.IO;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MySql;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Sqlite;
using SalutationAnalytics.Database;
using SalutationAnalytics.Messaging;
using SalutationApp.Policies;
using SalutationApp.Requests;
using Salutations_Migrations.Migrations;
using ChannelFactory = Paramore.Brighter.MessagingGateway.RMQ.ChannelFactory;

IHost host = CreateHostBuilder(args).Build();
host.CheckDbIsUp();
host.MigrateDatabase();
host.CreateInbox();
host.CreateOutbox(HasBinaryMessagePayload());
await host.RunAsync();
return;

static void AddSchemaRegistryMaybe(IServiceCollection services, MessagingTransport messagingTransport)
{
    if (messagingTransport != MessagingTransport.Kafka) return;

    SchemaRegistryConfig schemaRegistryConfig = new() { Url = "http://localhost:8081" };
    CachedSchemaRegistryClient cachedSchemaRegistryClient = new(schemaRegistryConfig);
    services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);
}

static IHostBuilder CreateHostBuilder(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .ConfigureHostConfiguration(configurationBuilder =>
        {
            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
            configurationBuilder.AddJsonFile("appsettings.json", true);
            configurationBuilder.AddJsonFile($"appsettings.{GetEnvironment()}.json", true);
            configurationBuilder
                .AddEnvironmentVariables(
                    "ASPNETCORE_"); //NOTE: Although not web, we use this to grab the environment
            configurationBuilder.AddEnvironmentVariables("BRIGHTER_");
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
}

static void ConfigureBrighter(HostBuilderContext hostContext, IServiceCollection services)
{
    MessagingTransport messagingTransport =
        GetTransportType(hostContext.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT]);

    AddSchemaRegistryMaybe(services, messagingTransport);

    Subscription[] subscriptions = GetSubscriptions(messagingTransport);

    RelationalDatabaseConfiguration relationalDatabaseConfiguration = new(DbConnectionString(hostContext));
    services.AddSingleton<IAmARelationalDatabaseConfiguration>(relationalDatabaseConfiguration);

    RelationalDatabaseConfiguration outboxConfiguration = new(
        DbConnectionString(hostContext),
        binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
    );
    services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

    (IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
        OutboxExtensions.MakeOutbox(hostContext, GetDatabaseType(hostContext), outboxConfiguration, services);

    services.AddServiceActivator(options =>
        {
            options.Subscriptions = subscriptions;
            options.DefaultChannelFactory = GetChannelFactory(messagingTransport);
            options.UseScoped = true;
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Singleton;
            options.CommandProcessorLifetime = ServiceLifetime.Scoped;
            options.PolicyRegistry = new SalutationPolicy();
            options.InboxConfiguration = new InboxConfiguration(
                CreateInbox(hostContext, relationalDatabaseConfiguration),
                InboxScope.Commands
            );
        })
        .ConfigureJsonSerialisation(options =>
        {
            //We don't strictly need this, but added as an example
            options.PropertyNameCaseInsensitive = true;
        })
        .UseExternalBus(config =>
        {
            config.ProducerRegistry = ConfigureProducerRegistry(messagingTransport);
            config.Outbox = makeOutbox.outbox;
            config.ConnectionProvider = makeOutbox.connectionProvider;
            config.TransactionProvider = makeOutbox.transactionProvider;
            config.MaxOutStandingMessages = 5;
            config.MaxOutStandingCheckIntervalMilliSeconds = 500;
        })
        .AutoFromAssemblies();

    services.AddHostedService<ServiceActivatorHostedService>();
}

static void ConfigureMigration(HostBuilderContext hostBuilderContext, IServiceCollection services)
{
    //dev is always Sqlite
    if (hostBuilderContext.HostingEnvironment.IsDevelopment())
        ConfigureSqlite(hostBuilderContext, services);
    else
        ConfigureProductionDatabase(hostBuilderContext, GetDatabaseType(hostBuilderContext), services);
}

static void ConfigureProductionDatabase(
    HostBuilderContext hostBuilderContext,
    DatabaseType databaseType,
    IServiceCollection services)
{
    switch (databaseType)
    {
        case DatabaseType.MySql:
            ConfigureMySql(hostBuilderContext, services);
            break;
        case DatabaseType.MsSql:
            ConfigureMsSql(hostBuilderContext, services);
            break;
        case DatabaseType.Postgres:
            ConfigurePostgreSql(hostBuilderContext, services);
            break;
        case DatabaseType.Sqlite:
            ConfigureSqlite(hostBuilderContext, services);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
    }
}

static void ConfigureMySql(HostBuilderContext hostBuilderContext, IServiceCollection services)
{
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddMySql5()
            .WithGlobalConnectionString(DbConnectionString(hostBuilderContext))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigureMsSql(HostBuilderContext hostBuilderContext, IServiceCollection services)
{
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddSqlServer()
            .WithGlobalConnectionString(DbConnectionString(hostBuilderContext))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigurePostgreSql(HostBuilderContext hostBuilderContext, IServiceCollection services)
{
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddPostgres()
            .ConfigureGlobalProcessorOptions(opt => opt.ProviderSwitches = "Force Quote=false")
            .WithGlobalConnectionString(DbConnectionString(hostBuilderContext))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigureSqlite(HostBuilderContext hostBuilderContext, IServiceCollection services)
{
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c =>
        {
            c.AddSQLite()
                .WithGlobalConnectionString(DbConnectionString(hostBuilderContext))
                .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations();
        });
}

static void ConfigureDapper(HostBuilderContext hostBuilderContext, IServiceCollection services)
{
    ConfigureDapperByHost(GetDatabaseType(hostBuilderContext), services);
}

static void ConfigureDapperByHost(DatabaseType databaseType, IServiceCollection services)
{
    switch (databaseType)
    {
        case DatabaseType.Sqlite:
            ConfigureDapperSqlite(services);
            break;
        case DatabaseType.MySql:
            ConfigureDapperMySql(services);
            break;
        case DatabaseType.MsSql:
            ConfigureDapperMsSql(services);
            break;
        case DatabaseType.Postgres:
            ConfigureDapperPostgreSql(services);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
    }
}

static void ConfigureDapperSqlite(IServiceCollection services)
{
    services.AddScoped<IAmARelationalDbConnectionProvider, SqliteConnectionProvider>();
    services.AddScoped<IAmATransactionConnectionProvider, SqliteUnitOfWork>();
}

static void ConfigureDapperMySql(IServiceCollection services)
{
    services.AddScoped<IAmARelationalDbConnectionProvider, MySqlConnectionProvider>();
    services.AddScoped<IAmATransactionConnectionProvider, MySqlUnitOfWork>();
}

static void ConfigureDapperMsSql(IServiceCollection services)
{
    services.AddScoped<IAmARelationalDbConnectionProvider, MsSqlConnectionProvider>();
    services.AddScoped<IAmATransactionConnectionProvider, MsSqlUnitOfWork>();
}

static void ConfigureDapperPostgreSql(IServiceCollection services)
{
    services.AddScoped<IAmARelationalDbConnectionProvider, PostgreSqlConnectionProvider>();
    services.AddScoped<IAmATransactionConnectionProvider, PostgreSqlUnitOfWork>();
}

static IAmAnInbox CreateInbox(HostBuilderContext hostContext, IAmARelationalDatabaseConfiguration configuration)
{
    if (hostContext.HostingEnvironment.IsDevelopment()) return new SqliteInbox(configuration);

    return CreateProductionInbox(GetDatabaseType(hostContext), configuration);
}

static IAmAProducerRegistry ConfigureProducerRegistry(MessagingTransport messagingTransport)
{
    return messagingTransport switch
    {
        MessagingTransport.Rmq => GetRmqProducerRegistry(),
        MessagingTransport.Kafka => GetKafkaProducerRegistry(),
        _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport), "Messaging transport is not supported")
    };
}

static IAmAnInbox CreateProductionInbox(DatabaseType databaseType, IAmARelationalDatabaseConfiguration configuration)
{
    return databaseType switch
    {
        DatabaseType.Sqlite => new SqliteInbox(configuration),
        DatabaseType.MySql => new MySqlInbox(configuration),
        DatabaseType.MsSql => new MsSqlInbox(configuration),
        DatabaseType.Postgres => new PostgreSqlInbox(configuration),
        _ => throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported")
    };
}

static string DbConnectionString(HostBuilderContext hostContext)
{
    //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
    return hostContext.HostingEnvironment.IsDevelopment()
        ? GetDevDbConnectionString()
        : GetConnectionString(hostContext, GetDatabaseType(hostContext));
}

static DatabaseType GetDatabaseType(HostBuilderContext hostContext)
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

static IAmAChannelFactory GetChannelFactory(MessagingTransport messagingTransport)
{
    return messagingTransport switch
    {
        MessagingTransport.Rmq => GetRmqChannelFactory(),
        MessagingTransport.Kafka => GetKafkaChannelFactory(),
        _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport), "Messaging transport is not supported")
    };
}

static string GetEnvironment()
{
    //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
    return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
}

static string GetConnectionString(HostBuilderContext hostContext, DatabaseType databaseType)
{
    return databaseType switch
    {
        DatabaseType.MySql => hostContext.Configuration.GetConnectionString("SalutationsMySql"),
        DatabaseType.MsSql => hostContext.Configuration.GetConnectionString("SalutationsMsSql"),
        DatabaseType.Postgres => hostContext.Configuration.GetConnectionString("SalutationsPostgreSql"),
        DatabaseType.Sqlite => GetDevDbConnectionString(),
        _ => throw new InvalidOperationException("Could not determine the database type")
    };
}

static string GetDevDbConnectionString()
{
    return "Filename=Salutations.db;Cache=Shared";
}

static IAmAChannelFactory GetKafkaChannelFactory()
{
    return new Paramore.Brighter.MessagingGateway.Kafka.ChannelFactory(
        new KafkaMessageConsumerFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "paramore.brighter", BootStrapServers = new[] { "localhost:9092" }
            }
        )
    );
}

static IAmAProducerRegistry GetKafkaProducerRegistry()
{
    IAmAProducerRegistry producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "paramore.brighter.greetingsender", BootStrapServers = new[] { "localhost:9092" }
            },
            new KafkaPublication[]
            {
                new()
                {
                    Topic = new RoutingKey("SalutationReceived"),
                    RequestType = typeof(SalutationReceived),
                    MessageSendMaxRetries = 3,
                    MessageTimeoutMs = 1000,
                    MaxInFlightRequestsPerConnection = 1,
                    MakeChannels = OnMissingChannel.Create
                }
            })
        .Create();

    return producerRegistry;
}

static IAmAChannelFactory GetRmqChannelFactory()
{
    return new ChannelFactory(new RmqMessageConsumerFactory(new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
            Exchange = new Exchange("paramore.brighter.exchange")
        })
    );
}

static IAmAProducerRegistry GetRmqProducerRegistry()
{
    IAmAProducerRegistry producerRegistry = new RmqProducerRegistryFactory(
        new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
            Exchange = new Exchange("paramore.brighter.exchange")
        },
        new RmqPublication[]
        {
            new()
            {
                Topic = new RoutingKey("SalutationReceived"),
                RequestType = typeof(SalutationReceived),
                WaitForConfirmsTimeOutInMilliseconds = 1000,
                MakeChannels = OnMissingChannel.Create
            }
        }
    ).Create();
    return producerRegistry;
}

static Subscription[] GetRmqSubscriptions()
{
    Subscription[] subscriptions =
    {
        new RmqSubscription<GreetingMade>(
            new SubscriptionName("paramore.sample.salutationanalytics"),
            new ChannelName("SalutationAnalytics"),
            new RoutingKey("GreetingMade"),
            runAsync: true,
            timeoutInMilliseconds: 200,
            isDurable: true,
            makeChannels: OnMissingChannel
                .Create) //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
    };
    return subscriptions;
}

static Subscription[] GetSubscriptions(MessagingTransport messagingTransport)
{
    return messagingTransport switch
    {
        MessagingTransport.Rmq => GetRmqSubscriptions(),
        MessagingTransport.Kafka => GetKafkaSubscriptions(),
        _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport), "Messaging transport is not supported")
    };
}

static Subscription[] GetKafkaSubscriptions()
{
    KafkaSubscription[] subscriptions =
    {
        new KafkaSubscription<GreetingMade>(
            new SubscriptionName("paramore.sample.salutationanalytics"),
            new ChannelName("SalutationAnalytics"),
            new RoutingKey("GreetingMade"),
            "kafka-GreetingsReceiverConsole-Sample",
            timeoutInMilliseconds: 100,
            offsetDefault: AutoOffsetReset.Earliest,
            commitBatchSize: 5,
            sweepUncommittedOffsetsIntervalMs: 10000,
            runAsync: true,
            makeChannels: OnMissingChannel.Create)
    };
    return subscriptions;
}

static MessagingTransport GetTransportType(string brighterTransport)
{
    return brighterTransport switch
    {
        MessagingGlobals.RMQ => MessagingTransport.Rmq,
        MessagingGlobals.KAFKA => MessagingTransport.Kafka,
        _ => throw new ArgumentOutOfRangeException(nameof(MessagingGlobals.BRIGHTER_TRANSPORT),
            "Messaging transport is not supported")
    };
}

static bool HasBinaryMessagePayload()
{
    return GetTransportType(Environment.GetEnvironmentVariable("BRIGHTER_TRANSPORT")) == MessagingTransport.Kafka;
}

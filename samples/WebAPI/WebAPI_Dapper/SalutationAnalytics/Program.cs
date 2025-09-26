using System;
using System.IO;
using Confluent.SchemaRegistry;
using DbMaker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MySql;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Sqlite;
using SalutationApp.Policies;
using SalutationApp.Requests;
using TransportMaker;

var builder = CreateHostBuilder(args);

var host = builder.Build();

host.CheckDbIsUp(ApplicationType.Salutations);
host.MigrateDatabase();
host.CreateInbox("Salutations");
host.CreateOutbox(ApplicationType.Salutations, "Salutations", ConfigureTransport.HasBinaryMessagePayload());
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
        .ConfigureLogging((_, builder) =>
        {
            builder.AddConsole();
            builder.AddDebug();
        })
        .ConfigureServices((hostContext, services) =>
        {
            SalutationsDbFactory.ConfigureMigration(hostContext, services);
            ConfigureDapper(hostContext, services);
            ConfigureBrighter(hostContext, services);
            ConfigureObservability(services); 
        })
        .UseConsoleLifetime();
}

static void ConfigureBrighter(HostBuilderContext hostContext, IServiceCollection services)
{
    string? transport = hostContext.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
    if (string.IsNullOrWhiteSpace(transport))
        throw new InvalidOperationException("Transport is not set");

    MessagingTransport messagingTransport = ConfigureTransport.TransportType(transport);

    AddSchemaRegistryMaybe(services, messagingTransport);

    Subscription[] subscriptions = ConfigureTransport.GetSubscriptions<GreetingMade>(messagingTransport);

    string? dbType = hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
    if (string.IsNullOrWhiteSpace(dbType))
        throw new InvalidOperationException("DbType is not set");

    string? connectionString =
        ConnectionResolver.DbConnectionString(hostContext.Configuration, ApplicationType.Salutations);    

    RelationalDatabaseConfiguration relationalDatabaseConfiguration = new(connectionString);
    services.AddSingleton<IAmARelationalDatabaseConfiguration>(relationalDatabaseConfiguration);

    RelationalDatabaseConfiguration outboxConfiguration = new(
        connectionString,
        binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
    );
    services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

    Rdbms rdbms = DbResolver.GetDatabaseType(dbType);
    (IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
        OutboxFactory.MakeDapperOutbox(rdbms, outboxConfiguration);

    services.AddConsumers(options =>
        {
            options.Subscriptions = subscriptions;
            options.DefaultChannelFactory = ConfigureTransport.GetChannelFactory(messagingTransport);
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Singleton;
            options.PolicyRegistry = new SalutationPolicy();
            options.InboxConfiguration = new InboxConfiguration(
                InboxFactory.MakeInbox(rdbms, relationalDatabaseConfiguration),
                InboxScope.Commands
            );
        })
        .ConfigureJsonSerialisation(options =>
        {
            //We don't strictly need this, but added as an example
            options.PropertyNameCaseInsensitive = true;
        })
        .AddProducers(config =>
        {
            config.ProducerRegistry = ConfigureTransport.MakeProducerRegistry<SalutationReceived>(messagingTransport);
            config.Outbox = makeOutbox.outbox;
            config.ConnectionProvider = makeOutbox.connectionProvider;
            config.TransactionProvider = makeOutbox.transactionProvider;
            config.MaxOutStandingMessages = 5;
            config.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
        })
        .AutoFromAssemblies();

    services.AddHostedService<ServiceActivatorHostedService>();
}

static void ConfigureObservability(IServiceCollection services)
{
    services.AddLogging(loggingBuilder =>
    {
        loggingBuilder.AddConsole();
        loggingBuilder.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.AddConsoleExporter();
        });
    });

    services.AddOpenTelemetry()
        .ConfigureResource(builder =>
        {
            builder.AddService(
                serviceName: "SalutationAnalytics",
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                serviceInstanceId: Environment.MachineName);
        }).WithTracing(builder =>
        {
            builder
                .AddBrighterInstrumentation()
                .AddSource("RabbitMQ.Client.*")
                .SetTailSampler<AlwaysOffSampler>()
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter()
                .AddOtlpExporter(options =>
                {
                    options.Protocol = OtlpExportProtocol.Grpc;
                });
        }) 
        .WithMetrics(builder => builder
            .AddAspNetCoreInstrumentation()
            .AddBrighterInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter()
        );
    
}

static void ConfigureDapper(HostBuilderContext hostContext, IServiceCollection services)
{
    string? dbType = hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
    if (string.IsNullOrWhiteSpace(dbType))
        throw new InvalidOperationException("DbType is not set");

    ConfigureDapperByHost(DbResolver.GetDatabaseType(dbType), services);
}

static void ConfigureDapperByHost(Rdbms databaseType, IServiceCollection services)
{
    switch (databaseType)
    {
        case Rdbms.Sqlite:
            ConfigureDapperSqlite(services);
            break;
        case Rdbms.MySql:
            ConfigureDapperMySql(services);
            break;
        case Rdbms.MsSql:
            ConfigureDapperMsSql(services);
            break;
        case Rdbms.Postgres:
            ConfigureDapperPostgreSql(services);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
    }
}

static void ConfigureDapperSqlite(IServiceCollection services)
{
    services.AddScoped<IAmARelationalDbConnectionProvider, SqliteConnectionProvider>();
    services.AddScoped<IAmATransactionConnectionProvider, SqliteTransactionProvider>();
}

static void ConfigureDapperMySql(IServiceCollection services)
{
    services.AddScoped<IAmARelationalDbConnectionProvider, MySqlConnectionProvider>();
    services.AddScoped<IAmATransactionConnectionProvider, MySqlTransactionProvider>();
}

static void ConfigureDapperMsSql(IServiceCollection services)
{
    services.AddScoped<IAmARelationalDbConnectionProvider, MsSqlConnectionProvider>();
    services.AddScoped<IAmATransactionConnectionProvider, MsSqlTransactionProvider>();
}

static void ConfigureDapperPostgreSql(IServiceCollection services)
{
    services.AddScoped<IAmARelationalDbConnectionProvider, PostgreSqlConnectionProvider>();
    services.AddScoped<IAmATransactionConnectionProvider, PostgreSqlTransactionProvider>();
}

static string? GetEnvironment()
{
    //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
    return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
}

using System;
using System.IO;
using Confluent.SchemaRegistry;
using DbMaker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using SalutationApp.EntityGateway;
using SalutationApp.Policies;
using SalutationApp.Requests;
using TransportMaker;

var host = CreateHostBuilder(args).Build();
host.CheckDbIsUp(ApplicationType.Salutations);
host.MigrateDatabase();
host.CreateInbox("Salutations");
host.CreateOutbox(ApplicationType.Salutations,  "Salutations", HasBinaryMessagePayload());
await host.RunAsync();
return;

static void AddSchemaRegistryMaybe(IServiceCollection services, MessagingTransport messagingTransport)
{
    if (messagingTransport != MessagingTransport.Kafka) return;

    SchemaRegistryConfig schemaRegistryConfig = new() { Url = "http://localhost:8081" };
    CachedSchemaRegistryClient cachedSchemaRegistryClient = new(schemaRegistryConfig);
    services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);
}

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureHostConfiguration(configurationBuilder =>
        {
            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
            configurationBuilder.AddJsonFile("appsettings.json", optional: true);
            configurationBuilder.AddJsonFile($"appsettings.{GetEnvironment()}.json", optional: true);
            configurationBuilder
                .AddEnvironmentVariables(
                    prefix: "ASPNETCORE_"); //NOTE: Although not web, we use this to grab the environment
            configurationBuilder.AddEnvironmentVariables(prefix: "BRIGHTER_");
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
            ConfigureEFCore(hostContext, services);
            ConfigureBrighter(hostContext, services);
            ConfigureObservability(services);
        })
        .UseConsoleLifetime();

static void ConfigureBrighter(HostBuilderContext hostContext, IServiceCollection services)
{
    string? transport = hostContext.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
    if (string.IsNullOrWhiteSpace(transport))
        throw new InvalidOperationException("Transport is not set");

    MessagingTransport messagingTransport = ConfigureTransport.TransportType(transport);

    AddSchemaRegistryMaybe(services, messagingTransport);
    
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
    (IAmAnOutbox outbox, Type transactionProvider, Type connectionProvider)  makeOutbox =
        OutboxFactory.MakeEfOutbox<SalutationsEntityGateway>(rdbms, outboxConfiguration);

    IAmAProducerRegistry producerRegistry = ConfigureProducerRegistry();

    var subscriptions = new Subscription[]
    {
        new RmqSubscription<GreetingMade>(
            new SubscriptionName("paramore.sample.salutationanalytics"),
            new ChannelName("SalutationAnalytics"),
            new RoutingKey("GreetingMade"),
            messagePumpType: MessagePumpType.Proactor,
            timeOut: TimeSpan.FromMilliseconds(200),
            isDurable: true,
            makeChannels: OnMissingChannel
                .Create), //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
    };

    var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(new RmqMessagingGatewayConnection
    {
        AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
        Exchange = new Exchange("paramore.brighter.exchange"),
    });

    services.AddConsumers(options =>
        {
            options.Subscriptions = subscriptions;
            options.DefaultChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Singleton;
            options.PolicyRegistry = new SalutationPolicy();
            options.InboxConfiguration = new InboxConfiguration(
                InboxFactory.MakeInbox(rdbms, relationalDatabaseConfiguration),
                InboxScope.Commands
            );
        })
        .AddProducers((configure) =>
        {
            configure.ProducerRegistry = producerRegistry;
            configure.Outbox = makeOutbox.outbox;
            configure.TransactionProvider = makeOutbox.transactionProvider;
            configure.ConnectionProvider = makeOutbox.connectionProvider;
            configure.MaxOutStandingMessages = 5;
            configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
        })
        .AutoFromAssemblies();

    services.AddHostedService<ServiceActivatorHostedService>();
}


static string GetEnvironment()
{
    //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
    return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
           ?? throw new InvalidOperationException(" ASP_NETCORE_ENVIRONMENT is not set ");
}

static bool HasBinaryMessagePayload()
{
    string? brighterTransport = Environment.GetEnvironmentVariable("BRIGHTER_TRANSPORT");
    if (string.IsNullOrWhiteSpace(brighterTransport))
        throw new InvalidOperationException("Transport is not set");
    
    MessagingTransport? transport = ConfigureTransport.TransportType(brighterTransport);
    if (transport == null)
        throw new InvalidOperationException("Transport is not set");
    
    return transport == MessagingTransport.Kafka;
}

static void ConfigureEFCore(HostBuilderContext hostContext, IServiceCollection services)
{
    string? connectionString = ConnectionResolver.DbConnectionString(hostContext.Configuration, ApplicationType.Salutations);

    if (hostContext.HostingEnvironment.IsDevelopment())
    {
        services.AddDbContext<SalutationsEntityGateway>(
            builder =>
            {
                builder.UseSqlite(connectionString);
            });
    }
    else
    {
        services.AddDbContextPool<SalutationsEntityGateway>(builder =>
        {
            builder
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();
        });
    }
}

static IAmAProducerRegistry ConfigureProducerRegistry()
{
    var producerRegistry = new RmqProducerRegistryFactory(
        new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
            Exchange = new Exchange("paramore.brighter.exchange"),
        },
        new[]
        {
            new RmqPublication
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
}


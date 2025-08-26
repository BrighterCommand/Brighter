using Azure.Core;
using Azure.Identity;
using DbMaker;
using GreetingsApp.EntityGateway;
using GreetingsApp.Events;
using Microsoft.EntityFrameworkCore;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using TransportMaker;


IHost host = CreateHostBuilder(args).Build();

host.CheckDbIsUp(ApplicationType.Greetings);
host.MigrateDatabase();
host.CreateOutbox(ApplicationType.Greetings, ConfigureTransport.HasBinaryMessagePayload());

host.Run();
return;

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureHostConfiguration(configurationBuilder =>
        {
            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
            configurationBuilder.AddJsonFile("appsettings.json", optional: true);
            configurationBuilder.AddJsonFile($"appsettings.Development.json", optional: true);
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
            GreetingsDbFactory.ConfigureMigration(hostContext.Configuration, services);
            ConfigureEFCore(hostContext, services);
            ConfigureBrighter(hostContext, services);
        })
        .UseConsoleLifetime();

static void ConfigureEFCore(HostBuilderContext hostContext, IServiceCollection services)
{
    string? connectionString = ConnectionResolver.DbConnectionString(hostContext.Configuration, ApplicationType.Greetings);

    if (hostContext.HostingEnvironment.IsDevelopment())
    {
        services.AddDbContext<GreetingsEntityGateway>(
            builder =>
            {
                builder.UseSqlite(connectionString);
            });
    }
    else
    {
        services.AddDbContextPool<GreetingsEntityGateway>(builder =>
        {
            builder
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();
        });
    }
}

static void ConfigureBrighter(HostBuilderContext hostContext, IServiceCollection services)
{
    string? transport = hostContext.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
    if (string.IsNullOrWhiteSpace(transport))
        throw new InvalidOperationException("Transport is not set");

    MessagingTransport messagingTransport = MessagingTransport.Asb;
    var asbClientProvider = new ServiceBusVisualStudioCredentialClientProvider("recs-testing.servicebus.windows.net");
    var asbConsumerFactory = new AzureServiceBusConsumerFactory(asbClientProvider);
    TokenCredential[] credentials = [new VisualStudioCredential()];

    string? dbType = hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
    if (string.IsNullOrWhiteSpace(dbType))
        throw new InvalidOperationException("DbType is not set");

    string? connectionString =
        ConnectionResolver.DbConnectionString(hostContext.Configuration, ApplicationType.Greetings);

    RelationalDatabaseConfiguration relationalDatabaseConfiguration = new(connectionString);
    services.AddSingleton<IAmARelationalDatabaseConfiguration>(relationalDatabaseConfiguration);

    RelationalDatabaseConfiguration outboxConfiguration = new(
        connectionString,
        binaryMessagePayload: false
    );
    services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

    Rdbms rdbms = DbResolver.GetDatabaseType(dbType);
    (IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
        OutboxFactory.MakeEfOutbox<GreetingsEntityGateway>(rdbms, outboxConfiguration);

    IAmAProducerRegistry producerRegistry = new AzureServiceBusProducerRegistryFactory(
        asbClientProvider,
        [
            new AzureServiceBusPublication
            {
                Topic = new RoutingKey("greeting.event"),
            },
            new AzureServiceBusPublication
            {
                Topic = new RoutingKey("greeting.addGreetingCommand"),
            },
            new AzureServiceBusPublication
            {
                Topic = new RoutingKey("greeting.Asyncevent"),
            }
        ]
    ).Create();

    var subscriptions = new Subscription[]
    {
        new AzureServiceBusSubscription<GreetingAsyncEvent>(
            new SubscriptionName("Async Event"),
            new ChannelName("paramore.example.greeting"),
            new RoutingKey("greeting.Asyncevent"),
            timeOut: TimeSpan.FromMilliseconds(400),
            makeChannels: OnMissingChannel.Assume,
            requeueCount: 3,
            messagePumpType: MessagePumpType.Proactor),

        new AzureServiceBusSubscription<GreetingEvent>(
            new SubscriptionName("Event"),
            new ChannelName("paramore.example.greeting"),
            new RoutingKey("greeting.event"),
            timeOut: TimeSpan.FromMilliseconds(400),
            makeChannels: OnMissingChannel.Assume,
            requeueCount: 3,
            messagePumpType: MessagePumpType.Proactor)
    };

    services.AddConsumers(options =>
        {
            options.Subscriptions = subscriptions;
            options.DefaultChannelFactory = new AzureServiceBusChannelFactory(asbConsumerFactory);
            options.UseScoped = true;
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Singleton;
            options.CommandProcessorLifetime = ServiceLifetime.Scoped;
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

using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MySql;
using Paramore.Brighter.MySql.EntityFrameworkCore;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Sqlite;
using Paramore.Brighter.Sqlite.EntityFrameworkCore;
using SalutationAnalytics.Database;
using SalutationApp.EntityGateway;
using SalutationApp.Policies;
using SalutationApp.Requests;

var host = CreateHostBuilder(args).Build();
host.CheckDbIsUp();
host.MigrateDatabase();
host.CreateInbox();
host.CreateOutbox();
await host.RunAsync();
return;

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
        .ConfigureLogging((context, builder) =>
        {
            builder.AddConsole();
            builder.AddDebug();
        })
        .ConfigureServices((hostContext, services) =>
        {
            ConfigureEFCore(hostContext, services);
            ConfigureBrighter(hostContext, services);
        })
        .UseConsoleLifetime();

static void ConfigureBrighter(HostBuilderContext hostContext, IServiceCollection services)
{
    (IAmAnOutbox outbox, Type transactionProvider, Type connectionProvider) = MakeOutbox(hostContext);
    var outboxConfiguration = new RelationalDatabaseConfiguration(DbConnectionString(hostContext));
    services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

    IAmAProducerRegistry producerRegistry = ConfigureProducerRegistry();

    var subscriptions = new Subscription[]
    {
        new RmqSubscription<GreetingMade>(
            new SubscriptionName("paramore.sample.salutationanalytics"),
            new ChannelName("SalutationAnalytics"),
            new RoutingKey("GreetingMade"),
            runAsync: true,
            timeoutInMilliseconds: 200,
            isDurable: true,
            makeChannels: OnMissingChannel
                .Create), //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
    };

    var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(new RmqMessagingGatewayConnection
    {
        AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
        Exchange = new Exchange("paramore.brighter.exchange"),
    });

    services.AddServiceActivator(options =>
        {
            options.Subscriptions = subscriptions;
            options.DefaultChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
            options.UseScoped = true;
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Singleton;
            options.CommandProcessorLifetime = ServiceLifetime.Scoped;
            options.PolicyRegistry = new SalutationPolicy();
            options.InboxConfiguration = new InboxConfiguration(
                ConfigureInbox(hostContext),
                scope: InboxScope.Commands,
                onceOnly: true,
                actionOnExists: OnceOnlyAction.Throw
            );
        })
        .UseExternalBus((configure) =>
        {
            configure.ProducerRegistry = producerRegistry;
            configure.Outbox = outbox;
            configure.TransactionProvider = transactionProvider;
            configure.ConnectionProvider = connectionProvider;
            configure.MaxOutStandingMessages = 5;
            configure.MaxOutStandingCheckIntervalMilliSeconds = 500;
        })
        .AutoFromAssemblies();

    services.AddHostedService<ServiceActivatorHostedService>();
}


static string GetEnvironment()
{
    //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
    return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
}

static void ConfigureEFCore(HostBuilderContext hostContext, IServiceCollection services)
{
    string connectionString = DbConnectionString(hostContext);

    if (hostContext.HostingEnvironment.IsDevelopment())
    {
        services.AddDbContext<SalutationsEntityGateway>(
            builder =>
            {
                builder.UseSqlite(connectionString,
                    optionsBuilder =>
                    {
                        optionsBuilder.MigrationsAssembly("Salutations_SqliteMigrations");
                    });
            });
    }
    else
    {
        services.AddDbContextPool<SalutationsEntityGateway>(builder =>
        {
            builder
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), optionsBuilder =>
                {
                    optionsBuilder.MigrationsAssembly("Salutations_MySqlMigrations");
                })
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();
        });
    }
}

static IAmAnInbox ConfigureInbox(HostBuilderContext hostContext)
{
    if (hostContext.HostingEnvironment.IsDevelopment())
    {
        return new SqliteInbox(new RelationalDatabaseConfiguration(DbConnectionString(hostContext),
            SchemaCreation.INBOX_TABLE_NAME));
    }

    return new MySqlInbox(new RelationalDatabaseConfiguration(DbConnectionString(hostContext),
        SchemaCreation.INBOX_TABLE_NAME));
}

static IAmAProducerRegistry ConfigureProducerRegistry()
{
    var producerRegistry = new RmqProducerRegistryFactory(
        new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
            Exchange = new Exchange("paramore.brighter.exchange"),
        },
        new RmqPublication[]
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


static string DbConnectionString(HostBuilderContext hostContext)
{
    //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
    return hostContext.HostingEnvironment.IsDevelopment()
        ? "Filename=Salutations.db;Cache=Shared"
        : hostContext.Configuration.GetConnectionString("Salutations");
}

static (IAmAnOutbox outbox, Type transactionProvider, Type connectionProvider) MakeOutbox(
    HostBuilderContext hostContext)
{
    if (hostContext.HostingEnvironment.IsDevelopment())
    {
        var outbox = new SqliteOutbox(new RelationalDatabaseConfiguration(DbConnectionString(hostContext)));
        var transactionProvider = typeof(SqliteEntityFrameworkConnectionProvider<SalutationsEntityGateway>);
        var connectionProvider = typeof(SqliteConnectionProvider);
        return (outbox, transactionProvider, connectionProvider);
    }
    else
    {
        var outbox = new MySqlOutbox(new RelationalDatabaseConfiguration(DbConnectionString(hostContext)));
        var transactionProvider = typeof(MySqlEntityFrameworkConnectionProvider<SalutationsEntityGateway>);
        var connectionProvider = typeof(MySqlConnectionProvider);
        return (outbox, transactionProvider, connectionProvider);
    }
}

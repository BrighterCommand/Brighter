using Confluent.SchemaRegistry;
using DbMaker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using SalutationApp.EntityGateway;
using SalutationApp.Policies;
using SalutationApp.Requests;
using TransportMaker;

namespace SalutationAnalytics;

public static class Extension
{
   public static void ConfigureBrighter(this IServiceCollection services, ConfigurationManager configuration)
    {
        string? transport = configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
        if (string.IsNullOrWhiteSpace(transport))
            throw new InvalidOperationException("Transport is not set");

        MessagingTransport messagingTransport = ConfigureTransport.TransportType(transport);

        AddSchemaRegistryMaybe(services, messagingTransport);

        string? dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(dbType))
            throw new InvalidOperationException("DbType is not set");

        string? connectionString =
            ConnectionResolver.DbConnectionString(configuration, ApplicationType.Salutations);

        RelationalDatabaseConfiguration relationalDatabaseConfiguration = new(connectionString);
        services.AddSingleton<IAmARelationalDatabaseConfiguration>(relationalDatabaseConfiguration);

        RelationalDatabaseConfiguration outboxConfiguration = new(
            connectionString,
            binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
        );
        services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

        Rdbms rdbms = DbResolver.GetDatabaseType(dbType);
        (IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
            OutboxFactory.MakeEfOutbox<SalutationsEntityGateway>(rdbms, outboxConfiguration);

        var messagingConnectionSting = configuration.GetConnectionString("messaging");
        IAmAProducerRegistry producerRegistry = ConfigureProducerRegistry(messagingConnectionSting);

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
            AmpqUri = new AmqpUriSpecification(new Uri(messagingConnectionSting)),
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
                    InboxFactory.MakeInbox(rdbms, relationalDatabaseConfiguration),
                    InboxScope.Commands
                );
            })
            .UseExternalBus((configure) =>
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
   
    static void AddSchemaRegistryMaybe(IServiceCollection services, MessagingTransport messagingTransport)
    {
        if (messagingTransport != MessagingTransport.Kafka) return;

        SchemaRegistryConfig schemaRegistryConfig = new() { Url = "http://localhost:8081" };
        CachedSchemaRegistryClient cachedSchemaRegistryClient = new(schemaRegistryConfig);
        services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);
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

    public static void ConfigureEFCore(this IServiceCollection services, ConfigurationManager configuration, IHostEnvironment environment)
    {
        string? connectionString = configuration.GetConnectionString("Salutions");

        if (environment.IsDevelopment())
        {
            services.AddDbContext<SalutationsEntityGateway>(builder =>
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

    static IAmAProducerRegistry ConfigureProducerRegistry(string? messagingConnectionSting)
    {
        var producerRegistry = new RmqProducerRegistryFactory(
            new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri(messagingConnectionSting)),
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
}

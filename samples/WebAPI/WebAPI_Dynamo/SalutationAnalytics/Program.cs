using System;
using System.IO;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using DbMaker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.Inbox.DynamoDB.V4;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.Outbox.DynamoDB;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using SalutationApp.Entities;
using SalutationApp.Policies;
using SalutationApp.Requests;
using TransportMaker;


await CreateHostBuilder(args).Build().RunAsync();
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
        .ConfigureLogging((_, builder) =>
        {
            builder.AddConsole();
            builder.AddDebug();
        })
        .ConfigureServices((hostContext, services) =>
        {
            var credentials = CreateCredentials();
            IAmazonDynamoDB client = CreateAndRegisterClient(credentials, hostContext, services);
            ConfigureDynamo(client, services);
            ConfigureBrighter(client, hostContext, services);
        })
        .UseConsoleLifetime();

static void ConfigureBrighter(
    IAmazonDynamoDB dynamoDb,
    HostBuilderContext hostContext,
    IServiceCollection services)
{
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

    var host = hostContext.HostingEnvironment.IsDevelopment() ? "localhost" : "rabbitmq";

    var rmqConnection = new RmqMessagingGatewayConnection
    {
        AmpqUri = new AmqpUriSpecification(new Uri($"amqp://guest:guest@{host}:5672")),
        Exchange = new Exchange("paramore.brighter.exchange")
    };

    var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);

    var transport = hostContext.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
    if (string.IsNullOrWhiteSpace(transport))
        throw new InvalidOperationException("Transport is not set");
        
    MessagingTransport messagingTransport =
        ConfigureTransport.TransportType(transport);

    ConfigureTransport.AddSchemaRegistryMaybe(services, messagingTransport);
            
    var producerRegistry = ConfigureTransport.MakeProducerRegistry<GreetingMade>(messagingTransport); 

    services.AddConsumers(options =>
        {
            options.Subscriptions = subscriptions;
            options.DefaultChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Singleton;
            options.PolicyRegistry = new SalutationPolicy();
            options.InboxConfiguration = new InboxConfiguration(
                ConfigureInbox(dynamoDb),
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
        .AddProducers((configure) =>
            {
                configure.ProducerRegistry = producerRegistry;
                configure.Outbox = ConfigureOutbox(dynamoDb);
                configure.ConnectionProvider = typeof(DynamoDbUnitOfWork);
                configure.TransactionProvider = typeof(DynamoDbUnitOfWork);
                configure.MaxOutStandingMessages = 5;
                configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
            }
        )
        .AutoFromAssemblies();

    services.AddHostedService<ServiceActivatorHostedService>();
}

static void ConfigureDynamo(IAmazonDynamoDB dynamoDb, IServiceCollection services)
{
    DbFactory.CreateEntityStore<Salutation>(dynamoDb);
    OutboxFactory.MakeDynamoOutbox(dynamoDb);
    InboxFactory.CreateInbox<GreetingMade>(dynamoDb, services);
}

static IAmazonDynamoDB CreateAndRegisterClient(AWSCredentials credentials, HostBuilderContext hostBuilderContext,
    IServiceCollection services)
{
    if (hostBuilderContext.HostingEnvironment.IsDevelopment())
    {
        return CreateAndRegisterLocalClient(credentials, services);
    }

    return CreateAndRegisterRemoteClient();
}

static AWSCredentials CreateCredentials()
{
    return new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");
}

static IAmazonDynamoDB CreateAndRegisterLocalClient(AWSCredentials credentials, IServiceCollection services)
{
    var clientConfig = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };

    var dynamoDb = new AmazonDynamoDBClient(credentials, clientConfig);
    services.Add(new ServiceDescriptor(typeof(IAmazonDynamoDB), dynamoDb));

    var dynamoDbConfiguration = new DynamoDbConfiguration();
    services.Add(new ServiceDescriptor(typeof(DynamoDbConfiguration), dynamoDbConfiguration));

    return dynamoDb;
}

static IAmazonDynamoDB CreateAndRegisterRemoteClient()
{
    throw new NotImplementedException();
}

static IAmAnInbox ConfigureInbox(IAmazonDynamoDB dynamoDb)
{
    return new DynamoDbInbox(dynamoDb, new DynamoDbInboxConfiguration());
}

static IAmAnOutbox ConfigureOutbox(IAmazonDynamoDB dynamoDb)
{
    return new DynamoDbOutbox(dynamoDb, new DynamoDbConfiguration(), TimeProvider.System);
}

static string GetEnvironment()
{
    //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
    return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
}

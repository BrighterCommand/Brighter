using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using DbMaker;
using GreetingsApp.EntityGateway;
using GreetingsApp.Events;
using GreetingsWorker;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using TransportMaker;

JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

var asbClientProvider = new ServiceBusVisualStudioCredentialClientProvider("fim-development-bus.servicebus.windows.net");
var asbConsumerFactory = new AzureServiceBusConsumerFactory(asbClientProvider);
TokenCredential[] credentials = [new VisualStudioCredential()];
MessagingTransport messagingTransport = MessagingTransport.Asb;

var sqlhelper = new MySqlTestHelper();
sqlhelper.SetupMessageDb();

RelationalDatabaseConfiguration outboxConfiguration = new(
    sqlhelper._mysqlSettings.TestsBrighterConnectionString
);

builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);
builder.Services.AddHealthChecks();
builder.Services.ConfigureEfCore<GreetingsEntityGateway>(builder.Configuration);

var dbType = builder.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
if (string.IsNullOrEmpty(dbType))
    throw new ArgumentNullException("No database type specified in configuration");

var rdbms = DbResolver.GetDatabaseType(dbType);
(IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
    OutboxFactory.MakeEfOutbox<GreetingsEntityGateway>(rdbms, outboxConfiguration);

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

var producerRegistry = new AzureServiceBusProducerRegistryFactory(
        asbClientProvider,
        new AzureServiceBusPublication[]
        {
            new() { Topic = new RoutingKey("greeting.event"), MakeChannels = OnMissingChannel.Assume},
            new() { Topic = new RoutingKey("greeting.addGreetingCommand"), MakeChannels = OnMissingChannel.Assume },
            new() { Topic = new RoutingKey("greeting.Asyncevent"), MakeChannels = OnMissingChannel.Assume }
        }
    )
    .Create();

builder.Services.AddConsumers(options =>
{
    options.Subscriptions = subscriptions;
    options.DefaultChannelFactory = new AzureServiceBusChannelFactory(asbConsumerFactory);
    options.UseScoped = true;
});

builder.Services.AddBrighter(options =>
{
    options.InstrumentationOptions = InstrumentationOptions.All;

}).AddProducers(configure =>
{
    configure.ProducerRegistry = producerRegistry;
    configure.Outbox = makeOutbox.outbox;
    configure.TransactionProvider = makeOutbox.transactionProvider;
    configure.ConnectionProvider = makeOutbox.connectionProvider;
    configure.MaxOutStandingMessages = 5;
    configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
});

builder.Services.AddHostedService<ServiceActivatorHostedService>();

WebApplication app = builder.Build();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var content = new
        {
            Status = report.Status.ToString(),
            Results = report.Entries.ToDictionary(e => e.Key,
                e => new { Status = e.Value.Status.ToString(), e.Value.Description, e.Value.Duration }),
            report.TotalDuration
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(content, jsonOptions));
    }
});

app.Run();

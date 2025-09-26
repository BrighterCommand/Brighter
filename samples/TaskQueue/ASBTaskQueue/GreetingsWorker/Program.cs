using System.Text.Json;
using System.Text.Json.Serialization;
using Greetings.Adaptors.Data;
using Greetings.Adaptors.Services;
using Greetings.Ports.CommandHandlers;
using Greetings.Ports.Commands;
using Greetings.Ports.Events;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Control.Api;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();

builder.Services.AddScoped<InstanceCount>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddTransient(typeof(MonitoringAsyncHandler<>));

var subscriptionName = "paramore.example.worker";

var subscriptions = new Subscription[]
{
    new AzureServiceBusSubscription<GreetingAsyncEvent>(
        new SubscriptionName("Greeting Event"),
        new ChannelName(subscriptionName),
        new RoutingKey("greeting.event"),
        timeOut: TimeSpan.FromMilliseconds(400),
        makeChannels: OnMissingChannel.Assume,
        requeueCount: 3,
        messagePumpType: MessagePumpType.Proactor,
        noOfPerformers: 2, unacceptableMessageLimit: 1),
    new AzureServiceBusSubscription<GreetingEvent>(
        new SubscriptionName("Greeting Async Event"),
        new ChannelName(subscriptionName),
        new RoutingKey("greeting.Asyncevent"),
        timeOut: TimeSpan.FromMilliseconds(400),
        makeChannels: OnMissingChannel.Assume,
        requeueCount: 3,
        messagePumpType: MessagePumpType.Reactor,
        noOfPerformers: 2),
    new AzureServiceBusSubscription<AddGreetingCommand>(
        new SubscriptionName("Greeting Command"),
        new ChannelName(subscriptionName),
        new RoutingKey("greeting.addGreetingCommand"),
        timeOut: TimeSpan.FromMilliseconds(400),
        makeChannels: OnMissingChannel.Assume,
        requeueCount: 3,
        messagePumpType: MessagePumpType.Reactor,
        noOfPerformers: 2)
};

string dbConnString = "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password1!;Application Name=BrighterTests;MultipleActiveResultSets=True";
            
//EF
builder.Services.AddDbContext<GreetingsDataContext>(o =>
{
    o.UseSqlServer(dbConnString);
});

var clientProvider = new ServiceBusConnectionStringClientProvider("Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");

var asbConsumerFactory = new AzureServiceBusConsumerFactory(clientProvider);
builder.Services.AddConsumers(options =>
    {
        options.Subscriptions = subscriptions;
        options.DefaultChannelFactory = new AzureServiceBusChannelFactory(asbConsumerFactory);
        
    })
    .AutoFromAssemblies();

builder.Services.AddHostedService<ServiceActivatorHostedService>();
                
builder.Logging.SetMinimumLevel(LogLevel.Information).AddConsole();


builder.Services.AddRouting();
builder.Services.AddHealthChecks()
    .AddCheck<BrighterServiceActivatorHealthCheck>("Brighter", HealthStatus.Unhealthy);

var app = builder.Build();

app.UseRouting();
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

jsonOptions.Converters.Add(new JsonStringConverter());
jsonOptions.Converters.Add(new JsonStringEnumConverter());

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var content = new
        {
            Status = report.Status.ToString(),
            Results = report.Entries.ToDictionary(e => e.Key, e => new
            {
                Status = e.Value.Status.ToString(),
                e.Value.Description,
                e.Value.Duration
            }),
            report.TotalDuration
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(content, jsonOptions));
    }
});
app.MapBrighterControlEndpoints();

app.Map("Dispatcher", (IDispatcher dispatcher) => { return dispatcher.Consumers.Count(); });

app.Run();

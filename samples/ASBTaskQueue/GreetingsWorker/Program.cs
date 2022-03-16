using System.Text.Json;
using System.Text.Json.Serialization;
using Greetings.Adaptors.Data;
using Greetings.Adaptors.Services;
using Greetings.Ports.CommandHandlers;
using Greetings.Ports.Commands;
using Greetings.Ports.Events;
using Greetings.Ports.Mappers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MsSql.EntityFrameworkCore;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.HealthChecks;
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
        new SubscriptionName(GreetingEventAsyncMessageMapper.Topic),
        new ChannelName(subscriptionName),
        new RoutingKey(GreetingEventAsyncMessageMapper.Topic),
        timeoutInMilliseconds: 400,
        makeChannels: OnMissingChannel.Create,
        requeueCount: 3,
        isAsync: true,
        noOfPerformers: 2, unacceptableMessageLimit: 1),
    new AzureServiceBusSubscription<GreetingEvent>(
        new SubscriptionName(GreetingEventMessageMapper.Topic),
        new ChannelName(subscriptionName),
        new RoutingKey(GreetingEventMessageMapper.Topic),
        timeoutInMilliseconds: 400,
        makeChannels: OnMissingChannel.Create,
        requeueCount: 3,
        isAsync: false,
        noOfPerformers: 2),
    new AzureServiceBusSubscription<AddGreetingCommand>(
        new SubscriptionName(AddGreetingMessageMapper.Topic),
        new ChannelName(subscriptionName),
        new RoutingKey(AddGreetingMessageMapper.Topic),
        timeoutInMilliseconds: 400,
        makeChannels: OnMissingChannel.Create,
        requeueCount: 3,
        isAsync: true,
        noOfPerformers: 2)
};

string dbConnString = "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password1!;Application Name=BrighterTests;MultipleActiveResultSets=True";
            
//EF
builder.Services.AddDbContext<GreetingsDataContext>(o =>
{
    o.UseSqlServer(dbConnString);
});

var outboxConfig = new MsSqlConfiguration(dbConnString, "BrighterOutbox");

//TODO: add your ASB qualified name here
var clientProvider = new ServiceBusVisualStudioCredentialClientProvider(".servicebus.windows.net");

var asbConsumerFactory = new AzureServiceBusConsumerFactory(clientProvider, false);
builder.Services.AddServiceActivator(options =>
    {
        options.Subscriptions = subscriptions;
        options.ChannelFactory = new AzureServiceBusChannelFactory(asbConsumerFactory);
        options.UseScoped = true;
        
    }).UseMsSqlOutbox(outboxConfig, typeof(MsSqlSqlAuthConnectionProvider))
    .UseMsSqlTransactionConnectionProvider(typeof(MsSqlEntityFrameworkCoreConnectionProvider<GreetingsDataContext>))
    .AutoFromAssemblies();

builder.Services.AddHostedService<ServiceActivatorHostedService>();
                
builder.Logging.SetMinimumLevel(LogLevel.Information).AddConsole();


builder.Services.AddRouting();
builder.Services.AddHealthChecks()
    .AddCheck<BrighterServiceActivatorHealthCheck>("Brighter", HealthStatus.Unhealthy);

var app = builder.Build();

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    jsonOptions.Converters.Add(new JsonStringConverter());
    jsonOptions.Converters.Add(new JsonStringEnumConverter());

    endpoints.MapHealthChecks("/health");
    endpoints.MapHealthChecks("/health/detail", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            var content = new
            {
                Status = report.Status.ToString(),
                Results = report.Entries.ToDictionary(e => e.Key, e => new
                {
                    Status = e.Value.Status.ToString(),
                    Description = e.Value.Description,
                    Duration = e.Value.Duration
                }),
                TotalDuration = report.TotalDuration
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(content, jsonOptions));
        }
    });
    
    endpoints.Map("Dispatcher", (IDispatcher dispatcher) => { return dispatcher.Consumers.Count(); });
});

app.Run();

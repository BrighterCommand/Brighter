using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orders.Data;
using Orders.Domain;
using Orders.Domain.Commands;
using Orders.Domain.Events;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();

builder.Services.AddScoped<SqlConnectionProvider, SqlConnectionProvider>();
builder.Services.AddScoped<IUnitOfWork, SqlUnitOfWork>();
builder.Services.AddScoped<SqlUnitOfWork, SqlUnitOfWork>();


builder.Services.AddTransient<IOrderRepository, OrderRepository>();

var subscriptionName = "paramore.example.worker";

var subscriptions = new Subscription[]
{
    new AzureServiceBusSubscription<NewOrderVersionEvent>(
        new SubscriptionName(NewOrderVersionEvent.Topic),
        new ChannelName(subscriptionName),
        new RoutingKey(NewOrderVersionEvent.Topic),
        timeoutInMilliseconds: 400,
        makeChannels: OnMissingChannel.Create,
        requeueCount: 3,
        isAsync: true,
        noOfPerformers: 2, unacceptableMessageLimit: 1)
};

string dbConnString = "Server=127.0.0.1,11433;Database=BrighterOrderTests;User Id=sa;Password=Password1!;Application Name=BrighterTests;MultipleActiveResultSets=True;encrypt=false";
            


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
    .UseMsSqlTransactionConnectionProvider(typeof(SqlConnectionProvider))
    .AutoFromAssemblies(Assembly.GetAssembly(typeof(CreateOrderCommand)));

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


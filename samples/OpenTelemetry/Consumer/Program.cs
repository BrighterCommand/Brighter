using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Shared.Commands;
using OpenTelemetry.Shared.Events;
using OpenTelemetry.Shared.Handlers;
using OpenTelemetry.Shared.Helpers;
using OpenTelemetry.Shared.Mappers;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

Paramore.Brighter.Logging.ApplicationLogging.LoggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        //options.add
    });
});

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ConsumerService"))
    .AddSource("Paramore.Brighter.ServiceActivator", "Brighter")
    .AddJaegerExporter()
    .Build();

var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
    Exchange = new Exchange("paramore.brighter.exchange")
};

var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);

var producerRegistry = Helpers.GetProducerRegistry(rmqConnection);

builder.Services.AddServiceActivator(options =>
    {
        options.Subscriptions = new Subscription[]
        {
            new RmqSubscription<MyDistributedEvent>(
                new SubscriptionName("Consumer"),
                new ChannelName("MyDistributedEvent"),
                new RoutingKey("MyDistributedEvent")
            ),
            new RmqSubscription<UpdateProductCommand>(
                new SubscriptionName("Consumer"),
                new ChannelName("UpdateProductCommand"),
                new RoutingKey("UpdateProductCommand")
            ),
            new RmqSubscription<ProductUpdatedEvent>(
                new SubscriptionName("Consumer"),
                new ChannelName("ProductUpdatedEvent"),
                new RoutingKey("ProductUpdatedEvent"),
                requeueCount: 5
            )
        };
        options.ChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
    })
    .MapperRegistry(r =>
    {
        r.Register<MyDistributedEvent, MessageMapper<MyDistributedEvent>>();
        r.Register<UpdateProductCommand, MessageMapper<UpdateProductCommand>>();
        r.Register<ProductUpdatedEvent, MessageMapper<ProductUpdatedEvent>>();
    })
    .Handlers(r =>
    {
        r.Register<MyDistributedEvent, MyDistributedEventHandler>();
        r.Register<UpdateProductCommand, UpdateProductCommandHandler>();
        r.Register<ProductUpdatedEvent, ProductUpdatedEventHandler>();
    })
    .UseExternalBus((configure) =>
    {
        configure.ProducerRegistry = producerRegistry;
    })
    .UseOutboxSweeper(options =>
    {
        options.TimerInterval = 30;
        options.MinimumMessageAge = 500;
    });


builder.Services.AddSingleton<TopicDictionary>();
builder.Services.AddSingleton(typeof(IAmAMessageMapper<>), typeof(MessageMapper<>));

builder.Services.AddHealthChecks()
    .AddCheck<BrighterServiceActivatorHealthCheck>("Brighter", HealthStatus.Unhealthy);

builder.Services.AddHostedService<ServiceActivatorHostedService>();

var  app = builder.Build();

app.UseRouting();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var content = new
        {
            Status = report.Status.ToString(),
            Results = report.Entries.ToDictionary(e => e.Key,
                e => new
                {
                    Status = e.Value.Status.ToString(),
                    Description = e.Value.Description,
                    Duration = e.Value.Duration
                }),
            TotalDuration = report.TotalDuration
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(content, JsonSerialisationOptions.Options));
    }
});

app.Run();

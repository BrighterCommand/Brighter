using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Shared.Commands;
using OpenTelemetry.Shared.Events;
using OpenTelemetry.Shared.Helpers;
using OpenTelemetry.Shared.Mappers;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;

var builder = WebApplication.CreateBuilder(args);

using var tracerProvider = 
    Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ProducerService"))
    .AddSource("Paramore.Brighter*", "Microsoft.*")
    .AddJaegerExporter(o =>
    {
        o.Endpoint = new Uri("http://localhost:9411/api/v2/spans");
    })
    .Build();

var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
    Exchange = new Exchange("paramore.brighter.exchange"),
};

builder.Services.AddBrighter(options =>
    {
        options.CommandProcessorLifetime = ServiceLifetime.Scoped;
    })
    .UseExternalBus(Helpers.GetProducerRegistry(rmqConnection))
    .UseInMemoryOutbox()
    .MapperRegistry(r =>
    {
        r.Register<MyDistributedEvent, MessageMapper<MyDistributedEvent>>();
        r.Register<ProductUpdatedEvent, MessageMapper<ProductUpdatedEvent>>();
        r.Register<UpdateProductCommand, MessageMapper<UpdateProductCommand>>();
    });

builder.Services.AddSingleton<TopicDictionary>();
builder.Services.AddSingleton(typeof(IAmAMessageMapper<>), typeof(MessageMapper<>));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapGet("/Send/{message}", (string message, IAmACommandProcessor commandProcessor) =>
{
    var dEvent = new MyDistributedEvent(message);
    var messageId = commandProcessor.DepositPost(dEvent);
    commandProcessor.ClearOutbox(messageId);

    return $"Sent Message {message} at {DateTime.Now}";
});
app.MapGet("/product/{name}", (string name, IAmACommandProcessor commandProcessor) =>
{
    var dEvent = new UpdateProductCommand(name);
    var messageId = commandProcessor.DepositPost(dEvent);
    commandProcessor.ClearOutbox(messageId);

    return $"Command Message {name} sent at {DateTime.Now}";
});

app.Run();

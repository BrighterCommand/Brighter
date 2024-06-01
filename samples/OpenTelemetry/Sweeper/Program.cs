// See https://aka.ms/new-console-template for more information

using System.Transactions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;

const string topic = "Test.Topic";

Console.WriteLine("Hello, World!");

var builder = WebApplication.CreateBuilder(args);

Paramore.Brighter.Logging.ApplicationLogging.LoggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        //options.add
    });
});

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Brighter Sweeper Sample"))
    .AddSource("Paramore.Brighter")
    .AddJaegerExporter()
    .Build();

IAmAProducerRegistry producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
{
    {"default", new InMemoryProducer(new InternalBus(), TimeProvider.System){Publication = { Topic  = new RoutingKey(topic)}}}
});

var requestContextFactory = new InMemoryRequestContextFactory();

builder.Services.AddBrighter()
    .UseExternalBus((configure) =>
    {
        configure.ProducerRegistry = producerRegistry;
        configure.RequestContextFactory = requestContextFactory;
    })
    .UseOutboxSweeper(options =>
    {
        options.TimerInterval = 5;
        options.MinimumMessageAge = 500;
    });

var app = builder.Build();

var outBox = app.Services.GetService<IAmAnOutboxSync<Message, CommittableTransaction>>();
if (outBox == null)
    throw new InvalidOperationException("Outbox is null");

outBox.Add(
    new Message(
        new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND, timeStamp:DateTime.UtcNow),
        new MessageBody("Hello")),
    requestContextFactory.Create()
    );

app.Run();


﻿// See https://aka.ms/new-console-template for more information

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Sweeper.Doubles;

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
    // .AddZipkinExporter(o => o.HttpClientFactory = () =>
    // {
    //     HttpClient client = new HttpClient();
    //     client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value");
    //     return client;
    //     o.Endpoint = new Uri("http://localhost:9411/api/v2/spans");
    // })
    .AddJaegerExporter()
    .Build();

IAmAProducerRegistry producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
{
    {"default", new FakeMessageProducer()}
});

builder.Services.AddBrighter()
    .UseExternalBus(producerRegistry)
    .UseInMemoryOutbox()
    .UseOutboxSweeper(options =>
    {
        options.TimerInterval = 5;
        options.MinimumMessageAge = 500;
    });

var app = builder.Build();

var outBox = app.Services.GetService<IAmAnOutboxSync<Message>>();
outBox.Add(new Message(new MessageHeader(Guid.NewGuid(), "Test.Topic", MessageType.MT_COMMAND, DateTime.UtcNow),
    new MessageBody("Hello")));

app.Run();


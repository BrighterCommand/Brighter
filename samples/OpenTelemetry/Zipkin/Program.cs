// See https://aka.ms/new-console-template for more information

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Zipkin.Doubles;

Console.WriteLine("Hello, World!");

var builder = WebApplication.CreateBuilder(args);

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Brighter Zipkin Sample"))
    .AddSource("Brighter")
    .AddZipkinExporter(o => o.HttpClientFactory = () =>
    {
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value");
        return client;
        o.Endpoint = new Uri("http://localhost:9411/api/v2/spans");
    })
    .Build();

IAmAProducerRegistry producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>()
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

app.Run();

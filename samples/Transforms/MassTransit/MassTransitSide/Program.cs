using System.Reflection;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services
    .AddMassTransit(bus =>
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        bus.SetKebabCaseEndpointNameFormatter();
        bus.AddConsumers(entryAssembly);

        bus.UsingAmazonSqs((context, cfg) =>
        {
            cfg.Host("us-east-1", h =>
            {
                h.AccessKey("test");
                h.SecretKey("test");

                h.Config(new AmazonSQSConfig { ServiceURL = "http://localhost:4566", });

                h.Config(new AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = "http://localhost:4566",
                });
            });

            cfg.Message<Greeting>(m =>
            {
                m.SetEntityName("masstransit-topic");
            });

            cfg.ReceiveEndpoint("masstransit-queue", e =>
            {
                e.Subscribe("brighter-topic", x =>
                {
                });

                e.ConfigureConsumer<GreetingConsumer>(context);
            });

            cfg.Publish((IAmazonSqsMessagePublishTopologyConfigurator<Greeting> p)=>
            {
                p.TopicTags.Add("Source", "Brighter");
            });

            cfg.ConfigureEndpoints(context);
        });
    });

var host = builder.Build();

await host.StartAsync();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) => cts.Cancel();

while (!cts.IsCancellationRequested)
{
    Console.Write("Say your name: ");
    var name = Console.ReadLine();

    if (string.IsNullOrEmpty(name))
    {
        continue;
    }

    using var scope = host.Services.CreateScope();
    var publish = scope.ServiceProvider.GetRequiredService<IBus>();
    await publish.Publish(new Greeting { Name = name });
}

await host.StopAsync();


[MessageUrn("greeting")]
public record Greeting
{
    public string Name { get; init; }
}


public class GreetingConsumer(ILogger<GreetingConsumer> logger) : IConsumer<Greeting>
{
    public Task Consume(ConsumeContext<Greeting> context)
    {
        logger.LogInformation("Hello {Name} ", context.Message.Name);
        return Task.CompletedTask;
    }
}

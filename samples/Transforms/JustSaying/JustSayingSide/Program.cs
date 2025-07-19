using Amazon;
using JustSaying;
using JustSaying.Messaging;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureLogging(builder => builder.AddConsole())
    .ConfigureServices(services =>
    {
        services
            .AddHostedService<BusService>()
            .AddJustSayingHandler<Greeting, GreetingHandler>()
            .AddJustSaying(cfg =>
            {
                cfg
                    .Client(client => client.WithServiceUrl("http://localhost:4566")
                        .WithBasicCredentials("test", "test"))
                    .Messaging(messaging => messaging.WithRegion(RegionEndpoint.USEast1))
                    .Publications(pub => pub.WithTopic<Greeting>(topic => topic.WithTag("Source", "Brighter")))
                    .Subscriptions(sub => sub.ForTopic<Greeting>(topic => topic.WithQueueName("justsaying-greeting")));
            });
    })
    .Build();

await host.StartAsync();

var cts = new CancellationTokenSource();
Console.CancelKeyPress  += (_,_) => cts.Cancel();

while (!cts.IsCancellationRequested)
{
    Console.Write("Say your name: ");
    var name = Console.ReadLine();

    if (string.IsNullOrEmpty(name))
    {
        continue;
    }

    using var scope = host.Services.CreateScope();
    var publish = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
    await publish.PublishAsync(new Greeting{ Name = name });
}

await host.StopAsync();

public class Greeting : Message
{
    public string Name { get; set; } = string.Empty;
}

public class FarewellViaSns : Message
{
    public string Name { get; set; } = string.Empty;
}

public class GreetingHandler(ILogger<GreetingHandler> logger) : IHandlerAsync<Greeting>
{
    public Task<bool> Handle(Greeting message)
    {
        logger.LogInformation("Hello {Name} (Tenant: {Tenant})", message.Name, message.Tenant);
        return Task.FromResult(true);
    }
}

public class BusService(IMessagingBus bus, ILogger<BusService> logger, IMessagePublisher publisher)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Bus is starting up...");

        await publisher.StartAsync(stoppingToken);
        await bus.StartAsync(stoppingToken);
    }
}

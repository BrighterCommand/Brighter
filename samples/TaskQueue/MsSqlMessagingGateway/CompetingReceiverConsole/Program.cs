using System;
using System.Threading;
using System.Threading.Tasks;
using CompetingReceiverConsole;
using Events;
using Events.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var subscriptions = new Subscription[]
{
    new Subscription<CompetingConsumerCommand>(
        new SubscriptionName("paramore.example.multipleconsumer.command"),
        new ChannelName("multipleconsumer.command"),
        new RoutingKey("multipleconsumer.command"),
        timeOut: TimeSpan.FromMilliseconds(200))
};

var messagingConfiguration = new RelationalDatabaseConfiguration(
    @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;",
    databaseName: "BrighterSqlQueue",
    queueStoreTable: "QueueData");
var messageConsumerFactory = new MsSqlMessageConsumerFactory(messagingConfiguration);

builder.Services.AddConsumers(options =>
{
    options.Subscriptions = subscriptions;
    options.DefaultChannelFactory = new ChannelFactory(messageConsumerFactory);
}).AutoFromAssemblies();

builder.Services.AddHostedService<ServiceActivatorHostedService>();
builder.Services.AddHostedService<RunStuff>();

builder.Services.AddSingleton<IAmACommandCounter, CommandCounter>();

var host = builder.Build();
await host.RunAsync();

internal sealed class RunStuff : IHostedService
{
    private readonly IAmACommandCounter _commandCounter;

    public RunStuff(IAmACommandCounter commandCounter)
    {
        _commandCounter = commandCounter;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"There were {_commandCounter.Counter} commands handled by this consumer");

        await Task.CompletedTask;
    }
}

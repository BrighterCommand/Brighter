using System;
using System.Threading;
using System.Threading.Tasks;
using CompetingReceiverConsole;
using Events.Ports.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using SampleInfrastructure;

var builder = Host.CreateApplicationBuilder(args);

var subscriptions = new Subscription[]
{
    // MsSqlSubscription, NOT Subscription: the MSSQL ChannelFactory casts what it is given
    // down to MsSqlSubscription and throws ConfigurationException when the cast fails.
    new MsSqlSubscription<CompetingConsumerCommand>(
        new SubscriptionName("paramore.example.multipleconsumer.command"),
        new ChannelName(SampleDatabase.CompetingTopic),
        new RoutingKey(SampleDatabase.CompetingTopic),
        timeOut: TimeSpan.FromMilliseconds(200),
        // Reactor, because CompetingConsumerCommandHandler is a sync RequestHandler<T>. The
        // default is Proactor, which calls SendAsync and finds an empty async chain — an Error
        // under Brighter's own ConsumerValidationRules.PumpHandlerMatch.
        messagePumpType: MessagePumpType.Reactor)
};

var connectionString = SampleDatabase.ConnectionString(
    builder.Configuration.GetConnectionString("Brighter"));

QueueTableProvisioner.EnsureQueueTable(connectionString, SampleDatabase.QueueTable);

var messagingConfiguration = new RelationalDatabaseConfiguration(
    connectionString,
    queueStoreTable: SampleDatabase.QueueTable);
var messageConsumerFactory = new MsSqlMessageConsumerFactory(messagingConfiguration);

builder.Services.AddConsumers(options =>
{
    options.Subscriptions = subscriptions;
    options.DefaultChannelFactory = new ChannelFactory(messageConsumerFactory);
})
// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
// Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
.UseScheduler(new InMemorySchedulerFactory())
.AutoFromAssemblies([typeof(CompetingConsumerCommand).Assembly])
// Surfaces a pump/handler mismatch as a named startup error rather than a per-message failure.
.ValidatePipelines();

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

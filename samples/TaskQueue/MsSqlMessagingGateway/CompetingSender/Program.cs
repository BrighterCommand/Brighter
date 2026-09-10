using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Events;
using Events.Ports.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.MsSql;
using SampleInfrastructure;

if (args.Length != 1)
{
    Console.WriteLine("usage: MultipleSender <count>");
    Console.WriteLine("eg   : MultipleSender 500");
    return;
}

if (!int.TryParse(args[0], out int repeatCount))
{
    Console.WriteLine($"{args[0]} is not a valid number");
    return;
}

var builder = Host.CreateApplicationBuilder(args);

//create the gateway
var connectionString = SampleDatabase.ConnectionString(
    builder.Configuration.GetConnectionString("Brighter"));

QueueTableProvisioner.EnsureQueueTable(connectionString, SampleDatabase.QueueTable);

var messagingConfiguration = new RelationalDatabaseConfiguration(
    connectionString,
    queueStoreTable: SampleDatabase.QueueTable);

var producerRegistry = new MsSqlProducerRegistryFactory(
        messagingConfiguration,
        // A Publication with no Topic throws ConfigurationException from
        // MsSqlMessageProducerFactory.Create(); the routing key must match the subscription
        // CompetingReceiverConsole declares.
        [new Publication<CompetingConsumerCommand>
        {
            Topic = new RoutingKey(SampleDatabase.CompetingTopic)
        }])
    .Create();

builder.Services.AddBrighter()
    // InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
    // Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
    .UseScheduler(new InMemorySchedulerFactory())
    .AddProducers((configure) =>
    {
        configure.ProducerRegistry = producerRegistry;
    })
    .AutoFromAssemblies();

builder.Services.AddHostedService<RunCommandProcessor>(provider => new RunCommandProcessor(provider.GetRequiredService<IAmACommandProcessor>(), repeatCount));

var host = builder.Build();
await host.RunAsync();

internal sealed class RunCommandProcessor : IHostedService
{
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly int _repeatCount;

    public RunCommandProcessor(IAmACommandProcessor commandProcessor, int repeatCount)
    {
        _commandProcessor = commandProcessor;
        _repeatCount = repeatCount;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (new TransactionScope(TransactionScopeOption.RequiresNew,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled))
        {
            Console.WriteLine($"Sending {_repeatCount} command messages");
            var sequenceNumber = 1;
            for (int i = 0; i < _repeatCount; i++)
            {
                _commandProcessor.Post(new CompetingConsumerCommand(sequenceNumber++));
            }
            // We do NOT complete the transaction here to show that a message is
            // always queued, whether the transaction commits or aborts!
        }

        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

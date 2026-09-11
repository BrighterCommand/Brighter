using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
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
    .AutoFromAssemblies([typeof(CompetingConsumerCommand).Assembly])
    // Producer-side validation of what has been registered above, so a misconfigured publication
    // fails at startup rather than on the first send.
    .ValidatePipelines();

builder.Services.AddHostedService<RunCommandProcessor>(provider => new RunCommandProcessor(
    provider.GetRequiredService<IAmACommandProcessor>(),
    provider.GetRequiredService<IHostApplicationLifetime>(),
    repeatCount));

var host = builder.Build();
await host.RunAsync();

internal sealed class RunCommandProcessor : BackgroundService
{
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly int _repeatCount;

    public RunCommandProcessor(IAmACommandProcessor commandProcessor, IHostApplicationLifetime lifetime, int repeatCount)
    {
        _commandProcessor = commandProcessor;
        _lifetime = lifetime;
        _repeatCount = repeatCount;
    }

    // ExecuteAsync rather than StartAsync, and the Yield is what makes that true: without an
    // await, BackgroundService.StartAsync runs this to completion inside the call that starts
    // the host.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        // Post opens a SqlConnection and Enlist defaults to true, so each insert joins this
        // transaction: an abandoned scope rolls the messages back with it.
        // An explicit timeout: every Post is a round trip, and the shipped profile sends 250 of
        // them inside this one scope. The default of 60 seconds is reachable against a remote
        // container, and it aborts with a TransactionAbortedException that explains nothing.
        using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew,
            new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromMinutes(5)
            },
            TransactionScopeAsyncFlowOption.Enabled))
        {
            Console.WriteLine($"Sending {_repeatCount} command messages");
            var sequenceNumber = 1;
            for (int i = 0; i < _repeatCount && !stoppingToken.IsCancellationRequested; i++)
            {
                _commandProcessor.Post(new CompetingConsumerCommand(sequenceNumber++));
            }

            // Only on a full run: completing a cancelled one would commit however many messages
            // it reached, which is not what Ctrl-C means.
            if (!stoppingToken.IsCancellationRequested)
                scope.Complete();
        }

        // Nothing left to do: stop rather than leaving the reader to find Ctrl-C.
        _lifetime.StopApplication();
    }
}

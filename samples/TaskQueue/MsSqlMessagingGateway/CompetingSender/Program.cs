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
    // Producer-side validation: RequestType set and implementing IRequest, wrap transforms
    // resolvable. A missing Topic is not among them — MsSqlProducerRegistryFactory.Create throws
    // that above, before AddBrighter is reached.
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

        // The scope must complete: Post opens a SqlConnection, Enlist defaults to true, so the
        // insert joins this transaction and an abandoned scope rolls the message back with it.
        // See the README, and GreetingsSender for the Outbox answer.
        using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled))
        {
            Console.WriteLine($"Sending {_repeatCount} command messages");
            var sequenceNumber = 1;
            for (int i = 0; i < _repeatCount && !stoppingToken.IsCancellationRequested; i++)
            {
                _commandProcessor.Post(new CompetingConsumerCommand(sequenceNumber++));
            }

            scope.Complete();
        }

        // Nothing left to do: stop rather than leaving the reader to find Ctrl-C.
        _lifetime.StopApplication();
    }
}

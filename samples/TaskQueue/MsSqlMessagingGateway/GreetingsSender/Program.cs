using Events.Ports.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Serilog;
using Serilog.Extensions.Logging;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory());

// One configuration object names the tables this process uses: the queue the transport writes
// to, and the Outbox. They live in the same database, which is the point of this sample.
// GreetingsReceiverConsole builds the matching configuration for the queue and the Inbox —
// the connection string and the table names have to agree across the two files.
// The default is the SQLEXPRESS instance this sample was written against. Set
// ConnectionStrings:Brighter (appsettings or the environment) to point it somewhere else —
// a container, say — without editing this file.
var connectionString = builder.Configuration.GetConnectionString("Brighter")
    ?? @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;";

var configuration = new RelationalDatabaseConfiguration(
    connectionString,
    databaseName: "BrighterSqlQueue",
    outBoxTableName: "Outbox",
    queueStoreTable: "QueueData");

// AddProducers is given MsSqlTransactionProvider as a TYPE, so the container activates it, and
// its constructor asks for exactly this interface. Without this line the first attempt to
// resolve a command processor throws, naming a type this file never mentions.
builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(configuration);

builder.Services.AddBrighter()
    // InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
    // Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
    .UseScheduler(new InMemorySchedulerFactory())
    .AddProducers((configure) =>
    {
        configure.ProducerRegistry = new MsSqlProducerRegistryFactory(
                configuration,
                [new Publication{Topic = new RoutingKey("greeting.event"), RequestType = typeof(GreetingEvent)}]
            )
            .Create();

        // Without these three the message goes straight to the queue. With them it lands in the
        // Outbox first, so it can share a transaction with your own write.
        configure.Outbox = new MsSqlOutbox(configuration);
        configure.ConnectionProvider = typeof(MsSqlConnectionProvider);
        configure.TransactionProvider = typeof(MsSqlTransactionProvider);
    })
    // Registers a hosted service that creates and migrates the Outbox table. It only runs when
    // the HOST starts — see StartAsync below. The QUEUE table is not covered: the MSSQL gateway
    // has no provisioning path, so OnMissingChannel.Create is inert there and
    // BrighterSqlQueue.sql is what creates it. MsSqlQueueBuilder gives the same DDL from code.
    .UseBoxProvisioning(options => options.AddMsSqlOutbox(configuration))
    .AutoFromAssemblies();

var host = builder.Build();

// StartAsync rather than RunAsync, because we have work to do between starting the host and
// waiting on it. Starting is what runs the box provisioning above, so it has to happen before
// the send rather than after.
await host.StartAsync();

var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();

// DepositPost writes to the Outbox and sends nothing. In a real handler this call shares a
// transaction with your own write, so the row and the message commit together or not at all.
var messageId = commandProcessor.DepositPost(new GreetingEvent("Ian"));

// ClearOutbox then dispatches it onto the queue. A long-running host would let the Outbox
// Sweeper (UseOutboxSweeper) do this on a timer instead.
commandProcessor.ClearOutbox([messageId]);

await host.StopAsync();

using Events.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
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

namespace GreetingsSender
{
    static class Program
    {
        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory());

            // One configuration object names all three tables: the queue the transport reads
            // and writes, the Outbox, and the Inbox the receiver de-duplicates against. They
            // all live in the same database, which is the point of this sample.
            var configuration = new RelationalDatabaseConfiguration(
                @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;",
                databaseName: "BrighterSqlQueue",
                outBoxTableName: "Outbox",
                inboxTableName: "InboxMessages",
                queueStoreTable: "QueueData");

            // The transaction provider needs this registration: AddProducers is given
            // MsSqlTransactionProvider as a TYPE, so the container activates it, and its
            // constructor asks for exactly this interface.
            serviceCollection.AddSingleton<IAmARelationalDatabaseConfiguration>(configuration);

            serviceCollection.AddBrighter()
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

                    // Without these three the message goes straight to the queue. With them it
                    // lands in the Outbox first, so it can share a transaction with your own write.
                    configure.Outbox = new MsSqlOutbox(configuration);
                    configure.ConnectionProvider = typeof(MsSqlConnectionProvider);
                    configure.TransactionProvider = typeof(MsSqlTransactionProvider);
                })
                // Creates and migrates the Outbox and Inbox tables at startup. The QUEUE table is
                // not covered: the MSSQL transport has no provisioning path, so BrighterSqlQueue.sql
                // creates that one. See MsSqlQueueBuilder for the same DDL from code.
                .UseBoxProvisioning(options =>
                {
                    options.AddMsSqlOutbox(configuration);
                    options.AddMsSqlInbox(configuration);
                })
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetRequiredService<IAmACommandProcessor>();

            // DepositPost writes to the Outbox and sends nothing. In a real handler this call
            // shares a transaction with your own write, so the row and the message commit
            // together or not at all.
            var messageId = commandProcessor.DepositPost(new GreetingEvent("Ian"));

            // ClearOutbox then dispatches it onto the queue. A long-running host would let the
            // Outbox Sweeper (UseOutboxSweeper) do this on a timer instead.
            commandProcessor.ClearOutbox([messageId]);
        }
    }
}

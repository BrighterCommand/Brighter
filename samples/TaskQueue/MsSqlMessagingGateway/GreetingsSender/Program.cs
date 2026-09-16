#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;
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
using SampleInfrastructure;
using Serilog;
using Serilog.Events;

// Information, not Debug: MsSqlMessageQueue logs its connection string at Debug, and the string
// this sample tells you to use carries a password. The gateway is overridden rather than the whole
// application so Brighter's own Debug output is still available where it is harmless.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Paramore.Brighter.MessagingGateway.MsSql", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.AddSerilog();

    var connectionString = SampleDatabase.ConnectionString(
        builder.Configuration.GetConnectionString("Brighter"));

    // One object, both the tables this process uses. GreetingsReceiverConsole builds the
    // matching pair for the queue and the Inbox from the same constants.
    var configuration = new RelationalDatabaseConfiguration(
        connectionString,
        outBoxTableName: SampleDatabase.OutboxTable,
        queueStoreTable: SampleDatabase.QueueTable);

    // AddProducers is given MsSqlTransactionProvider as a TYPE, so the container activates it,
    // and its constructor asks for exactly this interface.
    builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(configuration);

    builder.Services.AddBrighter()
        // InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
        // Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
        .UseScheduler(new InMemorySchedulerFactory())
        .AddProducers((configure) =>
        {
            configure.ProducerRegistry = new MsSqlProducerRegistryFactory(
                    configuration,
                    [new Publication<GreetingEvent> { Topic = new RoutingKey(SampleDatabase.GreetingTopic) }]
                )
                .Create();

            // Without these three the message goes straight to the queue. With them it lands in
            // the Outbox first, so it can share a transaction with your own write.
            configure.Outbox = new MsSqlOutbox(configuration);
            configure.ConnectionProvider = typeof(MsSqlConnectionProvider);
            configure.TransactionProvider = typeof(MsSqlTransactionProvider);
        })
        // Creates and migrates the Outbox table — but only once the HOST starts, because it
        // registers a hosted service. See StartAsync below.
        .UseBoxProvisioning(options => options.AddMsSqlOutbox(configuration))
        // Naming Events guarantees it is scanned whatever the load order. The list is additive,
        // not an allow-list: loaded assemblies are scanned as well.
        .AutoFromAssemblies([typeof(GreetingEvent).Assembly])
        // Producer-side validation of what has been registered above, so a misconfigured
        // publication fails at startup rather than on the first send. Last in the chain, as its
        // doc asks.
        .ValidatePipelines();

    using var host = builder.Build();

    // StartAsync rather than RunAsync, because we have work to do between starting the host and
    // waiting on it. Starting is what runs the box provisioning above, so it has to happen
    // before the send rather than after.
    await host.StartAsync();

    try
    {
        // From a scope, not the root provider: AddProducers registers the transaction provider
        // Transient, so resolving it at the root leaves a disposable tracked until shutdown — and
        // in an application where it is scoped to a DbContext, the scope is the only correct place.
        using var scope = host.Services.CreateScope();
        var commandProcessor = scope.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
        var transactionProvider = scope.ServiceProvider.GetRequiredService<IAmATransactionConnectionProvider>();

        // The transaction has to be opened here for the Outbox write to join it: the Outbox
        // attaches one only when the provider already has an open transaction, so passing the
        // provider without opening one deposits on its own auto-committed connection and shares
        // nothing.
        Id messageId;
        try
        {
            // Inside the try: GetTransaction opens the connection and begins the transaction, so
            // a failure here still has to reach the Close below.
            transactionProvider.GetTransaction();

            // DepositPost writes to the Outbox and sends nothing. Your own INSERT would go here,
            // on transactionProvider.GetConnection() and the same transaction, so the message and
            // the state it describes commit or roll back together.
            messageId = commandProcessor.DepositPost(
                new GreetingEvent("Ian"), transactionProvider);

            // Through the provider, not the raw DbTransaction: Commit clears the provider's
            // transaction, which is what keeps the Rollback below a no-op.
            transactionProvider.Commit();
        }
        catch
        {
            transactionProvider.Rollback();
            throw;
        }
        finally
        {
            transactionProvider.Close();
        }

        // Outside the try: the commit has happened, so a dispatch failure here must not reach a
        // rollback. Rolling back a committed transaction throws over the real exception and loses
        // it. A long-running host would let the Outbox Sweeper (UseOutboxSweeper) dispatch instead.
        commandProcessor.ClearOutbox([messageId]);
    }
    finally
    {
        await host.StopAsync();
    }
}
finally
{
    await Log.CloseAndFlushAsync();
}

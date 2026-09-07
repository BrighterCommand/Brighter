#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using GreetingsSenderWithOutbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Paramore.Brighter;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;

const string connectionString =
    "Host=localhost;Port=5432;Username=postgres;Password=password;Database=brightertests";

// The Greeting table is yours: your schema, your migrations, your problem. The two Brighter
// tables below are Brighter's. Do not let the three blur.
await CreateGreetingTableAsync(connectionString);

// The pivot of this sample. One configuration object names both Brighter tables, because the
// queue store and the Outbox are two parameters on the same type. That is the whole reason a
// single PostgreSQL database can be broker and Outbox at once: there is nothing to reconcile.
// Both names below are the defaults, spelled out because naming them is the point.
var configuration = new RelationalDatabaseConfiguration(
    connectionString,
    outBoxTableName: "Outbox",
    queueStoreTable: "Queue");

// The transport takes the same object, wrapped. PostgresMessagingGatewayConnection is a
// holder — it adds no settings of its own.
var connection = new PostgresMessagingGatewayConnection(configuration);

// Naming GreetingEvent here is also what loads the Greetings assembly, which AutoFromAssemblies
// below needs to have happened already. MakeChannels.Create is what creates the queue store
// table, so the Outbox table is provisioned by UseBoxProvisioning and the queue table by the
// transport itself — two routes, one database.
var producerRegistry = new PostgresProducerRegistryFactory(
    connection,
    [
        new PostgresPublication<GreetingEvent>
        {
            Topic = new RoutingKey("greeting.event"),
            MakeChannels = OnMissingChannel.Create
        }
    ]).Create();

var builder = Host.CreateApplicationBuilder(args);

// Easy to leave out, and the failure is nowhere near the omission. TransactionProvider below
// is given as a *type*, so the container activates PostgreSqlTransactionProvider, and its
// constructor asks for exactly this interface. Without this line the host starts, provisions
// the Outbox and only then fails, on the first attempt to resolve a command processor.
builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(configuration);

builder.Services
    .AddBrighter()
    .AddProducers(configure =>
    {
        configure.ProducerRegistry = producerRegistry;

        // These three make the Outbox durable and, more to the point, make it share the
        // handler's transaction. The transaction provider is the important one: it is what
        // lets the handler hand Brighter a transaction the handler itself opened.
        configure.Outbox = new PostgreSqlOutbox(configuration);
        configure.ConnectionProvider = typeof(PostgreSqlConnectionProvider);
        configure.TransactionProvider = typeof(PostgreSqlTransactionProvider);
    })
    .AutoFromAssemblies()

    // Creates and migrates the Outbox table at startup. It owns that table and nothing else.
    .UseBoxProvisioning(options => options.AddPostgreSqlOutbox(configuration))

    // The Sweeper: a hosted service that wakes on a timer, finds undispatched messages in the
    // Outbox and sends them — here, into the queue store table in the same database. Both
    // values are the defaults, spelled out because the delay they produce is visible.
    .UseOutboxSweeper(options =>
    {
        options.TimerInterval = 5;
        options.MinimumMessageAge = TimeSpan.FromSeconds(5);
    });

var host = builder.Build();

// StartAsync rather than RunAsync, because we have work to do between starting the host and
// waiting on it. Starting is what provisions the Outbox table and starts the Sweeper, so it
// has to happen before the send rather than after.
await host.StartAsync();

var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();
var failBeforeCommit = args.Contains("--fail");

// The failing run says something different so you can prove it is absent afterwards rather
// than counting rows.
var greeting = failBeforeCommit ? "This greeting will not survive" : "Hello from the sender";

try
{
    await commandProcessor.SendAsync(new AddGreeting(greeting, failBeforeCommit));

    Console.WriteLine("Committed. The greeting and the message are both in PostgreSQL.");
    Console.WriteLine("Waiting for the Sweeper to move it to the queue table. Ctrl+C to stop.");
}
catch (Exception e)
{
    Console.WriteLine($"Rolled back: {e.Message}");
    Console.WriteLine("Neither the greeting nor the message was written. Ctrl+C to stop.");
}

await host.WaitForShutdownAsync();

// Your table, created by your code. Plain ADO.NET: this is deliberately not going through
// Brighter, because it is not Brighter's table.
static async Task CreateGreetingTableAsync(string connection)
{
    await using var postgres = new NpgsqlConnection(connection);
    await postgres.OpenAsync();

    await using DbCommand command = postgres.CreateCommand();
    command.CommandText =
        """
        create table if not exists Greeting (
            Id      serial primary key,
            Message text not null
        )
        """;

    await command.ExecuteNonQueryAsync();
}

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
using Greetings;
using GreetingsSender;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Paramore.Brighter;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;

const string connectionString =
    "Host=localhost;Port=5432;Username=postgres;Password=password;Database=brightertests";

// Two tables live in this database and they have different owners. Greeting is yours: your
// schema, your migrations, your problem, and the line below is the whole of this sample's
// answer to that. The Outbox table is Brighter's, and UseBoxProvisioning creates it further
// down. Do not let the two blur — Brighter does not manage your schema.
await CreateGreetingTableAsync(connectionString);

// The Outbox and the provisioner both need to know where the database is and what the table
// is called. RelationalDatabaseConfiguration defaults the name to "Outbox".
var outboxConfiguration = new RelationalDatabaseConfiguration(connectionString);

var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
    Exchange = new Exchange("paramore.brighter.exchange")
};

// Unchanged from rung 2: the publication says where GreetingEvent goes. Naming the type here
// is also what loads the Greetings assembly, which AutoFromAssemblies below needs to have
// happened already — moving this line beneath the registration is a silent no-op.
var producerRegistry = new RmqProducerRegistryFactory(
    rmqConnection,
    [
        new RmqPublication<GreetingEvent>
        {
            Topic = new RoutingKey("greeting.event"),
            MakeChannels = OnMissingChannel.Create
        }
    ]).Create();

var builder = Host.CreateApplicationBuilder(args);

// The configuration goes into the container as well as into the two calls below, and this
// line is easy to leave out. TransactionProvider is given as a *type*, so the container
// activates PostgreSqlTransactionProvider, and its constructor asks for exactly this
// interface. Without it the host starts, provisions the Outbox and only then fails, on the
// first attempt to resolve a command processor.
builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

builder.Services
    .AddBrighter()
    .AddProducers(configure =>
    {
        configure.ProducerRegistry = producerRegistry;

        // Rung 2 had none of these three lines and got an in-memory Outbox by default: good
        // enough to make Post work, gone the moment the process is. These three make it
        // durable. The transaction provider is the important one — it is what lets the
        // handler hand Brighter a transaction that the handler itself opened.
        configure.Outbox = new PostgreSqlOutbox(outboxConfiguration);
        configure.ConnectionProvider = typeof(PostgreSqlConnectionProvider);
        configure.TransactionProvider = typeof(PostgreSqlTransactionProvider);
    })
    .AutoFromAssemblies()

    // Creates and migrates the Outbox table at startup, before anything else runs. It owns
    // that table and nothing else; the Greeting table above was yours to create. This needs
    // rights to CREATE TABLE, which the Docker Postgres has and your production database
    // very likely does not — see the Box Provisioning page for the alternative.
    .UseBoxProvisioning(options => options.AddPostgreSqlOutbox(outboxConfiguration))

    // The Sweeper: a hosted service that wakes on a timer, finds undispatched messages in the
    // Outbox and sends them. Both values below are the defaults, spelled out because the
    // delay they produce is the thing this rung teaches — a message is picked up on the first
    // tick after it is MinimumMessageAge old, so expect five to ten seconds.
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
// than counting rows: a table that still holds one greeting is only interesting if you can
// see which greeting it is.
var greeting = failBeforeCommit ? "This greeting will not survive" : "Hello from the sender";

try
{
    await commandProcessor.SendAsync(new AddGreeting(greeting, failBeforeCommit));

    Console.WriteLine("Committed. The greeting and the message are both in Postgres.");
    Console.WriteLine("Waiting for the Sweeper to dispatch it. Ctrl+C to stop.");
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

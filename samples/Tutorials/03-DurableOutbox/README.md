# 03 — Adding a Durable Outbox

The sample behind rung 3 of the *Get Started* tutorial ladder in the
[Brighter documentation](https://brightercommand.gitbook.io/paramore-brighter-documentation).
Every code block on the page *Adding a Durable Outbox* is this code, so please keep the two
in step: if you change a file here, the tutorial page needs the same edit. CI can prove this
compiles; nothing can prove the page still matches it.

This is [`02-FirstMessage`](../02-FirstMessage) with a durable Outbox. A greeting is written
to a business table and the message announcing it is written to the Outbox — both in one
Postgres transaction — and the Outbox Sweeper dispatches it to RabbitMQ a few seconds later.

## Running it

From the repository root, start both containers:

```bash
docker compose -f docker-compose-postgres.yaml up -d
docker compose -f docker-compose-rmq.yaml up -d
```

Neither command waits for its service to accept connections — the compose files have no
healthchecks — so give them a few seconds. RabbitMQ is ready when
<http://localhost:15672> answers.

Then, in two terminals, the receiver first — it is still the process that declares the queue
and the binding:

```bash
dotnet run --project samples/Tutorials/03-DurableOutbox/GreetingsReceiver
```

```bash
dotnet run --project samples/Tutorials/03-DurableOutbox/GreetingsSender
```

The sender prints `Committed.` almost at once and then waits. About ten seconds later the
receiver prints `Received: Hello from the sender`. **That gap is the point of this sample**:
the send is not on the request path any more. `DepositPostAsync` stored the message in
Postgres and returned; the Sweeper found it on a later tick and sent it. Ctrl+C stops either
process.

The delay is `TimerInterval` and `MinimumMessageAge` in `Program.cs`, five seconds each: a
message is dispatched on the first tick that finds it old enough. You do not have to take
that on trust — the Outbox row records both moments:

```bash
docker exec -it $(docker ps -qf name=postgres) psql -U postgres -d brightertests \
  -c 'select timestamp, dispatched, dispatched - timestamp as sweep_delay from Outbox;'
```

Both tables are worth looking at:

```bash
docker exec -it $(docker ps -qf name=postgres) \
  psql -U postgres -d brightertests -c 'select * from Greeting;' \
                   -c 'select messageid, topic, dispatched from Outbox;'
```

`Greeting` is the sample's own table. `Outbox` is Brighter's, and `dispatched` stays null
until the Sweeper sends the message — run that query within the first few seconds and you
will catch it null, which is the durable Outbox doing its job in one column.

### The interesting run

```bash
dotnet run --project samples/Tutorials/03-DurableOutbox/GreetingsSender -- --fail
```

The handler throws after writing the greeting *and* depositing the message, and before the
commit. Query both tables afterwards: there is no `This greeting will not survive` row in
`Greeting` and no new row in `Outbox`. Neither write survived, because they were never two
writes — they were one transaction.

One thing that will look odd: `Greeting.Id` skips a number after a failed run, because
Postgres allocates from the sequence before the rollback and does not give it back. Nothing
was lost; ids are not a count.

When you are done:

```bash
docker compose -f docker-compose-rmq.yaml down
docker compose -f docker-compose-postgres.yaml down -v
```

## What this adds to rung 2, and where

Every difference is in `GreetingsSender`. `Greetings/GreetingEvent.cs` and the whole of
`GreetingsReceiver` are byte-for-byte the files from `02-FirstMessage`, so `diff -r` against
that directory shows exactly what a durable Outbox costs and nothing else.

| Added | Why |
|---|---|
| `AddGreeting` + `AddGreetingHandlerAsync` | Rung 2's sender called `Post` straight from `Main`. A transaction needs somewhere to live, and a handler is where Brighter puts one |
| `Outbox`, `ConnectionProvider`, `TransactionProvider` on `AddProducers` | Rung 2 took the default in-memory Outbox. These three make it Postgres |
| `AddSingleton<IAmARelationalDatabaseConfiguration>` | `TransactionProvider` is given as a *type*, so the container activates it, and its constructor asks for this. Leave it out and the host starts, provisions the Outbox, and fails on the first resolve of a command processor |
| `UseBoxProvisioning(o => o.AddPostgreSqlOutbox(cfg))` | Creates and migrates the Outbox table at startup, so there is no second terminal and no migration project |
| `UseOutboxSweeper(...)` | Hosts the Sweeper in this process. It is a `IHostedService`, which is why the sender now runs a host instead of building one and throwing it away |
| `CreateGreetingTableAsync` | The `Greeting` table. **Brighter does not create this** — see below |

## Two tables, two owners

`UseBoxProvisioning` creates the **Outbox** table and nothing else. `Greeting` is the
application's table and the application creates it, in plain ADO.NET at the bottom of
`Program.cs`. The two sit in one database and are committed by one transaction, which
makes it easy to assume Brighter manages both. It does not, and a reader who believes it does
will go looking for a migration feature that is not there.

Box Provisioning also needs rights to `CREATE TABLE`, which the Docker Postgres here has and
a production database very often does not. It is one of two options; the other is to create
the Outbox table yourself from the supplied DDL.

## Why `ClearOutboxAsync` is missing

`samples/WebAPI/WebAPI_Dapper` calls `ClearOutboxAsync` immediately after the commit, which
dispatches the message there and then instead of waiting for the Sweeper. That is usually the
right trade — but it hides the thing this rung is teaching. Leaving it out makes the Outbox
visible: you can watch the row sit undispatched and then go.

## Why the sender writes with plain ADO.NET

The reference samples use Dapper, and Dapper is a fine choice. This one uses
`DbCommand` directly so that `command.Transaction = transaction` is on the page. The whole
claim of this rung is that your write and Brighter's write share a transaction, and an
explicit assignment argues that better than an extension method that takes `tx` as its third
argument.

## Where this sits on the ladder

This rung and [rung 4](../04-Kafka) are each one delta from
[rung 2](../02-FirstMessage) rather than from each other: this one keeps RabbitMQ and adds
durability, rung 4 keeps the shape and swaps the transport for Kafka. Neither depends on the
other, so they can be read in either order.

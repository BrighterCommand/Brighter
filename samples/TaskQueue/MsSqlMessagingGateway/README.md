# MSSQL Task Queue

SQL Server as the message broker, with the Outbox and the Inbox in the same database.

Four applications share one database: a queue table the transport reads and writes, an Outbox the
sender deposits into, and an Inbox the receiver de-duplicates against.

## Setup is two steps, and the split is deliberate

**1. Create the database.** It is the one thing that cannot create itself:

```bash
sqlcmd -S localhost,1433 -U sa -P '<password>' -C -i BrighterSqlQueue.sql
```

**A note on that password before you paste it:** an `export` puts it in your shell history and in
the environment of every child process, and `sqlcmd -P` puts it in the process table. For anything
but a throwaway local container, `SQLCMDPASSWORD` and `dotnet user-secrets` are the better homes.

**Unless you are on Windows with SQL Express, point the applications at that same server** — the
built-in default is `Server=.\sqlexpress;Integrated Security=SSPI`, which off Windows fails with a
`Named Pipes Provider` error:

```bash
export ConnectionStrings__Brighter='Server=localhost,1433;Database=BrighterSqlQueue;User Id=sa;Password=<password>;Encrypt=True;TrustServerCertificate=True'
```

**2. Run the applications.** Each creates the tables it owns, on every start:

| Table | Created by |
|---|---|
| `QueueData` | the MSSQL gateway, from `OnMissingChannel.Create` on the subscription and the publication |
| `Outbox` | Box Provisioning, in the sender |
| `InboxMessages` | Box Provisioning, in the receiver |

**The queue table needs no code in this sample.** `OnMissingChannel.Create` is the default on
`MsSqlSubscription` and on `Publication`, and the gateway acts on it as it opens a channel or builds
a producer — the same point in the lifecycle as the PostgreSQL gateway. Set `OnMissingChannel.Validate`
to have a missing table fail at startup instead, or `OnMissingChannel.Assume` where the DDL is
someone else's job.

An earlier version of this sample carried a `QueueTableProvisioner` of its own, because the gateway
accepted `OnMissingChannel.Create` and then ignored it. That code is now in the gateway, where it
can be tested.

`SampleInfrastructure` holds `SampleDatabase` — the connection string and the table names.
**`Events` is contracts only**: the commands and their mappers, which every application needs, with
no database dependency and no handlers.

**Handlers live with the process that owns their dependencies**, and both of them earn it:

- `GreetingEventHandler` carries `[UseInbox]`, a policy only `GreetingsReceiverConsole` configures.
- `CompetingConsumerCommandHandler` takes `IAmACommandCounter`, which only
  `CompetingReceiverConsole` registers.

That is not tidiness. `AutoFromAssemblies` registers every handler it finds, and
`Host.CreateApplicationBuilder` turns on `ValidateOnBuild` when `DOTNET_ENVIRONMENT=Development` —
so with the counter-dependent handler in the shared project, running *`GreetingsReceiverConsole`*
from an IDE profile fails at `host.Build()` with **`Unable to resolve service for type
'IAmACommandCounter'`**, in an application that has nothing to do with competing consumers.

**Each application names `Events` when it scans** — `AutoFromAssemblies([typeof(GreetingEvent).Assembly])`
rather than the no-argument overload. That **guarantees** `Events` is scanned whatever the load
order; it is not an allow-list, and every loaded assembly is still scanned as well. Without it the
sample works only as long as something happens to have forced `Events` to load first. It is not
what fixes the trap above — **only the handler move does that**, precisely because everything
loaded gets scanned regardless.

Because both the sender and the receiver provision what they need, **either can be started
first** against a database with no tables in it.

## Pointing it at something other than SQL Express

Every application reads `ConnectionStrings:Brighter`, falling back to the local SQL Express
instance, so the `export` in step 1 above is all it takes to run against a container — nothing
needs editing.

**`TrustServerCertificate=True` does not belong in production**, and nor does the `Encrypt=false`
you will meet in older examples. They are not the same concession: `Encrypt=false` drops TLS
altogether, while `TrustServerCertificate=True` keeps the encryption and skips only the check that
the certificate belongs to the server you meant to reach. The second is the lesser of the two,
which is why both the default connection string and the export above use it — a local SQL Express
instance and a bare container both present a self-signed certificate. Against a server with a
certificate your clients trust, drop it.

## The applications

| Run | With | What you should see |
|---|---|---|
| `GreetingsSender` | `GreetingsReceiverConsole` | the sender provisions the Outbox, deposits and clears; the receiver prints the greeting once. Redelivery of the same id is de-duplicated, but you have to force one — see *Idempotence* |
| `CompetingSender <count>` | two or more `CompetingReceiverConsole` | the count divided between the consumers — the split is arbitrary, and six messages across two receivers came out 3 and 3, 5 and 1, and 2 and 4 on three runs. The sender exits on its own; the receivers are hosts, so Ctrl-C when you have seen enough |

### What the competing demo looks like from outside

All the posts land inside one completed `TransactionScope`, so the receivers see nothing until the
batch commits and then take it in a burst — with the shipped profile's count of 250, that is one
commit of 250 rows. They never block on the open transaction: the dequeue uses
`with (rowlock, readpast)`.

**If you add your own `INSERT` on a second connection inside that scope, the transaction promotes
to MSDTC**, which on .NET is Windows-only. That promotion is the thing `GreetingsSender`'s Outbox
route avoids, by keeping the message and your own write on one connection.

### `Post` joins your transaction when the broker is your database

`CompetingSender` sends inside a `TransactionScope` and **completes it**, because with a database
as the broker the send is not decoupled from your transaction: `Post` opens a `SqlConnection`,
`Enlist` defaults to `true`, and the insert into the queue table joins the ambient transaction.
Abandon the scope and the message rolls back with it.

**The experiment is worth running yourself.** Comment out `scope.Complete()` and send:
nothing reaches `QueueData`, with no error anywhere. Then add `Enlist=False` to the connection
string and send again with the scope still abandoned — the messages arrive, because the insert is
no longer part of your transaction. Measured, three sends each way:

| | `Complete()` | rows in `QueueData` |
|---|---|---|
| default connection string | no | **0** |
| default connection string | yes | **3** |
| `…;Enlist=False` | no | **3** |

That third row is the decoupling a broker outside your database gives you for free, bought back by
opting out of enlistment — and losing, in exchange, any guarantee that the message and your own
write agree. **The Outbox is the version that keeps both**, and `GreetingsSender` is where to see
it.

This is the argument for the Outbox rather than a defect: `DepositPost` writes to the Outbox
inside your transaction, and `ClearOutbox` dispatches once it has committed.

### Idempotence

`GreetingEventHandler` carries `[UseInbox]`. That is the attribute route rather than
`AddConsumers`' global `InboxConfiguration`, because the global configuration only reaches the
pipeline when a process also registers producers — and this receiver registers none. The
`InboxConfiguration` registration remains, because it is what supplies the store. See
[#4335](https://github.com/BrighterCommand/Brighter/issues/4335).

**Running `GreetingsSender` twice will not show you this**, and that is worth knowing before you
conclude the Inbox is broken: `new GreetingEvent("Ian")` takes a fresh `Id` each time, so two runs
are two different messages and both are handled. The Inbox de-duplicates by message id, so you
need the *same* id delivered twice.

To force one, stop the receiver, run the sender, and duplicate the queued row before starting the
receiver again:

```sql
INSERT INTO QueueData (Topic, MessageType, Payload)
SELECT TOP 1 Topic, MessageType, Payload FROM QueueData;
```

`TOP 1` matters: without it the statement doubles whatever is in the queue, which against the
competing demo's backlog is not what you want.

The receiver then handles the message once and logs the second delivery:

```text
warn: Paramore.Brighter.Inbox.Handlers.UseInboxHandler
      Command 01a08bdb-... has already been seen
```

### Subscriptions must be `MsSqlSubscription<T>`

`ChannelFactory` downcasts what it is given and throws `ConfigurationException` when the cast
fails, so a plain `Subscription<T>` compiles and then dies as the Dispatcher builds its channels.
Both receivers call `.ValidatePipelines()`, which surfaces the related pump/handler mismatch as a
startup error rather than a per-message failure.

### Why no `databaseName:`

The `RelationalDatabaseConfiguration` calls name the tables but not the database — it is already
in the connection string, and nothing in the MSSQL gateway, Outbox, Inbox or Box Provisioning
reads `DatabaseName`. It is not inert everywhere, though: the MySQL migration runner and the
MongoDB adapters do read it, and it defaults to `"Brighter"`, so put it back if you adapt this to
either.

### A note on log levels

`MsSqlMessageQueue` logs its connection string at `Debug`, and the connection string above carries
an `sa` password. It does not reach the console as the sample stands — but in `GreetingsSender`
that is an accident of ordering rather than a property of the configuration. That application does
set `MinimumLevel.Debug()` with a console sink; the producer registry is simply built inside the
`AddProducers` callback, which runs before Brighter swaps your `ILoggerFactory` into
`ApplicationLogging`. Move that construction after `Build()` and the password lands on stdout.

## Further reading

[Use MSSQL for Transport, Outbox and Inbox](https://brightercommand.gitbook.io/paramore-brighter-documentation/transports/mssqlmessagebroker/mssqltransportinboxandoutbox)
is the guide this sample accompanies.

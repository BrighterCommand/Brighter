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
| `CompetingSender <count>` | two or more `CompetingReceiverConsole` | the count divided between the consumers — the split is arbitrary, and six messages across two receivers came out 3 and 3, 4 and 2, and 3 and 3 on three runs. The sender exits on its own; the receivers are hosts, so Ctrl-C when you have seen enough |

### Multiple receivers: competing queues and fan-out

With a plain `Publication`, receivers sharing a queue table and channel compete for messages.
Reading a message atomically deletes its row. This also applies to events: changing a message's
type does not enable broadcast delivery. `SubscriptionName` identifies a dispatcher subscription,
not an independent broker queue.

This is the shared-channel behavior reported in
[issue #2792](https://github.com/BrighterCommand/Brighter/issues/2792). To give each receiver group
a copy, keep the same logical topic and use a distinct subscriber channel for each group, as below.
Keeping the original shared channel continues to distribute messages between competing receivers.

For each subscriber group to receive its own copy, configure explicit subscriber queues on the
producer. All publishers of that topic must use the same list:

```csharp
var publication = new MsSqlPublication<GreetingEvent>
{
    Topic = new RoutingKey("greeting.event"),
    SubscriberChannels =
    [
        new ChannelName("greeting.email"),
        new ChannelName("greeting.audit")
    ]
};

var emailSubscription = new MsSqlSubscription<GreetingEvent>(
    subscriptionName: new SubscriptionName("email"),
    channelName: new ChannelName("greeting.email"),
    routingKey: new RoutingKey("greeting.event")) { IsIndependentSubscriber = true };

var auditSubscription = new MsSqlSubscription<GreetingEvent>(
    subscriptionName: new SubscriptionName("audit"),
    channelName: new ChannelName("greeting.audit"),
    routingKey: new RoutingKey("greeting.event")) { IsIndependentSubscriber = true };
```

Pass the publication to the producer factory and the subscriptions to the consumer configuration,
using the same SQL database and queue table. Match each subscription's message pump to its handler.
Each send inserts a copy for every configured subscriber queue in one atomic statement. Each copy
retains the logical topic, message id, body, and headers. Receivers can start after the send; their
copies remain in the queue. Multiple workers on `greeting.email` still compete for that group's copy.

The list must contain 1–1,000 nonblank channel names of at most 255 UTF-16 code units. A subscriber
channel must differ from the logical topic. Equivalent channel names under the queue column's SQL
collation receive one copy. Channel names are SQL parameter values, not SQL identifiers.

SQL rejects subscriber names equivalent to the logical topic under the actual column collation before
inserting any copies. This includes case, accent, and trailing-space aliases where SQL treats them as equal.
Set `IsIndependentSubscriber = true` on each fan-out subscription. This explicitly selects physical-queue
retry routing. Plain subscriptions retain topic-based scheduling, including when channel and topic spelling
differ but identify the same SQL queue.

The producer captures its logical topic, subscriber list, and `SendTimeout` when its publication is assigned.
Later changes to the caller's list or publication do not change active routing. Assign `producer.Publication`
again to apply new settings. Prepare that configuration before assignment; do not mutate it during capture.
Fan-out sends default to a 30-second command timeout. Set `MsSqlPublication.SendTimeout` to a nonnegative
`TimeSpan`; zero disables the command timeout, and positive fractions round up to whole seconds.

No schema migration is needed. The queue table's `Topic` column holds the destination queue, while
the serialized message holds the logical topic. A plain publication continues to write to its topic.
A subscription alone does not register a binding: adding a receiver without updating the producer's
subscriber list does not create new copies. Adding a destination affects future sends only; removing
one does not delete that queue's backlog. Drain retired queues before removing their consumers.
Also drain legacy topic queues before switching their producer registration to fan-out: a legacy
consumer's topic-based delayed retry can resolve the new producer and broadcast that retry to every
subscriber.

Immediate retries return only to the receiving queue. Purging a consumer clears only that queue.
For delayed subscriber retries, configure a SQL-targeted scheduler implementing
`IAmAMessageRequeueSchedulerSync` or `IAmAMessageRequeueSchedulerAsync` for the selected pump.
An ordinary topic scheduler is insufficient: it could broadcast the failed copy again. The consumer
factory rejects a positive subscription `requeueDelay` without the matching targeted scheduler.
Channel creation performs this validation before provisioning the queue store. An already-cancelled
async retry stops before changing message metadata or invoking the scheduler.
If a handler requests an explicit delay with `DeferMessageAction` despite a zero subscription delay,
and no matching targeted scheduler is available, the consumer logs a warning and immediately requeues
the copy to its physical queue. It preserves the logical topic and retry metadata without broadcasting
to other subscribers. A configured scheduler's failures still propagate.
There is no built-in persistent SQL retry scheduler in this change; use zero delay unless one is supplied.

The existing delivery guarantees still apply. A rollback undoes every inserted copy, but a lost
commit acknowledgement can produce duplicates when the sender retries. Receive deletes before the
handler runs, so a process crash after receive can lose that subscriber's copy. Fan-out does not add
acknowledgement-based redelivery or exactly-once processing. If subscriber handlers share an Inbox,
use distinct handler identities so deduplication in one group does not suppress another group.

Each destination stores the full serialized message. Size storage and transaction-log capacity for
subscriber count multiplied by payload size. A send inside an ambient transaction keeps its copies
uncommitted until that transaction completes. Concurrent transactions can commit out of identity order,
and consumers can skip locked rows, so delivery is not guaranteed to follow publication or commit order.

Consumer purge deletes at most 500 rows per statement and requests row locks. It records the queue's
highest identity first, so rows with higher identities remain queued. Without an ambient transaction,
each batch commits independently; cancellation or failure can therefore leave a partial purge. Inside
an ambient transaction, locks remain held until that transaction completes. The cutoff is an identity
boundary, not a snapshot of commit times.

Batching reduces lock escalation during purge, but does not guarantee freedom from blocking. Large
ambient transactions, memory pressure, or other table operations can still block unrelated queues.
`READPAST` skips row locks; it does not skip page or table locks, and deleting from secondary indexes can
still block.
Keep transactions short and schedule large purges with this shared-table contention in mind.
See SQL Server's [table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table)
and [lock escalation guidance](https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/performance/resolve-blocking-problems-caused-lock-escalation).

The existing dequeue query uses `READPAST` and requires locking Read Committed or Repeatable Read
isolation. If sends use an ambient `TransactionScope` and share a connection pool with consumers,
set its `TransactionOptions.IsolationLevel` explicitly to `IsolationLevel.ReadCommitted`.
The default Serializable isolation can remain on pooled connections and cause later dequeues to fail.
Databases using `READ_COMMITTED_SNAPSHOT` also require different dequeue locking hints; this
fan-out change does not add support for that configuration.

### Validating the transport

Run [verify-production.sql](verify-production.sql) in the intended deployment database to report
its SQL Server version, collation, and isolation configuration. It reads configuration only and
fails if `READ_COMMITTED_SNAPSHOT` is enabled. It does not certify application transaction scopes
or pooled session state. Keep the queue column's collation consistent between test and deployment.

With the test SQL Server available on `127.0.0.1:11433`, run the complete regression suite:

```bash
dotnet test tests/Paramore.Brighter.MSSQL.Tests/Paramore.Brighter.MSSQL.Tests.csproj
```

The `MsSqlFanOutTests`, `MsSqlFanOutConfigurationTests`, `MsSqlSubscriberRetryTests`, and
`MsSqlCompetingReceiversTests` cover the new contract and legacy queue behavior. Run them against
the SQL Server version and database collation used by the deployment before release.
The concurrency test runs four publishers and four consumers per subscriber queue simultaneously,
checking all 200 message IDs and payloads in both queues. Additional cases cover case-insensitive
and binary collations, maximum-length queue names, and large Unicode payloads.

### What the competing demo looks like from outside

Each `Post` commits on its own, so the receivers start taking messages while the sender is still
sending. Polling `QueueData` through the shipped profile's count of 250, the rows arrive steadily
rather than in one batch — first row to last in about 420 ms, nothing waiting on a commit at the
end. Neither receiver blocks on the row the other is handling: the dequeue uses
`with (rowlock, readpast)`.

### Do not wrap `Post` in a `TransactionScope`

**When the broker is your database, `Post` joins your ambient transaction.** It opens a
`SqlConnection`, `Enlist` defaults to `true`, and the insert into the queue table enlists like any
other write. Abandon the scope and the messages roll back with it, silently. That is the opposite
of what a `TransactionScope` around a send is usually reaching for — with a broker outside your
database the message is queued whether your transaction commits or aborts, and it is easy to carry
that assumption across.

Wrap `CompetingSender`'s loop in a scope and send three, and the rows follow the transaction:

| | `Complete()` | rows in `QueueData` |
|---|---|---|
| default connection string | no | **0** |
| default connection string | yes | **3** |
| `…;Enlist=False` | no | **3** |

That third row is the decoupling a broker outside your database gives you for free, bought back by
opting out of enlistment — and losing, in exchange, any guarantee that the message and your own
write agree.

**The scope is safe here only because it guards nothing, and that is the warning.** `Post` opens
and closes one connection at a time, so the transaction stays lightweight. Add the business write
that would give the scope something to be atomic *between* — a second connection open at the same
time — and .NET escalates to a distributed transaction:

```text
A. sequential connections in one scope
   ok -- stayed a lightweight transaction, no escalation
B. two connections open at once in one scope
   PlatformNotSupportedException: This platform does not support distributed transactions.
```

MSDTC is Windows-only on .NET, so the pattern becomes a platform failure at the moment it starts
meaning something.

**The Outbox is the version that works**, and it is why Brighter has one: `DepositPost` writes to
the Outbox inside your transaction, on your connection, and `ClearOutbox` dispatches once that
transaction has committed. `GreetingsSender` is where to see it.

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

## Further reading

[Use MSSQL for Transport, Outbox and Inbox](https://brightercommand.gitbook.io/paramore-brighter-documentation/transports/mssqlmessagebroker/mssqltransportinboxandoutbox)
is the guide this sample accompanies.

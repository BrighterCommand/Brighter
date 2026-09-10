# MSSQL Task Queue

SQL Server as the message broker, with the Outbox and the Inbox in the same database.

Four applications share one database: a queue table the transport reads and writes, an Outbox the
sender deposits into, and an Inbox the receiver de-duplicates against.

## Setup is two steps, and the split is deliberate

**1. Create the database.** It is the one thing that cannot create itself:

```bash
sqlcmd -S localhost,1433 -U sa -P '<password>' -C -i BrighterSqlQueue.sql
```

**2. Run the applications.** Each creates the tables it owns, on every start:

| Table | Created by |
|---|---|
| `QueueData` | `QueueTableProvisioner.EnsureQueueTable`, in both the sender and the receiver |
| `Outbox` | Box Provisioning, in the sender |
| `InboxMessages` | Box Provisioning, in the receiver |

**The queue table is provisioned by the sample rather than by Brighter, and that is not an
oversight.** The MSSQL gateway has no provisioning path at all: `OnMissingChannel.Create` is
accepted on a publication or subscription and then never acted on, unlike the PostgreSQL gateway.
`MsSqlQueueBuilder` is public precisely so callers can run the DDL themselves, and
`QueueTableProvisioner` is this sample doing that.

It lives in **`SampleInfrastructure`**, alongside `SampleDatabase` — the connection string and the
table names. `Events` stays what its name says: the commands, their mappers and the handler, with
no database dependency. That is the split the sample is trying to teach, and it is also practical,
because `Events` is the assembly every application hands to `AutoFromAssemblies`.

Because both the sender and the receiver provision what they need, **either can be started
first** against a database with no tables in it.

## Pointing it at something other than SQL Express

Every application reads `ConnectionStrings:Brighter`, falling back to the local SQL Express
instance. Nothing needs editing to run against a container:

```bash
export ConnectionStrings__Brighter='Server=localhost,1433;Database=BrighterSqlQueue;User Id=sa;Password=<password>;Encrypt=false'
```

**Neither `Encrypt=false` here nor `TrustServerCertificate=True` in the default connection string
belongs in production.** Both turn off a check that exists to stop you talking to the wrong
server: the first drops TLS altogether, the second keeps it and accepts any certificate. They are
here because a local SQL Express instance and a bare container both present a self-signed
certificate. Against a server with a certificate your clients trust, drop them.

## The applications

**Start with the greetings pair** — it is the path that round-trips end to end. The competing pair
demonstrates the subscription and provisioning fixes, but does not yet deliver its messages.

| Run | With | What you should see |
|---|---|---|
| `GreetingsSender` | `GreetingsReceiverConsole` | the sender provisions the Outbox, deposits and clears; the receiver prints the greeting once, and prints nothing on a redelivery of the same message id |
| `CompetingSender <count>` | two or more `CompetingReceiverConsole` | intended to show competing consumers dividing a queue — **currently does not round-trip, see [#4338](https://github.com/BrighterCommand/Brighter/issues/4338)** |

### Idempotence

`GreetingEventHandler` carries `[UseInbox]`. That is the attribute route rather than
`AddConsumers`' global `InboxConfiguration`, because the global configuration only reaches the
pipeline when a process also registers producers — and this receiver registers none. The
`InboxConfiguration` registration remains, because it is what supplies the store. See
[#4335](https://github.com/BrighterCommand/Brighter/issues/4335).

Send the same message twice and the second delivery logs:

```text
warn: Paramore.Brighter.Inbox.Handlers.UseInboxHandler
      Command 01a08bdb-... has already been seen
```

### Subscriptions must be `MsSqlSubscription<T>`

`ChannelFactory` downcasts what it is given and throws `ConfigurationException` when the cast
fails, so a plain `Subscription<T>` compiles and then dies as the Dispatcher builds its channels.
Both receivers call `.ValidatePipelines()`, which surfaces the related pump/handler mismatch as a
startup error rather than a per-message failure.

## Further reading

[Use MSSQL for Transport, Outbox and Inbox](https://brightercommand.gitbook.io/paramore-brighter-documentation/transports/mssqlmessagebroker/mssqltransportinboxandoutbox)
is the guide this sample accompanies.

# Greetings Sender with a PostgreSQL Outbox

This sender is the `PostgresTaskQueue` sample with a Transactional Outbox added, so **one
PostgreSQL database is both the broker and the Outbox**. The business row and the message
announcing it commit in a single transaction; the Sweeper then moves the message into the
queue store table in that same database, and `GreetingsReceiverConsole` — unchanged — consumes
it.

It accompanies the [Use PostgreSQL for Both Transport and Outbox](https://brightercommand.gitbook.io/paramore-brighter-documentation/transports/postgresqlmessagebroker/postgresqltransportandoutbox)
guide.

## What it shows

`RelationalDatabaseConfiguration` takes `queueStoreTable` and `outBoxTableName` as two
parameters on **one** object, which is why this composition needs no reconciling. The two
tables are provisioned by different routes on purpose:

| Table | Created by | Name in `pg_class` |
|---|---|---|
| `Queue` | the transport, from `MakeChannels = OnMissingChannel.Create` | `Queue` — the configured name, quoted as written |
| `Outbox` | `UseBoxProvisioning(… .AddPostgreSqlOutbox(…))` at startup | `outbox` — lowercased, then quoted |
| `greeting` | this sample's own DDL; not Brighter's table | `greeting` |

The Outbox folds to lower case deliberately, so a configured `"Outbox"` matches the table
older Brighter versions created unquoted — see `PgIdentifier`. It means
`select * from "Outbox"` fails while `select * from "Queue"` succeeds.

## Running it

```bash
docker compose -f docker-compose-postgres.yaml up -d

# Deposit a greeting and its message in one transaction, then let the Sweeper dispatch it
dotnet run --project samples/TaskQueue/PostgresTaskQueue/GreetingsSenderWithOutbox

# In a second terminal, consume it
dotnet run --project samples/TaskQueue/PostgresTaskQueue/GreetingsReceiverConsole
```

The Sweeper runs every 5 seconds and ignores messages younger than 5 seconds, so expect the
dispatch five to ten seconds after the send. Watch for
`Found 1 to clear out of amount 100` followed by
`Decoupled invocation of message: Topic:greeting.event`.

Run it with `--fail` to throw between the two writes and the commit:

```bash
dotnet run --project samples/TaskQueue/PostgresTaskQueue/GreetingsSenderWithOutbox -- --fail
```

Neither the greeting nor the message survives — the row counts in `greeting` and `outbox` are
unchanged, which is the point of putting them in one transaction.

# Bugfix: Relational outstanding count has no timeout, so a count stuck behind a lock never returns

**Linked Issue**: issue #4584
**Status**: Verified

## Symptom

**Observed.** `OutboxProducerMediator` counts outstanding messages on a background thread. For a relational outbox the count is `RelationDatabaseOutbox.GetOutstandingMessageCount` / `GetOutstandingMessageCountAsync`, which read the first page of outstanding messages through `OutstandingMessages` / `OutstandingMessagesAsync`. Those pass an `outBoxTimeout` of `-1` to `CreatePagedOutstandingCommand`, and `CreateCommand` turns a negative value into `CommandTimeout = 0`, which means no limit. If the SELECT is blocked behind a lock (for example an open transaction on the outbox table), the count waits for as long as the lock is held. The mediator's in-flight flag stays claimed, so it queues no further check and `CheckOutboxOutstandingLimit` keeps comparing posts against the last cached count.

**Expected.** The count gives up after a bounded time and throws. The mediator's existing catch treats a count it could not read as 0, so the limit fails open, as it already does for the DynamoDB outbox, whose count gives up after its configured timeout, 500 ms by default.

**Out of scope.** The sweeper scan uses the same command through `OutstandingMessagesAsync` and must keep its unbounded wait. The mediator still calls the async count without a cancellation token. Both stay as they are.

## Suspected Location

- `src/Paramore.Brighter/RelationDatabaseOutbox.cs:1000`, `:1006`: the two count members, which call `OutstandingMessages` / `OutstandingMessagesAsync`.
- `RelationDatabaseOutbox.cs:941`, `:987`: the `-1` passed for the timeout.
- `RelationDatabaseOutbox.cs:1301-1313`: `CreatePagedOutstandingCommand`, which carries the timeout to `CreateCommand`.
- `RelationDatabaseOutbox.cs:1393-1398`: `CreateCommand`, `CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout`. `src/Paramore.Brighter.Outbox.Spanner/SpannerOutbox.cs:50-57` overrides it and maps `<= 0` to 60.
- `src/Paramore.Brighter/OutboxProducerMediator.cs:1426-1470`: the count calls (sync first, then async) and the catch that sets the count to 0.
- `OutboxProducerMediator.cs:762`: the sweeper scan, which shares the command and must not gain a timeout.

## Root-Cause Hypothesis

The count shares the paged outstanding path with the sweeper scan, and that path hard-codes `-1`, so there is no way to give the count its own timeout. If the count had its own call into `CreatePagedOutstandingCommand` with a finite timeout, a SELECT blocked behind a lock would throw after that many seconds and the mediator would read the count as 0.

## Confirmed Root Cause

`RelationDatabaseOutbox.GetOutstandingMessageCount` and `GetOutstandingMessageCountAsync` (`RelationDatabaseOutbox.cs:1000-1010`) only call `OutstandingMessages` / `OutstandingMessagesAsync` and count the result. Those pass `-1` to `CreatePagedOutstandingCommand` (`:941`, `:987`), which forwards it to `CreateCommand` (`:1311`), where `-1` becomes `CommandTimeout = 0` (`:1398`), no limit. The count has no way to take its own timeout. While the SELECT is blocked, the mediator's in-flight flag stays claimed (`OutboxProducerMediator.cs` `CheckOutstandingMessages`, released only in the `finally`), so no further check is queued and `CheckOutboxOutstandingLimit` keeps using the cached count.

## Evidence

- [x] Code-trace: mediator count path (sync first, then async without a token) to `CreateCommand`; the mediator catch (`OutboxProducerMediator.cs:1463-1468`) sets the count to 0, so a thrown timeout fails open. `ReadFromStore(Async)` (`:1193-1242`) does not touch `CommandTimeout`, so the value set in `CreateCommand` is what the driver sees.
- The only production caller of `OutstandingMessagesAsync` besides the count is the sweeper (`OutboxProducerMediator.cs:762`). No subclass (MsSql, Sqlite, Postgres, MySql, Spanner) overrides or hides the count members or `OutstandingMessages(Async)`. Only `SpannerOutbox` overrides `CreateCommand` (`<= 0` becomes 60).
- Driver enforcement of `CommandTimeout` on a SELECT blocked behind a lock is driver knowledge, not shown by the repo. Measured against a 25 s table lock on SQLite, PostgreSQL, MySQL and SQL Server: with the fix the count threw at about 15 s and the sweep still waited out the lock. The Spanner emulator does not block a strong read behind an open read-write transaction, so the lock scenario could not be shown there; its run only confirms the count still returns with the new timeout.

## Suggested-Fix Assessment

CONFIRMED. A count-only path with a finite timeout, leaving the `-1` on `OutstandingMessages(Async)`, reaches every relational backend and fails open through the existing catch.

## Scope Notes

- The count must keep the span and the `db.response.returned_rows` tag that `OutstandingMessages(Async)` produces today.
- Spanner's count bound changes from 60 s to the new value.
- `CommandTimeout` does not cover opening the connection.
- Not touched: `GetNumberOfOutstandingMessages(Async)`, the inbox's identical mapping, MongoDb and Firestore counts, the XML doc that says milliseconds.
- On PostgreSQL and MySQL a plain SELECT is not blocked by row locks, only by a table-level lock.

## Regression Test

Red before the fix, green after it, in `tests/Paramore.Brighter.Sqlite.Tests/Outbox/`:

- `When_the_outstanding_count_waits_on_a_locked_table_should_time_out`
- `When_the_async_outstanding_count_waits_on_a_locked_table_should_time_out`

Characterisation, green before and after the fix (the sweep stays unbounded):

- `When_the_sweep_waits_on_a_locked_table_should_not_time_out`
- `When_the_async_sweep_waits_on_a_locked_table_should_not_time_out`

All four use `LockedSqliteOutbox`, which holds a real `BEGIN EXCLUSIVE` lock on a rollback-journal database file.

## Decided at the Confirm gate

- The count's timeout is 15 seconds, a named constant. On SQL Server, with 100,000 undispatched rows and a 2 KB body, the paged query took 56 to 125 ms with a warm cache, so a healthy count stays far below it.
- It is fixed, not configurable. The count members take no timeout and `RelationalDatabaseConfiguration` has none per query, so a setting would be new public API.
- Spanner uses the same value. Its `CreateCommand` maps a timeout of 0 or less to 60 seconds and passes 15 through, so its count goes from 60 to 15 seconds.
- `CreateCommand` sets `CommandTimeout`, so the 15 seconds wins over a Command Timeout in the connection string, as the unlimited timeout did before.

## Fix

- `src/Paramore.Brighter/RelationDatabaseOutbox.cs`: `OutstandingMessages` and `OutstandingMessagesAsync` now call private `ReadOutstandingMessages` / `ReadOutstandingMessagesAsync` with a timeout of -1, as before. `GetOutstandingMessageCount` and `GetOutstandingMessageCountAsync` call them with `RelationalDatabaseConfiguration.OUTSTANDING_COUNT_TIMEOUT_SECONDS`, 15.
- `tests/Paramore.Brighter.Sqlite.Tests/Outbox/`: the four tests above and `LockedSqliteOutbox`.
- `release_notes.md`: a Master entry for issue #4584.

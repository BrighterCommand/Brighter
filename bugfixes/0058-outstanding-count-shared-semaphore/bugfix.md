# Bugfix: Static count semaphore lets one hung outstanding count stop the checks of every mediator with the same types

**Linked Issue**: issue #4584
**Status**: Verified

## Symptom

**Observed.** `OutboxProducerMediator<TMessage, TTransaction>` counts outstanding messages on a background thread. Before it counts, it takes a `static` `SemaphoreSlim(1, 1)` and waits with no timeout. A static field on a generic class has one instance per closed type, so every mediator with the same message and transaction types shares this semaphore. `AddProducers` always uses `Message` as the message type, so in practice the semaphore is shared by every mediator in the process that uses the same transaction type.

If one mediator's count never returns, it never releases the semaphore. Then any other mediator of those types that has a limit set does this at its next due check:

1. It claims its own in-flight flag and queues the check.
2. The check blocks a pool thread on `Wait()` for as long as the first count is stuck.
3. Its in-flight flag stays claimed, so it queues no other check.
4. Its outstanding count keeps its last value. `CheckOutboxOutstandingLimit` compares every `Post`/`DepositPost` against that stale number: if it was over the limit, every post throws `OutboxLimitReachedException`; if it was under, posts never throw however much that mediator's own outbox grows.

The mediator whose count hangs also stops checking. That part is the unbounded count and is out of scope here (see below). This bugfix is about the spread: a hang in one mediator also stops the checks of other mediators that have their own outbox and a healthy database.

**Expected.** A count that hangs in one mediator affects only that mediator. Another mediator with the same closed generic types and its own outbox keeps starting counts while the first one is stuck.

**Reproduction** (from the issue):

1. Build two command processors whose mediators are both `OutboxProducerMediator<Message, CommittableTransaction>`, each on its own `CountingOutbox`, with `maxOutStandingMessages` set (for example 100) and a `maxOutStandingCheckInterval` of `TimeSpan.Zero`, the `AddProducers` default. `CountingOutboxCommandProcessorBuilder.Build` builds this.
2. Call `HoldCounts()` on the first outbox, post through the first command processor and wait for `WaitForCountToStartAsync`. The first count is now blocked inside the outbox while it holds the static semaphore.
3. Post through the second command processor.
4. Observed: the second outbox's `OutstandingCountCalls` stays at 0 for as long as the first count is held (the issue checked for 3 seconds), and the second count starts after `ReleaseCounts()` on the first outbox. Expected: the second outbox gets a count call whatever state the first one is in.

One `AddProducers` registers one singleton mediator per container (`ServiceCollectionExtensions.cs:419-423`), so the spread needs more than one mediator of the same types in a process: several hosts or containers, or command processors built by hand.

**Out of scope.** The count itself has no bound: no token or timeout on the count, and a relational `CommandTimeout` of 0. That is a separate part of issue #4584 and a later bugfix.

## Suspected Location

All in `src/Paramore.Brighter/OutboxProducerMediator.cs` unless stated otherwise.

- `OutboxProducerMediator.cs:48`: the generic class, so static fields exist once per closed type.
- `OutboxProducerMediator.cs:73-75`: `private static readonly SemaphoreSlim s_checkOutstandingSemaphoreToken = new(1, 1);` and its comment, which talks about "the static bool" and "locking the object"; neither exists anymore.
- `OutboxProducerMediator.cs:71`: `_backgroundClearSemaphore`, the per-mediator semaphore for the clear (the precedent below).
- `OutboxProducerMediator.cs:77`, `:82`: `_lastOutStandingMessageCheckAt` and `_outStandingCount`, both instance state. `_outStandingCount` is the value that goes stale.
- `OutboxProducerMediator.cs:89-91`: `_outstandingCheckInFlight`, the per-mediator in-flight flag from PR #4569.
- `OutboxProducerMediator.cs:810-824`: `CheckOutboxOutstandingLimit`, which throws `OutboxLimitReachedException` from the cached `_outStandingCount` (`:818-823`). Called on every add at `:292` (`AddToOutboxAsync`), `:337` (`AddToOutbox`) and `:727` (`BeginBatchAddToOutbox`).
- `OutboxProducerMediator.cs:826-871`: `CheckOutstandingMessages`.
  - `:828-829`: a comment that names "the semaphore shared by every mediator with the same types".
  - `:846-847`: `Interlocked.CompareExchange(ref _outstandingCheckInFlight, 1, 0)` claims the flag.
  - `:851`: records the check time when the check is queued.
  - `:857-867`: the `Task.Run` that calls `OutstandingMessagesCheck` at `:861` and releases the flag in the `finally` at `:863-866`, so a check that never returns keeps the flag claimed.
- `OutboxProducerMediator.cs:1426-1480`: `OutstandingMessagesCheck`.
  - `:1428`: `s_checkOutstandingSemaphoreToken.Wait();` with no timeout and no token.
  - `:1439-1445` (sync count) and `:1454-1462` (async count blocked with `.GetAwaiter().GetResult()`): the calls that can hang.
  - `:1478`: `s_checkOutstandingSemaphoreToken.Release()` in the `finally`, never reached if the count never returns.
  - Its only caller is the `Task.Run` at `:861`, which the CompareExchange at `:846` guards, so a single mediator never runs two counts at once.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:829`: DI closes the mediator over `Message`, so the semaphore is shared per transaction type.

Tests and test doubles:

- `tests/Paramore.Brighter.Core.Tests/CommandProcessors/TestDoubles/CountingOutbox.cs:18-95`: `HoldCounts()` (`:58`), `ReleaseCounts()` (`:63`), `WaitForCountToStartAsync` (`:70`), `WaitForOutstandingCountCallsAsync` (`:78`). `StartCount()` (`:89-94`) increments `OutstandingCountCalls` before it waits on the gate.
- `tests/Paramore.Brighter.Core.Tests/CommandProcessors/TestDoubles/CountingOutboxCommandProcessorBuilder.cs:41-52`: always builds `OutboxProducerMediator<Message, CommittableTransaction>`.
- Tests that depend on the semaphore being shared across mediators:
  - `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_a_burst_of_posts_arrives_before_the_first_check_starts_should_queue_one_outstanding_count.cs`: a second mediator holds its count (`:42-47`), and `:55` asserts `OutstandingCountCalls == 0` on the mediator under test. That holds only because the mediator under test is blocked at `OutboxProducerMediator.cs:1428`.
  - `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_the_first_check_starts_late_should_measure_the_interval_from_when_it_was_queued.cs`: the same hold (`:40-45`) and assertion (`:49`), then a clock advance (`:52`) so the check starts late.
  - `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_a_check_is_in_flight_should_queue_no_further_check.cs:44-46` mentions "the semaphore the counts share" in a comment only; it uses one mediator.
- `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Clear/When_independent_mediators_sweep_concurrently_should_clear_both_outboxes.cs:51-79`: the two-mediator test that came with the clear-semaphore precedent.

Release notes and history:

- `release_notes.md:1055-1077` (the issue #4554 entry): `:1060` and `:1073-1074` describe the shared semaphore.
- `release_notes.md:1883` (9.3.0): "Internal change to move outstanding message box to a semaphore slim over a mutex as thread-safe. Not strictly neededm, but follows our policy of moving to semaphore slim". This is about the move from a mutex.
- `specs/0003-remove_clear_event_bus_calls/requirements.md:69` lists "Changing the `OutboxProducerMediator` semaphores (they must remain static for global concurrency coordination)" as out of scope, and `docs/adr/0020-reduce-esb-complexity.md:76` describes "a semaphore to control the number of checks of the outbox that can be made at any one time". PR #4242 (61b4c28c3) has since made the clear semaphore per mediator.
- The field was already `static` in `ExternalBusServices` and was renamed to `s_checkOutstandingSemaphoreToken` in 1cdc04b60. Commit 61b4c28c3 ("fix(outbox): scope clear lock to mediator", issue #4241) turned the static clear semaphore into the instance field `_backgroundClearSemaphore` because "Independent mediators can own different outboxes and must not suppress each other's nonblocking clear operation."

## Root-Cause Hypothesis

`s_checkOutstandingSemaphoreToken` is a static field on the generic class (`:75`), so every mediator with the same closed types shares it. `OutstandingMessagesCheck` waits on it with no timeout (`:1428`) and releases it only after the outbox count returns (`:1478`). A count that never returns in one mediator keeps the semaphore, every other mediator of the same closed type blocks its next check at `:1428`, that check never reaches the `finally` at `:863-866`, its `_outstandingCheckInFlight` stays 1, the CompareExchange at `:846-847` rejects every later check, and its `_outStandingCount` is frozen for `CheckOutboxOutstandingLimit` (`:818-823`).

Since PR #4569 the in-flight flag already allows at most one `OutstandingMessagesCheck` per mediator, so the semaphore no longer serialises anything inside a mediator. Its only remaining effect is to serialise counts across mediators, which is how a hang spreads.

Falsifiable predictions:

- With two mediators of the same closed type on separate outboxes, a count held in the first stops the second outbox from receiving any count call for as long as the hold lasts.
- If the semaphore is not shared, the second outbox receives its count call within a short deadline while the first is still held.
- If the second mediator turns out to be blocked by something else (the in-flight flag, the interval, thread-pool starvation), the hypothesis is refuted.

Suggested fix from the issue: drop the semaphore, or make it per mediator as 61b4c28c3 did for the clear semaphore. **UNVERIFIED: to be proven or refuted in /bugfix:confirm.** To settle in confirm: whether a per-mediator semaphore adds anything over the in-flight flag, and whether anyone relies on the cross-mediator serialisation to limit concurrent counts against a database that several mediators share.

## Confirmed Root Cause

`s_checkOutstandingSemaphoreToken` is a `private static readonly SemaphoreSlim(1, 1)` on the generic class `OutboxProducerMediator<TMessage, TTransaction>` (`OutboxProducerMediator.cs:48`, `:75`), so there is one per closed type, and DI always closes over `Message` (`ServiceCollectionExtensions.cs:829`). `OutstandingMessagesCheck` takes it with `Wait()`, with no timeout and no token (`:1428`), and gives it back only in the `finally` at `:1478`, after the outbox count returns. When one mediator's count never returns, another mediator of the same closed type with a limit set claims its own flag (`:846`), queues its check (`:857`), blocks at `:1428`, never reaches the flag release at `:863-866`, rejects every later check at `:846-847`, and keeps checking adds (`:292`, `:337`, `:727`) against a frozen `_outStandingCount` (`:818-823`).

Since PR #4569 the in-flight flag allows at most one count per mediator, so the semaphore serialises nothing inside a mediator. Its only effect is to serialise counts across mediators, which is how the hang spreads.

## Evidence

- [x] Code-trace: `CommandProcessor.Post` (`CommandProcessor.cs:714-720`) deposits through `AddToOutbox`, which calls `CheckOutboxOutstandingLimit()` (`OutboxProducerMediator.cs:337`), then calls `ClearOutbox`, which ends in `CheckOutstandingMessages` (`:434`). With a limit set and a zero interval the check is queued (`:830-867`) and waits at `:1428` on the static field (`:75`). The holder sits in the sync count (`:1439-1445`) or the blocked async count (`:1454-1462`), and `Release()` runs only at `:1478`. `_outStandingCount`, `_lastOutStandingMessageCheckAt` and `_outstandingCheckInFlight` are instance fields (`:82`, `:77`, `:91`), so nothing else couples the mediators. `OutstandingMessagesCheck` has one caller, the `Task.Run` at `:861`, guarded by the flag.
- [x] Red repro: two command processors from `CountingOutboxCommandProcessorBuilder.Build`, each with its own `CountingOutbox`, `maxOutStandingMessages: 100` and a zero interval. Hold the first outbox's counts, post through the first and wait for its count to start, then post through the second. `secondOutbox.WaitForOutstandingCountCallsAsync(1, deadline)` returns false today, because the second mediator's check waits at `:1428`. The deadline must stay well under the 30 s hold limit of `CountingOutbox` (`CountingOutbox.cs:23`). A probe of this scenario, run when issue #4584 was written, saw no count in the second outbox for 3 s while the first was held, and one within 5 s of the release, which rules out thread-pool starvation.

## Scope Notes

- **Suggested fix: drop the semaphore.** Making it per mediator would also stop the spread, but it would never be contended, since the flag already allows one count per mediator, and it would add an undisposed `IDisposable`. No caller bypasses the flag.
- **Who sees a change.** Only a process with several limited mediators of one closed type, now able to run one count each at the same time instead of one in total, each capped at `limit + 1` rows (`:1435`). The two built-in extra mediators (`ControlBusReceiverBuilder.cs:177`, `ControlBusSenderFactory.cs:62`) have no limit and never took the semaphore.
- **Memory visibility is unchanged.** `_outStandingCount` is read on posting threads that never take the semaphore (`:816`, `:819`), and `_lastOutStandingMessageCheckAt` is written only at `:851`, outside it.
- **A second way the semaphore leaks, removed by the same change.** `Log.BeginCountOfOutstandingMessages` (`:1430`) runs after `Wait()` but outside the `try`, so a logger that throws there skips `Release()` and stops every mediator of the closed type with no hung outbox at all.
- **Tests that depend on the shared semaphore** (both from PR #4569):
  - `When_a_burst_of_posts_arrives_before_the_first_check_starts_should_queue_one_outstanding_count`: its precondition (a check queued but not started) cannot be set up without the shared semaphore, and `:55` would race. A burst while a check is pending is now also rejected by the flag, which `When_a_check_is_in_flight_should_queue_no_further_check` already covers.
  - `When_the_first_check_starts_late_should_measure_the_interval_from_when_it_was_queued`: the gap between queuing and starting is now only `Task.Run` latency, which a test cannot hold open without a seam (`CONTRIBUTING.md:196`). Its "no check at 2.9 s, a check at 3.2 s" part (`:63-74`) is the only negative test of the interval gate (`:839`).
- **Comments that become false:** `OutboxProducerMediator.cs:73-74` (the field's comment), `:828-829` (names the shared semaphore), `When_a_check_is_in_flight_should_queue_no_further_check.cs:44-45`. The issue #4554 entries in `release_notes.md` (`:1060`, `:1073-1074`) are in the past tense and stay true; this fix needs its own entry.
- **Decided at the Confirm gate:** remove the semaphore; delete the burst test; rewrite the starts-late test as a test of the interval gate, without the second mediator.
- **Out of scope, still present:** the count has no bound (`RelationDatabaseOutbox.cs:1000-1009`, `:1398`), so a hang still stops the hung mediator's own checks. That is the other part of issue #4584.

## Regression Test

- `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_a_count_is_held_in_one_mediator_should_count_in_another_mediator_of_the_same_types.cs`: red before the fix. Two mediators of the same closed type, each with its own `CountingOutbox`; the first one's count is held and the second must count within 5 s.
- `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_posts_come_within_the_interval_should_queue_no_check_until_it_has_passed.cs`: replaces `When_the_first_check_starts_late_should_measure_the_interval_from_when_it_was_queued`, without the second mediator. It is green before and after the fix and keeps the only negative test of the interval gate; removing the gate, or halving or doubling the interval, makes it fail.
- `When_a_burst_of_posts_arrives_before_the_first_check_starts_should_queue_one_outstanding_count` is removed. A burst while a check is in flight is covered by `When_a_check_is_in_flight_should_queue_no_further_check`.
- Not covered by a test of its own: the leak through `Log.BeginCountOfOutstandingMessages` (`:1430`). `s_logger` comes from the static, process-wide `ApplicationLogging`, so a throwing logger would affect every test in the process. Removing the semaphore removes that path.
- No longer covered: that the interval is measured from when a check is queued rather than when it starts. Without the shared semaphore the gap between the two is only `Task.Run` latency, which a test cannot hold open without a seam (`CONTRIBUTING.md:196`).

## Fix

- `src/Paramore.Brighter/OutboxProducerMediator.cs`: removed `s_checkOutstandingSemaphoreToken` and its `Wait()` and `Release()` in `OutstandingMessagesCheck`, so each mediator counts on its own; the in-flight flag still keeps one count per mediator. Dropped the clause of the comment in `CheckOutstandingMessages` that named the shared semaphore.
- `tests/.../When_a_check_is_in_flight_should_queue_no_further_check.cs`: dropped the clause of a comment that named the shared semaphore.
- `release_notes.md`: a Master entry for issue #4584.

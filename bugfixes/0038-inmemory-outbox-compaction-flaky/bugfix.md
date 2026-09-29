# Bugfix: InMemoryOutbox dispatched-only compaction test fails intermittently because compaction is silently skipped

**Linked Issue**: #4475
**Status**: Verified

## Symptom

`OutboxCompactionDispatchedOnlyTests.When_compacting_only_dispatched_messages_in_outbox` fails intermittently in CI with "Oldest dispatched message should be removed by compaction" (`tests/Paramore.Brighter.InMemory.Tests/Outbox/When_compacting_only_dispatched_messages_in_outbox.cs:75`).

**Expected:** With `EntryLimit = 5`, `CompactionPercentage = 0.5`, `ExpirationScanInterval = 100ms`, and 2 undispatched + 3 dispatched messages, the 6th `Add` triggers compaction: `newSize = 2`, `entriesToRemove = 5 - 2 = 3`. Only dispatched entries are eligible, so all 3 dispatched messages are removed; `EntryCount` becomes 3 (2 undispatched + trigger) and `dispatchedIds[0]` is gone.

**Observed:** Intermittently `dispatchedIds[0]` is still present after the polling loop. The undispatched-survival asserts (line 70) run first and pass, so compaction removed nothing (or at least not `dispatchedIds[0]`).

**Key clue from the CI log:** the failed test ran for **"[2 s]"** — exactly the polling loop's exhaustion time (lines 59-64: 20 retries × `Task.Delay(100)`). `EntryCount` never dropped to ≤ 3 within 2 seconds, i.e. compaction either never ran or removed nothing. *(Correction from /bugfix:confirm: a compaction delayed beyond 2s would also show "[2 s]", so the duration alone does not discriminate between a dropped and a very late compaction.)*

**Reproduction (suspected):** Run the InMemory test project repeatedly under load (e.g. many parallel test classes / constrained thread pool); fails intermittently. Not yet reproduced locally.

## Suspected Location

- `src/Paramore.Brighter/InMemoryBox.cs:102-119` — `ClearExpiredMessages`: if `ExpirationScanInterval` has elapsed, fires a fire-and-forget `Task.Factory.StartNew` expiry scan.
- `src/Paramore.Brighter/InMemoryBox.cs:121-134` — `RunRemoveExpiredMessages`: `Monitor.TryEnter(_cleanupRunningLockObject)`.
- `src/Paramore.Brighter/InMemoryBox.cs:138-165` — `EnforceCapacityLimit`: cooldown check at line 144 (`(now - _lastCompactionAttemptAt) < ExpirationScanInterval`); line 156 sets `_lastCompactionAttemptAt = now` *before* the background task runs; lines 158-163 fire-and-forget the compaction.
- `src/Paramore.Brighter/InMemoryBox.cs:167-180` — `RunCompact`: `Monitor.TryEnter` on the **same** `_cleanupRunningLockObject` (declared line 54) used by expiry. If the lock is taken, compaction is silently dropped — no retry, no wait, no reset of `_lastCompactionAttemptAt`.
- `src/Paramore.Brighter/InMemoryOutbox.cs:142-143` — `Add` calls `ClearExpiredMessages()` then `EnforceCapacityLimit()` back-to-back, so an expiry task and a compaction task are queued near-simultaneously.
- `src/Paramore.Brighter/InMemoryOutbox.cs:674-688` — `Compact`: filters to dispatched entries (`TimeFlushed != MinValue`), orders by `TimeFlushed`, takes `entriesToRemove`.
- `src/Paramore.Brighter/InMemoryOutbox.cs:514-529` — `MarkDispatched`: sets `TimeFlushed` from `_timeProvider`.
- Test: `tests/Paramore.Brighter.InMemory.Tests/Outbox/When_compacting_only_dispatched_messages_in_outbox.cs:24` sets `ExpirationScanInterval = 100ms`; lines 34-48 advance a `FakeTimeProvider` 100ms per insert; line 53 advances 200ms; line 56 is the trigger `Add`; lines 59-64 poll up to 2s.

## Root-Cause Hypothesis

**Primary hypothesis — compaction is silently skipped due to lock contention with the expiry scan.**

On the trigger `Add` (test line 56) at fake time T+700ms, `_lastScanAt` was last set at T+400ms, so `ClearExpiredMessages()` sees ≥100ms elapsed, updates `_lastScanAt`, and queues a `RunRemoveExpiredMessages` task (`InMemoryBox.cs:106-118`). Immediately after, `EnforceCapacityLimit()` sees `count (5) >= EntryLimit (5)`, sets `_lastCompactionAttemptAt = now`, and queues a `RunCompact(3)` task (`InMemoryBox.cs:151-163`).

Both tasks race on the thread pool and both `Monitor.TryEnter` the same `_cleanupRunningLockObject`. If the expiry task holds the lock when `RunCompact` tries it, `TryEnter` returns false and compaction is discarded (`InMemoryBox.cs:169`). Nothing removes anything:
- The expiry scan is a no-op here (`EntryTimeToLive` defaults to 5 min; entries are ≤500ms old).
- No further compaction is attempted: the test never advances the `FakeTimeProvider` again, so the cooldown at line 144 blocks every subsequent attempt.

The polling loop runs the full 2s, the undispatched asserts pass, line 75 fails.

Additional lock holders: earlier `Add` calls at T+100..T+400 also queued expiry scans, which under thread-pool starvation (parallel xUnit classes in CI) may still be queued/running.

Why this test and not its siblings: `When_controlling_cache_size.cs` (both tests) and `Inbox/When_inbox_compaction_removes_oldest_by_write_time.cs` keep the default `ExpirationScanInterval` (10 min), so `ClearExpiredMessages` never fires and nothing contends for the lock. This test is the only compaction-expecting test that sets a 100ms scan interval.

**How /bugfix:confirm could falsify it:**
- (a) Temporary instrumentation counting `RunCompact` `TryEnter` failures across repeated runs — every failure should correlate with a skipped compaction; failures where compaction ran refute it.
- (b) Force the interleaving deterministically (hold `_cleanupRunningLockObject` via reflection, or block expiry) during the trigger `Add` — the test should then fail every time.
- (c) Raise `ExpirationScanInterval` so no expiry task is queued on the trigger `Add` (recomputing the cooldown timing) — flakiness should disappear.

If failures persist when no expiry task is ever queued, the hypothesis is refuted.

**Ranked alternatives:**

1. **Issue's hypothesis — "compaction relies on a background thread; the test is flaky due to timing." UNVERIFIED — to be proven or refuted in /bugfix:confirm.** The background compaction task is scheduled but doesn't run within the 2s window. Consistent with the exactly-2s duration, but requires >2s thread-pool starvation of a single queued task — far less likely than a silent `TryEnter` skip, and it doesn't explain why only the short-scan-interval test fails. Distinguish via instrumentation: if `Compact` is eventually entered late → timing; if never entered because `TryEnter` failed → primary hypothesis.
2. **Stale expiry tasks from earlier `Add`/`MarkDispatched` calls hold the lock.** A variant of the primary hypothesis (lock holder is an earlier-queued scan delayed by pool pressure); confirm should report which task held the lock.
3. **Timestamp ties make "oldest" nondeterministic.** Unlikely: `TimeFlushed` values are distinct (T+200, T+300, T+400), and all 3 dispatched entries are removed anyway, so ordering cannot decide `dispatchedIds[0]`'s survival.
4. **Compaction evicts undispatched entries / arithmetic picks too few.** Refuted by reading: `InMemoryOutbox.cs:678` filters to dispatched; `entriesToRemove = 3` (`InMemoryBox.cs:153-154`); undispatched asserts pass in the failing run.
5. **Shared static state / parallel test interference.** No shared state found — each test owns its `InMemoryOutbox`, `FakeTimeProvider`, and instance `Requests` (`InMemoryBox.cs:51`); no `CollectionBehavior`/`xunit.runner.json` override. Parallelism matters only indirectly, by widening the race window.

**Why it may have gotten worse:** the test and the compaction cooldown `_lastCompactionAttemptAt` (`InMemoryBox.cs:53,144,156`) were introduced in `0a025e876` "[Feat] InMemoryBox Expiry and Outbox limits (#4116)". The shared lock with `TryEnter`-and-drop semantics dates from 2021 (`cfca71e45`, `35e995618`), but previously a dropped compaction would be retried on the next `Add`. The cooldown being set *before* scheduling, combined with a frozen `FakeTimeProvider`, makes a single dropped compaction permanent for this test.

**What a fix must address (not a design):** a compaction can be lost under lock contention and, due to the cooldown, never retried; the test assumes a single trigger `Add` guarantees compaction, which the implementation does not.

## Confirmed Root Cause

**Verdict: CONFIRMED.**

`InMemoryBox<T>` runs expiry and compaction as separate fire-and-forget pool tasks that both `Monitor.TryEnter` the same `_cleanupRunningLockObject` (`src/Paramore.Brighter/InMemoryBox.cs:54,123,169`), and both silently no-op if the lock is held (no wait, no retry, no reset). `EnforceCapacityLimit` stamps `_lastCompactionAttemptAt = now` *before* queuing the compaction task (`InMemoryBox.cs:156` vs `158-163`), so a compaction dropped by `RunCompact` still counts as an attempt and the cooldown at `InMemoryBox.cs:144` suppresses every retry for a full `ExpirationScanInterval`.

In the test, the trigger `Add` queues an expiry task and then a compaction task back to back (`InMemoryOutbox.cs:142-143`); the `FakeTimeProvider` never advances again, so if the compaction task loses the lock race (to this expiry task or to one of the earlier still-pending ones) compaction never happens, the poll exhausts at 2s, and `When_compacting_only_dispatched_messages_in_outbox.cs:75` fails.

Before `0a025e876` (#4116) compaction already used `TryEnter` on the same lock (verified via `git show 0a025e876^:src/Paramore.Brighter/InMemoryBox.cs`), but there was no cooldown, so the next `Add` over the limit retried. #4116 added the cooldown **and** this test, so the test has been flaky since it was written.

A second, independent path produces the identical symptom: the compaction task is queued but not scheduled within the 2s poll window (thread-pool starvation). The issue's "timing" explanation is this path; it is real but secondary.

## Evidence

- [x] **Code-trace**
  1. Start: `_lastScanAt = T0`, `_lastCompactionAttemptAt = MinValue` (`InMemoryBox.cs:52-53`).
  2. In `Add`, `ClearExpiredMessages()` and `EnforceCapacityLimit()` run *before* `Requests.TryAdd` (`InMemoryOutbox.cs:142-153`), so `EntryCount` is read pre-insert.
  3. Adds u0@T0 (no scan), u1@T+100, d0@T+200, d1@T+300, d2@T+400 each queue an expiry scan (except u0); counts 0–4 are below the limit, so no compaction attempt is made before the trigger. `MarkDispatched` (`InMemoryOutbox.cs:517`) calls `ClearExpiredMessages()` at the same fake time, so elapsed = 0 and it does nothing. The test comment "Advance past compaction cooldown" (line 52) is misleading: no cooldown is active before the trigger.
  4. Trigger `Add` @T+700: 300ms since the last scan ≥ 100ms, so an expiry task is queued (`InMemoryBox.cs:110-118`); count 5 ≥ 5, so `entriesToRemove = 3`, `_lastCompactionAttemptAt = T+700`, and `RunCompact(3)` is queued (`InMemoryBox.cs:151-163`). Up to 5 expiry tasks (T+100..T+700) may be pending as potential lock holders.
  5. `RunCompact` has `if (Monitor.TryEnter(...)) {...}` with no else branch (`InMemoryBox.cs:169-179`): losing the lock is a silent no-op.
  6. No retry path: there is no timer in `InMemoryBox`; the test makes no further `Add`; `GetAsync` (`InMemoryOutbox.cs:358`) only calls `ClearExpiredMessages` (elapsed 0); even another `Add` at T+700 would return at `InMemoryBox.cs:144`.
  7. Expiry cannot do the compaction's work: it removes only dispatched entries older than the TTL (`InMemoryOutbox.cs:658-672`), and the default TTL is 5 min (`InMemoryBox.cs:64`).
  8. If `Compact` runs, `dispatchedIds[0]` is always removed: it filters to dispatched entries, orders by `TimeFlushed`, and takes 3 of exactly 3 (`InMemoryOutbox.cs:674-688`). No other path was found in which `Compact` runs and d0 survives.
  9. With compaction dropped: count stays at 6, the poll runs 20×100ms ("[2 s]"), line 70 passes, and line 75 fails.
- [ ] **Red repro** (designed; to be written by /bugfix:test). This is pure logic with no infrastructure. `InMemoryOutbox` is not sealed (`InMemoryOutbox.cs:83`) and `RemoveExpiredMessages` is `protected override` (`:658`).
  - **Setup:** a test subclass overrides `RemoveExpiredMessages` to signal `expiryEntered` and then block on a `release` gate while holding the lock. The gate is armed just before the trigger `Add`.
  - **Steps:** trigger `Add`, wait for `expiryEntered`, allow time for `RunCompact` to run, release the gate, then optionally `Add` again at the same fake time and poll.
  - **Expected today:** `GetAsync(dispatchedIds[0]).IsEmpty` is false, `EntryCount` has not been compacted, and the compaction call count is 0. The only nondeterminism, a `RunCompact` that is later still, can make the test pass spuriously but never fail spuriously.

## Scope Notes

- **Suggested fix: PARTIAL.** The issue's "background thread / timing" explanation describes a real secondary path (late scheduling), but it misses the primary deterministic path: lock contention plus a cooldown stamped before the task runs. Waiting longer or polling more cannot fix a dropped compaction. Raising `ExpirationScanInterval` in the test would hide the product defect and should not be treated as the fix.
- **This is a product defect, not only a flawed test assumption.**
  - With a real `TimeProvider`, both gates use `ExpirationScanInterval` (default 10 min, `InMemoryBox.cs:72`), so once the box is over the limit, expiry and compaction tend to fire on the same `Add`, with expiry queued first.
  - In production n ≥ `EntryLimit` (default 2048), so the O(n) expiry scan holds the lock for a meaningful time.
  - A lost compaction leaves `EntryLimit` unenforced for at least one more interval, and the loss can recur because the two tasks stay in phase.
  - For the outbox this is worst, because expiry never removes undispatched entries (`InMemoryOutbox.cs:663`).
- **Inbox parity:** `InMemoryInbox` (the only other `InMemoryBox<T>` subclass, `src/Paramore.Brighter/InMemoryInbox.cs:115`) has the same back-to-back `ClearExpiredMessages(); EnforceCapacityLimit();` (`InMemoryInbox.cs:144-145`) and inherits the same drop and cooldown behaviour. The fix belongs in `InMemoryBox.cs`, not in each subclass.
- **Expiry can be dropped too:** `RunRemoveExpiredMessages` has the same `TryEnter`-and-drop pattern (`InMemoryBox.cs:123`), and `_lastScanAt` has already advanced (`:110`), so a contended expiry pass is lost for an interval. This predates #4116.
- **Other tests exposed to late scheduling only** (a single trigger with a fixed delay or a bounded poll):
  - `Outbox/When_expiring_message_in_outbox.cs:38-45` (fixed `Task.Delay(500)`)
  - `Outbox/When_expiring_only_dispatched_messages_in_outbox.cs:47-55`
  - `Outbox/When_entry_limit_is_minus_one_no_compaction.cs:47,58-69`
  - `Inbox/When_expiring_messages_in_inbox.cs:38,45` (fixed 500ms) and `:78-86`
  - `Outbox/When_controlling_cache_size.cs`
  - `Inbox/When_inbox_compaction_removes_oldest_by_write_time.cs`
  
  Only the failing test is exposed to the lock-drop path. No tests outside `Paramore.Brighter.InMemory.Tests` set a short `ExpirationScanInterval`.

## Regression Test

All three tests are red today and fail the same way on every run (8/8 runs). Each one holds the cleanup lock deterministically through a test double that overrides a protected cleanup hook.

- `tests/Paramore.Brighter.InMemory.Tests/Outbox/When_compaction_contends_with_expiry_for_the_cleanup_lock_should_still_compact.cs`
  - **Scenario:** an expiry scan holds the lock, and then an `Add` over the limit triggers compaction.
  - **Expected:** `EntryCount == 3`. **Actual today:** 6, because the compaction was dropped.
- `tests/Paramore.Brighter.InMemory.Tests/Inbox/When_inbox_compaction_contends_with_expiry_for_the_cleanup_lock_should_still_compact.cs`
  - **Scenario:** the same as the outbox test, run against `InMemoryInbox`.
  - **Expected:** 3. **Actual today:** 6.
- `tests/Paramore.Brighter.InMemory.Tests/Outbox/When_expiry_contends_with_compaction_for_the_cleanup_lock_should_still_expire.cs`
  - **Scenario:** compaction holds the lock, and then a `Get` triggers an expiry scan for dispatched messages that are past their TTL.
  - **Expected:** 1. **Actual today:** 3, because the expiry scan was dropped.
- Test doubles:
  - `tests/Paramore.Brighter.InMemory.Tests/TestDoubles/BlockingExpiryInMemoryOutbox.cs`
  - `tests/Paramore.Brighter.InMemory.Tests/TestDoubles/BlockingExpiryInMemoryInbox.cs`
  - `tests/Paramore.Brighter.InMemory.Tests/TestDoubles/BlockingCompactionInMemoryOutbox.cs`

**Known limitation:** each test waits 500ms for the contended task to reach the held lock. If a task is scheduled later than that, it finds the lock free and the test passes spuriously. The tests can never fail spuriously.

## Fix

**First attempt (`85d3325ad`, superseded):** replaced `Monitor.TryEnter` with a blocking `lock`. This stopped work being lost, but under load it holds a pool thread for every waiting request. With a short `ExpirationScanInterval` and scans slower than that interval, those blocked threads pile up and make the pool starvation worse. A compaction that had to wait also used a stale entry count.

**Final fix: `src/Paramore.Brighter/InMemoryBox.cs`.** Expiry and compaction requests are merged onto a single background worker.

- `ClearExpiredMessages` and `EnforceCapacityLimit` keep their existing interval and cooldown checks. When cleanup is due, they record a request and call `EnsureCleanupWorker()`.
- At most one worker runs, started with `Task.Factory.StartNew(..., TaskScheduler.Default)`. So it always runs on the thread pool, never on a Reactor/Proactor pump's `SynchronizationContext`. It contains no `await` and no blocking wait.
- The worker loops, taking pending requests until none are left. It decides to stop under the same lock that requests are recorded under, so no request can be missed. If a pass throws, the running flag is reset so the next request starts a new worker.
- Compaction is re-checked when it runs. It is skipped if the box is now under `EntryLimit`, and it never removes enough to take the box below its target size.
- Both boxes are covered because `InMemoryOutbox` and `InMemoryInbox` share this base class. Nothing else changed: no defaults, cooldown logic or interval logic.

**Failure path (from the `@claude` review on #4482):** each operation in a pass now runs inside its own `try`/`catch`.

- A failed expiry no longer skips a compaction taken in the same pass.
- The worker only ever stops in `TryTakeCleanupRequests`, under the lock. This removes the window in which a request made after a throw could be stranded for up to one interval.
- The first failure is rethrown once the worker has stopped, so it still surfaces as an unobserved task exception, the same as before.
- Covered by `Outbox/When_expiry_throws_in_the_same_cleanup_pass_as_a_compaction_should_still_compact.cs`, using `TestDoubles/FailingSecondExpiryInMemoryOutbox.cs`.

**New tests measured against each version of `InMemoryBox.cs`** (net9.0; the numbers are `EntryCount`, or the number of scans for the coalescing test):

| Test | master `4b25517` | blocking lock `85d3325` | single worker `10c28b1` | final |
|---|---|---|---|---|
| `Outbox/When_compaction_contends_with_expiry_…_should_still_compact` | ❌ 6 | ✅ | ✅ | ✅ 3 |
| `Inbox/When_inbox_compaction_contends_with_expiry_…_should_still_compact` | ❌ 6 | ✅ | ✅ | ✅ 3 |
| `Outbox/When_expiry_contends_with_compaction_…_should_still_expire` | ❌ 3 | ✅ | ✅ | ✅ 1 |
| `Outbox/When_expiry_is_requested_repeatedly_during_a_cleanup_should_coalesce_into_one_scan` | ❌ 6 † | ❌ 6 | ✅ | ✅ 2 |
| `Outbox/When_expiry_brings_the_outbox_under_its_limit_before_a_waiting_compaction_should_not_compact` | ✅ ‡ | ❌ 1 | ✅ | ✅ 3 |
| `Outbox/When_expiry_throws_in_the_same_cleanup_pass_as_a_compaction_should_still_compact` | ❌ 6 | ✅ | ❌ 6 | ✅ 3 |

† On master this value depends on timing. Scans that reach the lock while it is held are dropped, but this test releases the lock straight after its reads, so they usually find it free and run. The blocking lock gives 6 every time.

‡ This test does not fail against master, because master drops the waiting compaction altogether. It pins a fault that the blocking lock introduced (over-trimming from an out-of-date count), not the original bug.

**Second contributing cause, thread-pool starvation from other tests (test-only fix, `e72f0fa91`):**

- `ConcurrentStartGuardTests` puts 20 `Task.Run` items on a `Barrier`, and the scheduler same-id concurrency test puts 100.
- In CI they starved the pool for 19–27s, which delayed the background cleanup past the tests' waits. This was seen on PR #4482.
- `ConcurrentStartGuardTests` was added in `af0501b35` (June), after #4116.
- Both test classes now run in the non-parallel `ThreadPoolSaturating` collection.

**Results:**

| Suite | Result |
|---|---|
| InMemory | 159/159 on net9.0 and net10.0 |
| Core (net9.0, after merging master) | 1498 passed, 7 skipped |
| 18 cleanup tests, 10 runs with `DOTNET_PROCESSOR_COUNT=2` | green every run |

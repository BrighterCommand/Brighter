# Tasks: 0039 - Circuit breaker stale tripped topics (#4628)

**Spec**: `specs/0039-circuit-breaker-stale-tripped-topics/requirements.md`
**ADR**: `docs/adr/0089-distributed-outbox-circuit-breaker.md` (Accepted). Tasks implement the ADR. They do not re-design it.

This list follows the ADR's *Implementation Approach* steps 1-16 in order. **Phase 1** (steps 1-5) puts a time limit on `InMemoryOutboxCircuitBreaker`. It touches only core and **merges as its own PR**. **Phase 2** (steps 6-16) adds the distributed breaker, the trip stores, the async roles, sweeper refresh, health signals, SQL stores with provisioning, real-database tests and the documentation.

Conventions:
- **Tags** (the label `/spec:implement` and `/spec:ralph-implement` dispatch on):
  - `TEST + IMPLEMENT`: use `/tdd:test-first`; the ⛔ gate applies; the test must be RED before any production code exists.
  - `CHARACTERISE`: use `/tdd:test-first`; the ⛔ gate applies. The test is expected to be **green on arrival** because earlier tasks already deliver the behaviour. Each such task names a 🔁 RED mutation. Apply it to *production* code (never the test), check the test fails **on the assertion the task is about**, revert, check green, and run the full suite. Commit the test alone as `test:`. The mutation is never committed. A `CHARACTERISE` test that is RED on arrival is simply RED: the behaviour is missing, so continue as `TEST + IMPLEMENT` and add the minimum production code that the task's *Depends on* tasks should have delivered, saying so in the commit. A `TEST + IMPLEMENT` test that is green on arrival, or a `CHARACTERISE` mutation that fails some other way, is a **stop and ask** (`ralph-implement` returns `ALREADY_COMPLETE` or `FAILED` for the owner to review) (`.agent_instructions/testing.md`, *When a new test passes on first run*).
  - `STRUCTURAL`: no behaviour change. Use `/refactor:tidy-first`. Commit as `refactor:` / `test:`.
  - `PROJECT`: project or package wiring, build and verification steps. Commit as `chore:`.
  - `DOC`: ADR notes, release notes and the Docs list. Commit as `docs:`.
- Production code in a `TEST + IMPLEMENT` task is limited to what that task's test requires. Behaviour that a later test specifies is added by that later task, not early.
- One deliberate exception to the rule above: a multi-topic store write applies its topics in ordinal topic order (T2.57-T2.59a, the SQL stores; the in-memory store of T2.8 holds one lock and needs no order), because the ADR forbids any other order (deadlock avoidance) and no single-write contract test can fail on it. T2.60 pins that order for the SQL stores with a named 🔁 RED mutation and the real-database deadlock tests.
- STRUCTURAL tasks: the named suites must be green before and after.
- Commit shape for a behavioural task: a `feat:` (or `test:`) commit for the code, then a separate `docs:` commit that ticks the box in this file.
- Production SQL stores are tested only against real databases (MsSql, PostgreSql, MySql). Those tests reuse the core store contract suite from Phase 2b.

## Dependency and phase overview

```
Phase 1 (core only, own PR)
  T1.1 (structural) -> T1.2 (option) -> T1.4 (expiry) -> T1.3, T1.5..T1.12 (copy of D, count, re-trip, Reset cause, concurrency, default)
  -> T1.13 (stale-trip sweep) -> T1.14, T1.14a (release note, ADR notes) -> T1.15 PR BOUNDARY (merge before Phase 2)

Phase 2 (starts only after T1.15 has merged)
  2a T2.1 (structural)            -> 2b T2.2..T2.8 (options, store contract)
  -> 2c T2.9..T2.24 incl. T2.16a (breaker view, coalesced writes, announcements, elapsed time)
  -> 2d T2.25..T2.38 (T2.37 retired) incl. T2.28a, T2.30a, T2.30b (timeouts, fallback, recovery, failure telemetry)
  -> 2e T2.39..T2.48 incl. T2.39a, T2.39c, T2.39d, T2.43a (mediator uses async role)
  -> 2f T2.49..T2.51 (sweeper refresh) -> 2g T2.52..T2.56 (health signals)
  -> 2h T2.57..T2.60 (SQL stores incl. T2.57a/b..T2.59a/b and T2.60a-c, real DBs) -> 2i T2.61..T2.65 (provisioning)
  -> 2j T2.66..T2.67 (real-DB acceptance) -> 2k T2.68..T2.69 (dependency guard, verification)
  -> 2l T2.70..T2.72 (ADR notes, release notes, Docs list)
```

---

## Phase 1 - Time-bound the in-memory breaker

### Phase 1a - Structural preparation of the in-memory breaker

- [ ] **STRUCTURAL: T1.1 - Make in-memory trip entries immutable and inject a clock, with no behaviour change**
  - **USE COMMAND**: `/refactor:tidy-first make InMemoryOutboxCircuitBreaker entries immutable records with reference equality holding count and timestamp, add a trailing optional TimeProvider, replace AddOrUpdate decrement with TryUpdate retry, compare entries by reference on conditional remove`
  - Change:
    - `InMemoryOutboxCircuitBreaker.cs:44-53`: add a trailing `TimeProvider? timeProvider = null` parameter, defaulting to `TimeProvider.System`.
    - Change the dictionary value from `int` to a private sealed record (as ADR 0089 says) that overrides `Equals(Entry?)` to `ReferenceEquals` and `GetHashCode` to `RuntimeHelpers.GetHashCode`, because `TryUpdate` and the conditional remove compare with the default comparer and a record's default is value equality. It holds the remaining count and a `GetTimestamp()` value. Every trip creates a new instance.
    - A decrement replaces the entry through `TryUpdate(key, entry with count - 1, observedEntry)` and retries if that fails. It has no add branch, so it replaces `AddOrUpdate` at `:75`, and a key that another thread removed is never added back.
    - The conditional remove at `:81-98` compares entries by reference (the record's overridden equality), so a stale decrement or remove cannot hit a fresh trip with an equal count and timestamp.
    - No D check and no Reset handling here: D arrives in T1.2, and T1.4, T1.7, T1.8 and T1.9 add the expiry behaviour with their tests.
    - Leave the read of `CooldownCount` at each trip unchanged.
  - Green before and after: all of `tests/Paramore.Brighter.Core.Tests/CircuitBreaker/` (including `When_cooldown_runs_concurrently_should_be_atomic.cs` and `When_Cooldown_On_TrippedTopic.cs`), `Sweeping/`, `Observability/Metrics/` and `Confirmation/`.
  - Covers: NFR-1 (constructor source compatible), NFR-5, NFR-8 (groundwork), FR-2.5 (D half). ADR step 1.
  - Depends on: none
  - References: `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs:44-53,:75,:81-98,:104`. ADR 0089 *Phase 1: an in-memory trip ends by count or by time* and *`InMemoryOutboxCircuitBreaker`*. ADR 0028.

### Phase 1b - Maximum trip duration option

- [ ] **TEST + IMPLEMENT: T1.2 - The in-memory breaker rejects an out-of-range maximum trip duration and defaults to 5 minutes**
  - **USE COMMAND**: `/tdd:test-first when the maximum trip duration is out of range should reject the in-memory breaker at construction`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_the_maximum_trip_duration_is_out_of_range_should_reject_the_breaker.cs`
  - Test should verify:
    - `TimeSpan.Zero`, -1 s, `Timeout.InfiniteTimeSpan`, `TimeSpan.MaxValue` and 24 h + 1 ms each throw `ArgumentOutOfRangeException` from `new InMemoryOutboxCircuitBreaker(options)`.
    - 1 ms and exactly 24 h are accepted.
    - A default `OutboxCircuitBreakerOptions` has `MaximumTripDuration` = 5 minutes and `CooldownCount` = 10, unchanged.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `public TimeSpan MaximumTripDuration { get; set; } = TimeSpan.FromMinutes(5);` beside `CooldownCount` at `OutboxCircuitBreakerOptions.cs:37`.
    - Validate the range in the `InMemoryOutboxCircuitBreaker` constructor. The in-memory breaker validates D only.
  - Covers: FR-2, FR-2.1 (D), FR-2.2, FR-2.3, AC-6 (D half). ADR step 2. OQ-2, OQ-10 (D name).
  - Depends on: T1.1
  - References: `src/Paramore.Brighter/CircuitBreaker/OutboxCircuitBreakerOptions.cs:37`. ADR 0089 *`OutboxCircuitBreakerOptions`* and *Why 5 minutes for D*.

### Phase 1c - Expiry and the Reset cause

- [ ] **TEST + IMPLEMENT: T1.4 - An in-memory trip ends at exactly D with no cooldown or sweep**
  - **USE COMMAND**: `/tdd:test-first when a tripped topic reaches the maximum trip duration with no cooldown should no longer be listed`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_a_trip_reaches_the_maximum_trip_duration_without_cooldown_should_no_longer_be_listed.cs`
  - Test should verify:
    - `CooldownCount = 10`, D = 5 min, `FakeTimeProvider`. `orders` is tripped at t = 0 and `CoolDown` is never called.
    - `TrippedTopics` contains `orders` at t = 4 min 59.999 s.
    - It is empty at exactly t = 5 min and at t = 10 min.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add D as a readonly field copied from the options in the constructor.
    - Add an expiry pass at the head of `TrippedTopics` (`:58`) only. The pass removes every entry whose `GetElapsedTime(entry.Timestamp) >= D`, using the conditional remove by reference, and `TrippedTopics` never lists an ended entry, even when another thread wins the remove.
    - Leave `CoolDown` and `TripTopic` without an expiry pass. T1.9 and T1.8 add them with the tests that need them.
  - Covers: FR-1, FR-1.2, FR-1.3, NFR-6 (D half), AC-1.
  - Depends on: T1.2
  - References: `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs:58`. ADR 0089 Phase 1 table, row 1. ADR step 3.

- [ ] **CHARACTERISE: T1.3 - The in-memory breaker copies D at construction and keeps reading CooldownCount at each trip**
  - **USE COMMAND**: `/tdd:test-first when the options change after the in-memory breaker is built should keep the maximum trip duration it was built with`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_the_options_change_after_construction_should_keep_the_maximum_trip_duration_it_was_built_with.cs`
  - Test should verify:
    - Build with D = 5 min, then set `options.MaximumTripDuration` = 1 min. A trip at t = 0 is still listed at t = 4 min 59 s and has ended at t = 5 min.
    - Changing `CooldownCount` after construction still affects the next trip, as it does today (NFR-1).
  - 🔁 RED mutation: read `options.MaximumTripDuration` at each expiry check instead of the value copied at construction. The test must fail on the "still listed at t = 4 min 59 s" assertion, because the trip now ends at 1 min. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T1.4 copies D into a readonly field at construction, and the per-trip read of `CooldownCount` is unchanged (T1.1). This test guards both.
  - Covers: FR-2.1 (read once), AC-6 (options-change clause, D), NFR-1.
  - Depends on: T1.4
  - References: `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs`. ADR 0089 *Deliberate readings*: "`CooldownCount` stays live".

- [ ] **CHARACTERISE: T1.5 - The cooldown count still ends a trip first when it is shorter than D**
  - **USE COMMAND**: `/tdd:test-first when the cooldown count runs out before the maximum trip duration should end the trip on the third cooldown`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_the_cooldown_count_runs_out_before_the_maximum_trip_duration_should_end_the_trip.cs`
  - Test should verify:
    - `CooldownCount = 2`, D = 5 min, and the clock never moves.
    - After 2 `CoolDown` calls `orders` is still listed. The 3rd call ends it.
  - 🔁 RED mutation: make the expiry pass also treat the count as expired at 0, so the trip ends on the 2nd cycle. The test must fail on the "still listed after 2 `CoolDown` calls" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. The count rule is unchanged from today (the (N+1)th cycle ends the trip, through the `TryUpdate` decrement from T1.1). This test pins it next to D.
  - Covers: FR-1 (count limit unchanged), FR-2.3, AC-2.
  - Depends on: T1.4
  - References: `tests/Paramore.Brighter.Core.Tests/CircuitBreaker/When_Cooldown_On_TrippedTopic.cs`. ADR 0089 Phase 1 table, row 2.

- [ ] **CHARACTERISE: T1.6 - A re-trip restarts both the D clock and the cooldown count**
  - **USE COMMAND**: `/tdd:test-first when a tripped topic is re-tripped should restart both the maximum trip duration and the cooldown count`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_a_tripped_topic_is_re_tripped_should_restart_both_limits.cs`
  - Test should verify:
    - D = 5 min. Trip at t = 0, re-trip at t = 4 min. The topic is listed at t = 8 min 59 s and not listed at t = 9 min.
    - `CooldownCount = 2`. Trip, run 2 cycles, re-trip, run 2 more cycles: still listed. The 3rd cycle ends it.
  - 🔁 RED mutation: in `TripTopic` on a live entry, reuse the observed entry's timestamp and count instead of the fresh ones. The test must fail on the "listed at t = 8 min 59 s" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T1.1 makes every trip a fresh entry (`CooldownCount`, fresh timestamp), and T1.4 judges by that timestamp. This test pins the re-trip rule (Phase 1 table, row 4).
  - Covers: FR-1.1, AC-3.
  - Depends on: T1.4
  - References: `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs:104-120`.

- [ ] **TEST + IMPLEMENT: T1.7 - A time-ended trip announces one Reset, from the first observing call, naming its cause**
  - **USE COMMAND**: `/tdd:test-first when a trip ends by maximum trip duration should announce exactly one reset from the first observing call naming the cause`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_a_trip_ends_by_maximum_trip_duration_should_announce_one_reset_naming_the_cause.cs`
  - Test should verify:
    - With a tracer and log capture: trip `orders`, move past D, read `TrippedTopics` 3 times and call `CoolDown` once.
    - Exactly one Reset span, one Information reset log and one `paramore.brighter.circuit_breaker.trips` increment with the Reset operation are produced, all by the first read.
    - The log names `maximum trip duration`.
    - A separate trip ended by cooldown produces one Reset whose log names `cooldown`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add a `{ResetCause}` placeholder to the Reset `LoggerMessage` (`:127-137`, message at `:135`).
    - The expiry pass in `TrippedTopics` announces the Reset of every entry it removes, with cause `maximum trip duration`. The decrement path in `CoolDown` announces with cause `cooldown`.
    - Update `When_a_tripped_topic_cools_down_should_log_information_and_export_a_reset_span.cs` (assertion near `:50`) to the new template in this task. This is the one intended change to an existing assertion (FR-1.4, NFR-1).
  - Covers: FR-1.4, FR-8.6 (in-memory), FR-7.2 (Reset log wording), AC-5 (parts 1-2).
  - Depends on: T1.4
  - References: `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs:127-137`. `tests/Paramore.Brighter.Core.Tests/CircuitBreaker/When_a_tripped_topic_cools_down_should_log_information_and_export_a_reset_span.cs`. `docs/adr/0058-outbox-circuit-breaker-trip-logging.md`.

- [ ] **TEST + IMPLEMENT: T1.8 - A TripTopic that first observes an ended trip announces the Reset before the Trip**
  - **USE COMMAND**: `/tdd:test-first when trip topic is the first call after a trip has ended should announce the reset before the trip`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_trip_topic_is_the_first_call_after_a_trip_ended_should_announce_reset_before_trip.cs`
  - Test should verify:
    - `orders` is past D and not yet observed. The next call, `TripTopic(orders)`, produces a Reset and then a Trip, not a ReTrip, in that order.
    - `orders` is past D and not yet observed. The next call, `TripTopic(invoices)`, produces the Reset of `orders` and then the Trip of `invoices`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the expiry pass to the head of `TripTopic` (`:104`), before it classifies the trip, so the Reset of an ended entry is announced first and the same topic then becomes a fresh entry announced as a Trip, not a ReTrip (Phase 1 table, row 3).
  - Covers: FR-1.4, AC-5 (parts 3-4).
  - Depends on: T1.7
  - References: `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs:104`.

- [ ] **TEST + IMPLEMENT: T1.9 - When both limits have passed, the maximum trip duration is named as the cause**
  - **USE COMMAND**: `/tdd:test-first when the first call after the maximum trip duration would also run the cooldown count out should name the maximum trip duration`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_both_limits_pass_in_one_cooldown_should_name_the_maximum_trip_duration.cs`
  - Test should verify:
    - `CooldownCount = 1`, D = 5 min. `orders` is tripped at t = 0 and one `CoolDown` runs at t = 1 min.
    - The 2nd `CoolDown`, at t = 5 min, produces exactly one Reset, and it names the maximum trip duration.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the expiry pass to the head of `CoolDown` (`:66`), before any decrement, so the Reset names the maximum trip duration when D has passed.
  - Covers: FR-1.4, AC-5 (part 5).
  - Depends on: T1.8
  - References: ADR 0089 Phase 1 invariant: "Time wins when both limits have passed".

- [ ] **CHARACTERISE: T1.10 - Concurrent calls at expiry produce exactly one Reset**
  - **USE COMMAND**: `/tdd:test-first when two cooldowns run concurrently at expiry should produce exactly one reset and never list the topic again`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_two_cooldowns_run_concurrently_at_expiry_should_produce_one_reset.cs`
  - Test should verify:
    - Over 1,000 repetitions, two parallel `CoolDown` calls at t = D produce exactly one Reset.
    - `TrippedTopics` never lists the topic afterwards.
  - 🔁 RED mutation: announce the Reset before attempting the conditional remove, so two racing `CoolDown` calls both announce. The test must fail on the "exactly one Reset" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T1.1 removes by reference and T1.7-T1.9 announce only when the remove succeeds. This test guards NFR-5 at expiry.
  - Covers: FR-1.4, NFR-5, NFR-8. ADR step 3 (concurrent-expiry test).
  - Depends on: T1.9
  - References: `tests/Paramore.Brighter.Core.Tests/CircuitBreaker/When_cooldown_runs_concurrently_should_be_atomic.cs`.

- [ ] **CHARACTERISE: T1.11 - A trip that races the call ending the topic is never lost (in-memory)**
  - **USE COMMAND**: `/tdd:test-first when a trip races the call that would end the topic should leave the topic tripped with its limits restarted`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_a_trip_races_the_ending_call_should_restart_the_cooldown_at_the_trip.cs`
  - Test should verify:
    - Before each repetition `orders` is tripped and cooled `CooldownCount` times (count 0), so the first `CoolDown` of the race ends it, and the race tasks start together on a barrier.
    - `CooldownCount = 2`, `FakeTimeProvider`. `TripTopic`, `CoolDown` and `TrippedTopics` run concurrently from many tasks, repeated 1,000 times. The race makes at most `CooldownCount` `CoolDown` calls after the racing trip.
    - No exception is thrown, the topic is still tripped, and no reader sees a half-updated state.
    - Exactly `CooldownCount` - k further `CoolDown` calls leave it listed, where k is the number of race `CoolDown` calls that completed after the trip. The next call removes it.
    - In a separate run, moving the clock to trip time + D removes it.
  - 🔁 RED mutation: replace the conditional remove by reference with `TryRemove(key)`, so the call that ends the topic can delete a fresh trip that raced it. The test must fail on the "the topic is still tripped after the race" assertion within its 1,000 repetitions. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. The immutable entry, the no-add `TryUpdate` decrement and the remove by reference (T1.1) already deliver this. This test extends `When_cooldown_runs_concurrently_should_be_atomic` to the time limit.
  - Covers: NFR-5, NFR-8, AC-37 (in-memory half).
  - Depends on: T1.6, T1.10
  - References: `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs`.

- [ ] **CHARACTERISE: T1.12 - Default D leaves default single-instance sweeping unchanged**
  - **USE COMMAND**: `/tdd:test-first when every sweep obtains the lock on schedule with default options should exclude a tripped topic from exactly ten sweeps`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_sweeps_run_on_schedule_with_default_options_should_exclude_a_tripped_topic_from_exactly_ten_sweeps.cs`
  - Test should verify:
    - Default options, `FakeTimeProvider`, `TimerInterval` 5 s, every sweep obtains the lock and takes no time.
    - `orders` tripped at t = 0 is excluded from exactly 10 sweeps and included in the 11th.
  - 🔁 RED mutation: set the default `MaximumTripDuration` to 30 s, so D ends the trip early. The test must fail on the "excluded from exactly 10 sweeps" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T1.2-T1.4 deliver this. Adjust the default only if the test proves the margin wrong.
  - Covers: FR-2.2, FR-7.2, AC-7.
  - Depends on: T1.4
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/When_a_topic_trips_on_the_shared_breaker_should_report_it_until_cooldown_evicts_it.cs`. `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/SweepableOutbox.cs`.

### Phase 1d - Stale-trip sweep

- [ ] **CHARACTERISE: T1.13 - An instance with a stale trip sweeps the topic once it obtains the lock**
  - **USE COMMAND**: `/tdd:test-first when an instance that held a trip without the lock obtains the lock after the maximum trip duration should sweep with no tripped topics`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_stale_trip_instance_obtains_the_lock_after_the_maximum_trip_duration_should_sweep_the_topic.cs`
  - Test should verify:
    - A `TimedOutboxSweeper` with a shared-singleton `InMemoryOutboxCircuitBreaker` (D = 5 min) and a lock double that withholds the lock.
    - `orders` is tripped at t = 0, the clock moves to t = 6 min, and the double then grants the lock.
    - That sweep calls `OutstandingMessagesAsync` with no tripped topics.
  - 🔁 RED mutation: change the expiry comparison to 2 × D in both the `TrippedTopics` and `CoolDown` passes (the sweep's `CoolDown` runs before its read of `TrippedTopics`, so a single pass is not enough). The test must fail on the "no tripped topics" assertion, because the trip is still live at t = 6 min. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T1.4 delivers this. This test proves it through a real sweeper (ADR step 4).
  - Covers: FR-1, FR-1.2, AC-4.
  - Depends on: T1.4
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/SwitchableDistributedLock.cs`, `HeldDistributedLock.cs`, `SweepableOutbox.cs`. `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs`. ADR step 4.

### Phase 1e - Phase 1 release note and PR boundary

- [ ] **DOC: T1.14 - Check the Phase 1 release-note items in the existing marked section**
  - The section `### Circuit-broken topics no longer stay tripped for ever ...` with marker `<!-- spec: 0039-circuit-breaker-stale-tripped-topics -->` already exists under `## Master` in `release_notes.md` (from commit 7f650ff19). Check it against what Phase 1 ships. Do not rewrite it blindly.
  - It must state: `MaximumTripDuration`, its 5-minute default and the effect on existing `InMemoryOutboxCircuitBreaker` users (FR-7.2); that the Reset log names its cause; and the trailing optional `TimeProvider` constructor parameter.
  - The third breaking-change bullet joins the `InMemoryOutboxCircuitBreaker` and `TimedOutboxSweeper` constructors. Split it: Phase 1 keeps the in-memory constructor part only.
  - Because Phase 1 merges alone, adjust the section with `/spec:write_release_notes` so that every statement is true of what has merged. Hold back the Phase 2 claims (`DistributedOutboxCircuitBreaker`, request-path write, health changes, the `TimedOutboxSweeper` constructor and `BoxType` bullets) until T2.71.
  - Covers: FR-9.2 (Phase 1 items), FR-7.2. ADR step 5.
  - Depends on: T1.2, T1.9
  - References: `release_notes.md:3-17`.

- [ ] **DOC: T1.14a - Amend ADRs 0028 and 0058 for the Phase 1 changes**
  - Add a short amendment note to the Status section of each ADR, pointing to ADR 0089 and naming only what Phase 1 changes:
    - `docs/adr/0028-support-circuit-breaking-of-topics.md`: an in-memory trip also ends at the maximum trip duration D, and the cooldown no longer has to advance on the lock holder's sweep to end a trip.
    - `docs/adr/0058-outbox-circuit-breaker-trip-logging.md`: the in-memory Reset log names its cause.
  - T2.70 extends these notes with the Phase 2 parts, and adds the notes for ADRs 0085 and 0086.
  - Covers: FR-9.1 (Phase 1 part), ADR step 16 (Phase 1 part). Amendments to 0028 and 0058.
  - Depends on: T1.7
  - References: `docs/adr/0089-distributed-outbox-circuit-breaker.md` (*Amends*).

- [ ] **PROJECT: T1.15 - Phase 1 PR boundary: run Core.Tests Sweeping/Metrics/CircuitBreaker suites + AC-31 + AC-38 check, merge as own PR before starting Phase 2**
  - Run `tests/Paramore.Brighter.Core.Tests` filtered to `CircuitBreaker`, `Sweeping`, `Observability/Metrics` and `Confirmation`. All must be green.
  - AC-31: `ListedTopicsCircuitBreaker` and `ThrowingOutboxCircuitBreaker` are unchanged and compile, and every existing `new InMemoryOutboxCircuitBreaker(...)` call compiles unchanged. The only changed assertion is the Reset template (T1.7).
  - AC-38: compare `dotnet list package --include-transitive` for `src/Paramore.Brighter` with the previous release. No new package.
  - Confirm that Phase 1 changes no interface, no package graph and no health signal.
  - Open and merge the Phase 1 PR. **Phase 2 work starts only after this merge.**
  - Covers: AC-31, AC-38 (Phase 1), NFR-1, NFR-7. ADR "Phase 1 ends here". OQ-13.
  - Depends on: T1.1-T1.14a
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/ListedTopicsCircuitBreaker.cs`, `ThrowingOutboxCircuitBreaker.cs`.

---

## Phase 2 - Distributed circuit breaker

### Phase 2a - Thread a trip batch through dispatch

- [ ] **STRUCTURAL: T2.1 - Add an optional trip-batch parameter to the dispatch methods, passing null everywhere**
  - **USE COMMAND**: `/refactor:tidy-first add a trailing IAmATripBatch? trips parameter to DispatchAsync and BulkDispatchAsync, pass null from every caller, route TripTopic through the batch when present, and add empty role interfaces`
  - Change:
    - `OutboxProducerMediator.cs`: give `DispatchAsync` (`:1257`) and `BulkDispatchAsync` (`:1152`) a trailing `IAmATripBatch? trips` parameter.
    - Pass `null` from every caller (`:486`, `:776`, `:780`).
    - The helper at `:1542-1546` calls `trips?.TripTopic` when a batch is present, and otherwise the breaker's `TripTopic`.
    - Add `IAmAnOutboxCircuitBreakerAsync`, `IAmATripBatch` and `IAmASharedOutboxCircuitBreaker`, with the members ADR 0089 specifies, and `TrippedTopicStoreStatus`, in `Paramore.Brighter.CircuitBreaker`, as declarations only: nothing implements them yet, and T2.9 adds the implementing class with stubs. `IAmAnOutboxCircuitBreaker` is not touched.
  - Green before and after: `Core.Tests` `CircuitBreaker/`, `Sweeping/`, `Confirmation/`, `CommandProcessors/Clear/`, `CommandProcessors/Post/`, `Observability/Metrics/`.
  - Covers: NFR-1, OQ-7 (role names). ADR step 6.
  - Depends on: T1.15
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:486,:776,:780,:1152,:1257,:1542-1546`. `src/Paramore.Brighter/CircuitBreaker/IAmAnOutboxCircuitBreaker.cs`.

### Phase 2b - Options and the trip-store contract

- [ ] **PROJECT: T2.2 - Make the store contract suite reusable by core and by the SQL test projects**
  - Put the abstract store contract suites in `tests/Paramore.Brighter.Base.Test/CircuitBreaker/`, following the pattern of `Outbox/OutboxTest.cs`. There are two: `TrippedTopicStoreWriteContractTest` (T2.4, T2.5, T2.8) and `TrippedTopicStoreEndedContractTest` (T2.6, T2.7). Each store has one concrete runner per suite. Add a `ProjectReference` from `Paramore.Brighter.Core.Tests` to `Paramore.Brighter.Base.Test`. The MSSQL, PostgresSQL and MySQL test projects already reference Base.Test.
  - Add what T2.66 and T2.67 need and these projects lack, with versions in `Directory.Packages.props`: `Microsoft.Extensions.TimeProvider.Testing` to `Paramore.Brighter.MSSQL.Tests` and `Paramore.Brighter.MySQL.Tests` (PostgresSQL.Tests has it), and the OpenTelemetry packages the telemetry assertions use (`OpenTelemetry`, `OpenTelemetry.Exporter.InMemory`) to `Paramore.Brighter.MySQL.Tests`. Add the same two OpenTelemetry packages to `Paramore.Brighter.MSSQL.Tests` and `Paramore.Brighter.PostgresSQL.Tests` if they are missing.
  - Build the test solution to confirm the new reference causes no type or package conflicts.
  - Covers: ADR step 7 ("write this contract suite so that the SQL stores can reuse it"), ADR step 15.
  - Depends on: T2.1
  - References: `tests/Paramore.Brighter.Base.Test/Paramore.Brighter.Base.Test.csproj`. `tests/Paramore.Brighter.Base.Test/Outbox/OutboxTest.cs`. `tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj`.

- [ ] **TEST + IMPLEMENT: T2.3 - The options expose the distributed defaults: C = 50 s, leeway zero, store timeout 2 s**
  - **USE COMMAND**: `/tdd:test-first when outbox circuit breaker options are created should default cooldown duration to fifty seconds, clock skew leeway to zero and store timeout to two seconds`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker"
  - Test file: `When_outbox_circuit_breaker_options_are_created_should_default_the_distributed_durations.cs`
  - Test should verify:
    - `CooldownDuration` == 50 s, `ClockSkewLeeway` == `TimeSpan.Zero`, `StoreTimeout` == 2 s.
    - `CooldownCount` and `MaximumTripDuration` are unchanged.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the three properties to `OutboxCircuitBreakerOptions.cs`. Validation belongs to the distributed breaker (T2.9).
  - Covers: FR-2, FR-2.4, NFR-3 (configurable), OQ-4, OQ-6, OQ-10.
  - Depends on: T2.1
  - References: `src/Paramore.Brighter/CircuitBreaker/OutboxCircuitBreakerOptions.cs`. ADR 0089 options table and *Why 2 seconds for the store timeout*.

- [ ] **TEST + IMPLEMENT: T2.4 - The trip store keeps the later stamp and returns the prior one**
  - **USE COMMAND**: `/tdd:test-first when a trip is written to the store should keep the later stamp and return the prior stamp`
  - Test location: "tests/Paramore.Brighter.Base.Test/CircuitBreaker" (abstract contract) with a concrete `InMemoryTrippedTopicStore` runner in "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `TrippedTopicStoreWriteContractTest.cs` (method `When_a_trip_is_written_should_keep_the_later_stamp_and_return_the_prior`), runner `When_the_in_memory_trip_store_runs_the_write_contract_should_pass.cs`
  - Test should verify:
    - First write: `PriorTripTime` is null, `StoredTripTime` equals the written stamp, `LeftEnded` is false.
    - A later write returns the old stamp as prior and stores the later stamp.
    - An earlier write returns the stored stamp as prior and leaves the stored stamp unchanged.
    - `ReadAsync` returns the row in `Live`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `IAmATrippedTopicStore`, `TopicTrip`, `TripWriteResult`, `TrippedTopicStoreRead` and `InMemoryTrippedTopicStore` (one lock) in `src/Paramore.Brighter/CircuitBreaker/`, with the signatures in ADR 0089 *`IAmATrippedTopicStore`*.
  - Covers: FR-3.8, FR-7.4 (in-memory store), OQ-9 (later-of upsert). ADR step 7.
  - Depends on: T2.2, T2.3
  - References: ADR 0089 *`IAmATrippedTopicStore`* and *The trip table and the SQL stores*.

- [ ] **CHARACTERISE: T2.5 - A microsecond-truncated stamp reads back equal**
  - **USE COMMAND**: `/tdd:test-first when a stamp truncated to whole microseconds is written should read back equal`
  - Test location: "tests/Paramore.Brighter.Base.Test/CircuitBreaker"
  - Test file: `TrippedTopicStoreWriteContractTest.cs` (method `When_a_microsecond_stamp_is_written_should_read_back_equal`)
  - Test should verify:
    - A stamp with sub-microsecond ticks, truncated as the breaker takes it, comes back from `ReadAsync` and `WriteAsync` exactly equal (`TripTime`, `StoredTripTime`).
  - 🔁 RED mutation: round the stamp to whole milliseconds in `InMemoryTrippedTopicStore.WriteAsync`. The test must fail on the equality assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. The in-memory store stores the value unchanged, so this test only pins the contract the SQL stores must meet (T2.57-T2.59b). The shared stamp-truncation helper is added by T2.10.
  - Covers: NFR-6 (stamp stored unchanged). ADR *Terms*: Stamp. ADR step 7.
  - Depends on: T2.4
  - References: ADR 0089 *Terms*.

- [ ] **TEST + IMPLEMENT: T2.6 - A read marks ended rows and gives each one to exactly one caller, never deleting**
  - **USE COMMAND**: `/tdd:test-first when many readers read an ended row concurrently should return it as ended to exactly one caller`
  - Test location: "tests/Paramore.Brighter.Base.Test/CircuitBreaker"
  - Test file: `TrippedTopicStoreEndedContractTest.cs` (method `When_readers_race_over_an_ended_row_should_return_it_to_exactly_one`), runner `When_the_in_memory_trip_store_runs_the_ended_contract_should_pass.cs`
  - Test should verify:
    - Over 1,000 repetitions of parallel `ReadAsync(now, tripLength)`, a row stamped at or before `now - tripLength` appears in `Ended` for exactly one caller and in no caller's `Live`.
    - A row stamped after the cutoff stays in `Live`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `ReadAsync`, mark ended (set `EndedAt = now`), return the marked rows, then return the unmarked rows.
  - Covers: FR-4.4, OQ-9, NFR-5. ADR step 7.
  - Depends on: T2.4
  - References: ADR 0089 *How a read ends trips*. Alternative 15 (no `DELETE`).

- [ ] **TEST + IMPLEMENT: T2.7 - A write over an ended row revives it only with a later stamp**
  - **USE COMMAND**: `/tdd:test-first when a trip is written over an ended row should revive it only with a later stamp and otherwise leave it ended`
  - Test location: "tests/Paramore.Brighter.Base.Test/CircuitBreaker"
  - Test file: `TrippedTopicStoreEndedContractTest.cs` (method `When_a_trip_is_written_over_an_ended_row_should_revive_only_with_a_later_stamp`), run by the runner T2.6 created
  - Test should verify:
    - A row marked ended by `ReadAsync` (T2.6), then written with an equal or earlier stamp: `LeftEnded` is true, prior is null, and the row is not in `Live`.
    - Written with a later stamp: `LeftEnded` is false, prior is null, and the row is in `Live` with the new stamp, so the marked row was never deleted.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Apply the `EndedAt` and prior rules from the SQL table to the in-memory store's write, over a row that T2.6's read has marked.
  - Covers: FR-3.8, FR-4.4, NFR-5.
  - Depends on: T2.6
  - References: ADR 0089 *How a read ends trips* (re-trip race paragraph).

- [ ] **TEST + IMPLEMENT: T2.8 - A store write rejects duplicate topics and is all-or-nothing**
  - **USE COMMAND**: `/tdd:test-first when a write contains the same topic twice should reject it with argument exception and change nothing`
  - Test location: "tests/Paramore.Brighter.Base.Test/CircuitBreaker"
  - Test file: `TrippedTopicStoreWriteContractTest.cs` (method `When_a_write_repeats_a_topic_should_reject_it_and_change_nothing`)
  - Test should verify:
    - `WriteAsync([orders, invoices, orders])` throws `ArgumentException`, and no topic is stored.
    - A valid multi-topic write returns one result per topic.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Validate distinct topics, then apply all topics under one lock.
  - Covers: FR-3.7 (one batched write), NFR-2. ADR step 7.
  - Depends on: T2.4
  - References: ADR 0089 *`IAmATrippedTopicStore`* (error conditions).

### Phase 2c - Distributed breaker view and coalesced writes

- [ ] **TEST + IMPLEMENT: T2.9 - The distributed breaker validates C, the leeway and the store timeout at construction**
  - **USE COMMAND**: `/tdd:test-first when the distributed breaker is built with an out of range duration should throw argument out of range`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_distributed_breaker_is_built_with_an_out_of_range_duration_should_throw.cs`
  - Test should verify:
    - C rejects Zero, -1 s, `Timeout.InfiniteTimeSpan`, `TimeSpan.MaxValue` and 24 h + 1 ms, and accepts 1 ms and 24 h.
    - The leeway rejects negative values and values over 24 h, and accepts zero and exactly 24 h.
    - `StoreTimeout` rejects Zero, -1 s, `Timeout.InfiniteTimeSpan`, `TimeSpan.MaxValue`, 999 ms and 60 s + 1 ms, and accepts 1 s and 60 s.
    - A null store throws `ArgumentNullException`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `DistributedOutboxCircuitBreaker(IAmATrippedTopicStore store, OutboxCircuitBreakerOptions? options = null, IAmABrighterTracer? tracer = null, InstrumentationOptions instrumentationOptions = InstrumentationOptions.All, TimeProvider? timeProvider = null)` implementing the three roles. Members whose behaviour a later task's test drives are `throw new NotImplementedException()` stubs until that task: `TripTopic` and `TrippedTopics` (T2.10), `BeginTripBatch` (T2.12), `ReadTrippedTopicsAsync` (T2.19), `StoreStatus` (T2.25) and `StartRefresh` (T2.29). `CoolDown` is an empty body. A stub is replaced only by its named task, never early.
    - Copy C, the leeway and `StoreTimeout` at construction.
  - Covers: FR-2.1 (C, leeway, store timeout), FR-2.5 (C half), AC-6 (distributed half). ADR step 8 (validation slice).
  - Depends on: T2.3, T2.4
  - References: ADR 0089 *`DistributedOutboxCircuitBreaker`* and the options table.

- [ ] **TEST + IMPLEMENT: T2.10 - A sync trip updates the view at once and never waits on the store**
  - **USE COMMAND**: `/tdd:test-first when a synchronous callback trips a topic while the store blocks should return at once and list the topic`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_synchronous_trip_happens_while_the_store_blocks_should_return_at_once_and_list_the_topic.cs`
  - Test should verify:
    - With a store double that blocks for ever, sync `TripTopic(orders)` returns within 1 s and `TrippedTopics` lists `orders` immediately.
    - The write is attempted off the calling thread.
    - Sync `TrippedTopics` makes no store call and announces nothing.
    - The stamp is the breaker clock's `GetUtcNow()`, truncated to microseconds.
    - Changing the options object after construction has no effect on the breaker: with C changed from 50 s to 10 s afterwards, `orders` is still listed at t = 49.9 s.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Guard the view and the fallback fields with a private lock that is never held across I/O.
    - Sync `TripTopic` updates the view and records a pending write per topic, started through `Task.Run` when none is in flight.
    - `TrippedTopics` filters the view by `now - stamp < tripLength`, where the trip length is C + `ClockSkewLeeway`, computed once and used by every judgement that a trip has ended (view, `ReadAsync` cutoff, write-over-ended). With the default leeway of zero it equals C.
  - Covers: FR-3, FR-3.6, FR-3.7 (sync callbacks), FR-4.4 (sync `TrippedTopics` never touches the store), NFR-3 (callback never waits), NFR-6, NFR-8, AC-30 (part 1).
  - Depends on: T2.9
  - References: ADR 0089 *`DistributedOutboxCircuitBreaker`* member table.

- [ ] **TEST + IMPLEMENT: T2.11 - A burst of callbacks keeps at most one pending and one in-flight write per topic**
  - **USE COMMAND**: `/tdd:test-first when ten thousand callbacks trip a topic while its write is in flight should keep one pending write holding the latest stamp and write it afterwards`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_many_callbacks_trip_a_topic_during_an_in_flight_write_should_keep_one_pending_write.cs`
  - Test should verify:
    - 10,000 sync trips of `orders` while the store blocks leave at most one pending write (plus the one in flight), and the pending write holds the latest stamp.
    - When the in-flight write is released, its continuation starts the pending write, and the store ends with the latest stamp.
    - The same rule applies to trips from the async confirmation callback path.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Keep a per-topic pending stamp and in-flight flag. The continuation of a write starts that topic's pending write.
    - Leave the confirmation callbacks at `OutboxProducerMediator.cs:946` and `:1040` on the sync `TripTopic`.
  - Covers: FR-3.7 (synchronous callbacks, which paths use which rule), NFR-2, AC-30 (part 2). ADR step 8 (AC-30 burst slice).
  - Depends on: T2.10
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:946,:1040`. `tests/Paramore.Brighter.Core.Tests/Confirmation/When_concurrent_same_topic_confirmations_fail_should_not_lose_trips.cs`.

- [ ] **TEST + IMPLEMENT: T2.12 - A trip batch writes each distinct topic once, in one store operation**
  - **USE COMMAND**: `/tdd:test-first when a trip batch collects many failures should write each distinct topic once in one store operation`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_trip_batch_collects_many_failures_should_write_each_topic_once.cs`
  - Test should verify:
    - With a counting store double, 100 batch trips of `orders` give exactly 1 topic entry written, with the latest stamp.
    - Trips across 50 topics give exactly 50 entries in 1 store operation.
    - A second `WriteAsync` does nothing.
    - A batch with no trips makes no store call.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `BeginTripBatch` returns a batch whose `TripTopic` updates the view at once and keeps the latest stamp per topic.
    - `WriteAsync` (no token) issues one `store.WriteAsync` for the distinct topics, and none when the batch holds no trips.
  - Covers: FR-3.7 (dispatch calls, cost of coalescing), FR-3.6, NFR-2, AC-36 (write parts). OQ-7.
  - Depends on: T2.10
  - References: ADR 0089 *`IAmAnOutboxCircuitBreakerAsync` and `IAmATripBatch`* and *Why one batched write*.

- [ ] **CHARACTERISE: T2.13 - A batch trip shows in the view at once, and reaches the store only at the end of the call**
  - **USE COMMAND**: `/tdd:test-first when a topic is tripped mid batch should list it at once and write it to the store only when the batch is written`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_topic_is_tripped_mid_batch_should_list_it_at_once_and_write_it_at_the_end.cs`
  - Test should verify:
    - After `batch.TripTopic(orders)`, `TrippedTopics` lists `orders` and the store holds nothing.
    - After `WriteAsync`, the store holds `orders`.
  - 🔁 RED mutation: make the batch's `TripTopic` call `store.WriteAsync` at once. The test must fail on the "store holds nothing" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.12 makes the batch update the view at once and write only in `WriteAsync`. This test pins FR-3.7 (mid-call).
  - Covers: FR-3.1 (visible to the tripping instance first), FR-3.7 (mid-call), AC-40 (breaker half; the mediator half is T2.45).
  - Depends on: T2.12
  - References: ADR 0089 Phase 2 sequence diagram.

- [ ] **TEST + IMPLEMENT: T2.14 - Writes landing out of order keep the later stamp, announce a ReTrip, and the writer's view takes the stored stamp**
  - **USE COMMAND**: `/tdd:test-first when an earlier stamped write lands after a later one should keep the later stamp, announce a retrip and move the writer's view to the stored stamp`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_an_earlier_stamped_write_lands_after_a_later_one_should_keep_the_later_stamp.cs`
  - Test should verify:
    - C = 50 s, ordered-apply store double, A stamps `orders` at 10 s and B at 30 s, both through a trip batch (`BeginTripBatch`), and B's write is applied first. The sync `TripTopic` path is T2.16a's.
    - The stored stamp is 30 s, and A's view takes 30 s when its write completes.
    - `orders` is listed on both until t = 79.9 s and not at t = 80 s.
    - A's write is announced as a ReTrip and B's as a Trip.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In the batch `WriteAsync` completion, classify per ADR *How a write is announced* rows 3 and 5, and set the view stamp to `StoredTripTime`. Do not yet apply this to the sync `TripTopic` write (T2.16a does).
  - Covers: FR-3.3 (classification at write completion), FR-3.8, NFR-6, AC-42 (part 1).
  - Depends on: T2.12
  - References: ADR 0089 *How a write is announced*.

- [ ] **TEST + IMPLEMENT: T2.15 - A write whose stamp has already run out is not issued and is dropped silently**
  - **USE COMMAND**: `/tdd:test-first when a write's stamp is a trip length or more before the writer's clock should not issue it and drop the topic silently`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_write_stamp_has_already_run_out_should_not_write_and_drop_the_topic_silently.cs`
  - Test should verify:
    - A batch trip stamped t = 0, with the clock at t = 50 s when the batch's `WriteAsync` runs, leaves the store unchanged.
    - No Trip, ReTrip or Reset is produced, and the topic leaves the view.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Filter stale trips (row 1) before the store call.
  - Covers: FR-3.8 (stale write), AC-42 (part 2).
  - Depends on: T2.14
  - References: ADR 0089 *How a write is announced*, row 1.

- [ ] **TEST + IMPLEMENT: T2.16 - A write over an ended but unmarked row announces Reset then Trip, by the writer only**
  - **USE COMMAND**: `/tdd:test-first when a later write lands over an ended but unmarked row should have the writer announce one reset then one trip`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_later_write_lands_over_an_ended_unmarked_row_should_announce_reset_then_trip.cs`
  - Test should verify:
    - C = 50 s and a stored entry stamped t = 0 that is not marked ended. A batch write stamped t = 60 s makes the writer produce one Reset and then one Trip. Another breaker over the same store that makes no call produces neither; cross-instance Reset uniqueness is T2.20's.
    - A write that leaves the row ended (row 6) announces nothing and drops the topic from the view.
    - Row 4/5 pre-check: when the stored stamp in the answer has already run out, the writer drops the topic and announces nothing.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement rows 4 and 6 and the pre-check paragraph from *How a write is announced*, for the batch `WriteAsync`.
  - Covers: FR-3.8 (Reset then Trip), FR-4.4, FR-8.6 (distributed), AC-42 (part 3).
  - Depends on: T2.14
  - References: ADR 0089 *How a write is announced*, rows 4 and 6 and the paragraph after the table.

- [ ] **TEST + IMPLEMENT: T2.16a - A write started by a synchronous or callback trip is announced and reconciled like a batch write**
  - **USE COMMAND**: `/tdd:test-first when a synchronous trip's write completes should announce it by the stored prior stamp, take the stored stamp and drop a stamp that has run out`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_synchronous_trip_write_completes_should_announce_and_reconcile_like_a_batch_write.cs`
  - Test should verify:
    - A sync `TripTopic(orders)` on an untripped topic, once its write completes, produces exactly one Trip (row 3), and none before it completes.
    - A sync trip whose earlier stamp lands after a later stamp from another instance produces a ReTrip, and the writer's view takes the stored stamp (row 5), as T2.14 asserts for a batch.
    - A pending sync write whose stamp is a trip length or more before the clock when it is issued is not issued, announces nothing and leaves the view (row 1).
    - A sync write over an ended but unmarked row produces one Reset then one Trip from the writer (row 4), and one that leaves the row ended announces nothing (row 6).
    - A trip from the async confirmation callback (`OutboxProducerMediator.cs:1040`), driven through the mediator, produces exactly one Trip once its write completes (row 3). The other rows are driven through direct `TripTopic` calls only. The sync callback at `:946` shares the sync `TripTopic` path and is not driven separately.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Route the completion of the pending write that T2.11 starts through the same classification and reconciliation step as the batch `WriteAsync` completion (rows 1, 3, 4, 5 and 6, and adopting `StoredTripTime`), so there is one step, not two copies.
  - Covers: FR-3.3 (sync and callback trips), FR-3.7 (callbacks use the pending-write rule), FR-3.8, FR-8.6 (distributed).
  - Depends on: T2.16, T2.11
  - References: ADR 0089 *How a write is announced*. `src/Paramore.Brighter/OutboxProducerMediator.cs:946,:1040`.

- [ ] **TEST + IMPLEMENT: T2.17 - Distributed breaker spans carry the cooldown duration, and the counter attributes are unchanged**
  - **USE COMMAND**: `/tdd:test-first when the distributed breaker announces a transition should tag its span with the cooldown duration and keep the counter attributes unchanged`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_distributed_breaker_announces_a_trip_should_tag_the_span_with_the_cooldown_duration.cs`
  - Test should verify:
    - A Trip span from the distributed breaker has `circuit_breaker.cooldown_duration` = C in seconds and a `CooldownCount` of 0.
    - An in-memory breaker span has no such attribute.
    - The `paramore.brighter.circuit_breaker.trips` attribute set is unchanged.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add an optional `TimeSpan? CooldownDuration { get; init; }` (not positional) to `CircuitBreakerSpanInfo.cs`.
    - Add the `circuit_breaker.cooldown_duration` constant to `BrighterSemanticConventions.cs`.
    - Set the attribute in `BrighterTracer.CreateCircuitBreakerSpan` only when the property is present.
  - Covers: FR-8.6 (counter attribute set unchanged), ADR 0058 extension (span attribute).
  - Depends on: T2.14
  - References: `src/Paramore.Brighter/Observability/CircuitBreakerSpanInfo.cs`, `BrighterSemanticConventions.cs`, `BrighterTracer.cs`. `docs/adr/0058-outbox-circuit-breaker-trip-logging.md`.

- [ ] **CHARACTERISE: T2.18 - Concurrent first trips on two instances list the topic once, with one Trip and one ReTrip**
  - **USE COMMAND**: `/tdd:test-first when two instances trip the same untripped topic at the same moment should list it once with exactly one trip and one retrip`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_two_instances_trip_a_topic_at_the_same_moment_should_announce_one_trip_and_one_retrip.cs`
  - Test should verify:
    - Breakers A and B over one `InMemoryTrippedTopicStore` trip `orders` from parallel tasks through trip batches, repeated 1,000 times. The sync path is covered by T2.16a's classification step.
    - On every repetition `orders` is listed once, the deployment-wide telemetry has exactly one Trip and one ReTrip, and the topic ends C after the later stamp.
  - 🔁 RED mutation: classify Trip or ReTrip from whether the topic was already in the writer's own view, instead of from the store's `PriorTripTime`. The test must fail on the "exactly one Trip and one ReTrip" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.14 and T2.16a classify from the store's prior stamp, and the store's upsert is atomic. This test guards FR-3.3 under 1,000 races.
  - Covers: FR-3.3, NFR-5 (distributed), AC-11.
  - Depends on: T2.14
  - References: ADR 0089 *Testing strategy*.

- [ ] **TEST + IMPLEMENT: T2.19 - A re-trip on another instance restarts the cooldown everywhere**
  - **USE COMMAND**: `/tdd:test-first when another instance re-trips a topic should restart its cooldown on every instance`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_another_instance_re_trips_a_topic_should_restart_the_cooldown_everywhere.cs`
  - Test should verify:
    - C = 50 s on a shared `FakeTimeProvider`. A trips at t = 0 and B re-trips at t = 30 s.
    - After reads, `orders` is listed on both at t = 79.9 s and on neither at t = 80 s.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Make `ReadTrippedTopicsAsync` merge store `Live` rows into the view.
  - Covers: FR-3.2, AC-10.
  - Depends on: T2.14
  - References: ADR 0089 Phase 2 invariants.

- [ ] **TEST + IMPLEMENT: T2.20 - A distributed trip ends by elapsed time whichever instance reads, with one Reset**
  - **USE COMMAND**: `/tdd:test-first when instances alternate holding the lock should end a distributed trip at exactly the cooldown duration with one reset`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_instances_alternate_the_lock_should_end_the_trip_at_the_cooldown_duration_with_one_reset.cs`
  - Test should verify:
    - Default options (C = 50 s). A request-path trip of `orders` is recorded on A at t = 0, before any sweep.
    - Each simulated sweep, alternating between A and B, calls `ReadTrippedTopicsAsync`. Every read before t = 50 s excludes `orders`, and the first at or after t = 50 s includes it.
    - Exactly one Reset is produced deployment-wide. An instance whose read finds the row already ended announces none.
    - `CoolDown` calls do not advance or end the trip.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Announce a Reset for each `Ended` row this read marked (*How a read ends trips*), passing the same trip length (C + leeway) to `ReadAsync`.
    - `CoolDown` does nothing to a distributed trip. T2.33 adds the Reset of an unshared trip.
  - Covers: FR-2.4, FR-4, FR-4.3, FR-4.4, AC-13 (breaker level; the mediator's pass-through is spot-checked in T2.39d).
  - Depends on: T2.19, T2.6
  - References: ADR 0089 *How a read ends trips*.

- [ ] **CHARACTERISE: T2.21 - The number of sweeps and instances does not change the cooldown**
  - **USE COMMAND**: `/tdd:test-first when three instances with offset ticks sweep should exclude the topic from every sweep before the cooldown duration`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_three_instances_with_offset_ticks_sweep_should_exclude_the_topic_until_the_cooldown_duration.cs`
  - Test should verify:
    - C = 50 s, 3 instances, `TimerInterval` 5 s, first ticks at 0.1 s, 1.8 s and 3.5 s.
    - The 12 lock-obtaining reads in [0, 20 s) all exclude `orders`, and the first read at or after 50 s includes it.
  - 🔁 RED mutation: end a trip after three reads of the same view, whatever the clock says. The test must fail on the "all 12 reads exclude `orders`" assertion (each view makes four reads in [0, 20 s), so the fourth read of the first instance, at about 15.1 s, lists nothing). Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.20 ends a trip by elapsed time alone. This test pins that sweep and instance counts play no part.
  - Covers: FR-4, AC-14.
  - Depends on: T2.20
  - References: requirements FR-4 example.

- [ ] **CHARACTERISE: T2.22 - An instance that had lock_unavailable neither holds back nor keeps a trip**
  - **USE COMMAND**: `/tdd:test-first when an instance obtains the lock after a long lock unavailable spell should include the topic whose trip ended`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_an_instance_obtains_the_lock_after_lock_unavailable_should_include_the_ended_topic.cs`
  - Test should verify:
    - A trips `orders` at t = 0. B reads while A does nothing until t = 100 s.
    - At t = 100 s, before A reads, A's `TrippedTopics` does not list `orders`.
    - A's next read then includes `orders`.
  - 🔁 RED mutation: skip the elapsed-time check for entries in the view, listing every view entry until a read replaces it. The test must fail on the "A's `TrippedTopics` does not list `orders` at t = 100 s" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.10 and T2.20 judge view entries by elapsed time alone. This test pins FR-4.1.
  - Covers: FR-4.1, AC-15.
  - Depends on: T2.20
  - References: ADR 0089 Phase 2 invariants.

- [ ] **CHARACTERISE: T2.23 - Trips survive a restart, are listed after the first read, and still end by C**
  - **USE COMMAND**: `/tdd:test-first when every breaker is recreated over a durable store should list a live trip after the first read and drop an ended one`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_breakers_are_recreated_over_the_store_should_list_live_trips_after_the_first_read.cs`
  - Test should verify:
    - C = 50 s. `orders` is tripped at t = 0 and the breakers are recreated at t = 10 s.
    - Before any read the new view holds no store trips. After `ReadTrippedTopicsAsync` it lists `orders`.
    - Recreated at t = 60 s, the breaker does not list it after a read.
    - With no sweeper running, the trip still ends at C.
  - 🔁 RED mutation: read the store in the breaker's constructor, so the view is seeded before any `ReadTrippedTopicsAsync`. The test must fail on the "before any read the new view holds no store trips" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.9/T2.10 start the view empty of store trips, and T2.19/T2.20 fill it on a read. This test pins FR-3.4 and AC-12.
  - Covers: FR-3.4, FR-4.2, AC-12. ADR *Deliberate readings*: "A new breaker reads before it lists".
  - Depends on: T2.20
  - References: ADR 0089 *Testing strategy* (AC-12).

- [ ] **CHARACTERISE: T2.24 - The clock skew leeway bounds the trip across skewed clocks**
  - **USE COMMAND**: `/tdd:test-first when instance clocks differ by a skew covered by the leeway should list the topic until the cooldown and drop it by the cooldown plus twice the skew`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_instance_clocks_are_skewed_within_the_leeway_should_bound_the_trip.cs`
  - Test should verify:
    - Two `FakeTimeProvider`s differ by a skew, and each breaker has `ClockSkewLeeway` = skew. In a second run the leeway is the skew and the actual clock difference is skew / 2.
    - With C = 50 s, by the tripping instance's clock, `orders` is listed on every instance 1 ms before C and on none 1 ms after C + 2 × skew.
    - Every judgement that a trip has ended (view, store read cutoff, write-over-ended) uses C + leeway.
  - 🔁 RED mutation: judge view entries by C instead of C + leeway. The test must fail on the "listed on every instance 1 ms before C" assertion, because the instance whose clock runs ahead ends the trip early. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing, if T2.10 and T2.20 computed the trip length once as C + leeway as they say. This is the first test with a non-zero leeway, so it may be red on arrival; if so, the fix is to compute the trip length that way, and the task continues as `TEST + IMPLEMENT`. It pins NFR-6 and AC-32.
  - Covers: FR-5.6, NFR-6, AC-32. OQ-4.
  - Depends on: T2.20
  - References: ADR 0089 *Why a user-set leeway for clock skew*.

### Phase 2d - Store failure, timeouts and fallback

- [ ] **TEST + IMPLEMENT: T2.25 - Every store operation is bounded by the store timeout and counts as a failure on timeout**
  - **USE COMMAND**: `/tdd:test-first when the store never completes should give up after the store timeout and use the fallback view`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_store_never_completes_should_give_up_after_the_store_timeout.cs`
  - Test should verify:
    - `StoreTimeout` = 2 s and a store double that never completes and ignores cancellation.
    - `ReadTrippedTopicsAsync` returns the fallback view after no more than 2 s on the breaker's `TimeProvider`.
    - A 2-topic batch `WriteAsync` returns after no more than 2 s, and the topics become unshared (observed through `StoreStatus.IsUsingFallback` staying true).
    - `StoreStatus.LastFailure` == `timeout`.
    - A token cancelled by the caller makes `ReadTrippedTopicsAsync` throw `OperationCanceledException` and changes no state.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Pass a token from `TimeProvider.CreateCancellationTokenSource(StoreTimeout)` and stop waiting with `WaitAsync(StoreTimeout, timeProvider)` (`Microsoft.Bcl.TimeProvider`, already referenced).
    - Attach a continuation that observes the abandoned task's exception.
    - Record a timeout in `StoreStatus` (`LastFailure` = `timeout`, `IsUsingFallback` = true) and mark the timed-out batch's topics unshared. Naming a thrown exception by its short type name is the catch's job: the read-path catch is T2.27's and the write-path catch is T2.28's.
  - Covers: FR-5.4, NFR-3, NFR-9, AC-19 (breaker part; the PostAsync part is T2.44). OQ-6, OQ-14 (the write ignores the caller's token).
  - Depends on: T2.12, T2.19
  - References: ADR 0089 *`IAmATrippedTopicStore`*, paragraph after the table.

- [ ] **TEST + IMPLEMENT: T2.26 - Abandoned store calls are guarded, so a hung store cannot pile up calls or disable refresh**
  - **USE COMMAND**: `/tdd:test-first when a store call is abandoned after the timeout should hold its in flight guard until it completes or four store timeouts pass then release it with a warning`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_store_call_is_abandoned_should_hold_its_guard_until_four_store_timeouts.cs`
  - Test should verify:
    - While an abandoned sweep read is in flight, another sweep read starts no store call (dropped, not queued), uses the current view, changes no state and logs no Warning.
    - At 4 × `StoreTimeout` the guard is released with a Warning and the next request reaches the store.
    - The same rule holds for each topic's callback write. The refresh guard is T2.29's.
    - No `TaskScheduler.UnobservedTaskException` is raised.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add an in-flight guard helper and apply it to the sweep read and to each topic's callback write. It clears in a `finally` when the task completes or the 4× guard timer fires.
  - Covers: NFR-2, NFR-3, NFR-9. ADR risk "A store that ignores cancellation". OQ-14 (no overlapping calls). ADR *Deliberate readings*: "In flight".
  - Depends on: T2.25
  - References: ADR 0089 *How the remaining small points are settled*: "No overlapping calls".

- [ ] **TEST + IMPLEMENT: T2.27 - A failed read falls back to the instance's own view, with a Warning and no exception**
  - **USE COMMAND**: `/tdd:test-first when the store throws on read should return the fallback view, log a warning and not throw`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_store_throws_on_read_should_return_the_fallback_view_and_log_a_warning.cs`
  - Test should verify:
    - The fallback view holds `orders`, tripped 20 s ago with C = 50 s, and the store throws on read.
    - `ReadTrippedTopicsAsync` returns `[orders]` only, so topics not in the view are not excluded.
    - A Warning names the failure, and `StoreStatus.IsUsingFallback` is true with `LastFailure` = the short type name.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Catch every store exception, set the read-failure part of the fallback state, and return the view.
  - Covers: FR-5, FR-5.1, FR-6.1, NFR-9, AC-17 (breaker half; the sweep half is T2.40).
  - Depends on: T2.25
  - References: ADR 0089 *Store failure and the health signals*, ladder row 4.

- [ ] **TEST + IMPLEMENT: T2.28 - A failed write leaves the trip unshared and never aborts the batch**
  - **USE COMMAND**: `/tdd:test-first when the store throws on write during a batch should keep dispatching the batch and keep the trip as unshared`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_store_throws_on_write_during_a_batch_should_keep_dispatching_and_keep_the_trip_unshared.cs`
  - Test should verify:
    - Store throws on write. A mediator batch of `orders`, `invoices`, `invoices` where the `orders` send fails. Until T2.39 the mediator trips through the sync `TripTopic`, so this is the sync path.
    - A trip batch whose `WriteAsync` fails completes without throwing, logs a Warning and leaves its topics unshared.
    - Both `invoices` messages are sent and marked dispatched, the `orders` message stays outstanding, and no exception escapes.
    - A Warning names the failure. This instance's next read excludes `orders` until C.
    - The failed write announces exactly one Trip from this instance's view (a ReTrip when the view held a live `orders` entry before this trip), through `TripTopic` telemetry. Assert it in both scenarios: the mediator's sync path and a direct trip batch.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - On a write failure on any path (sync trip write, batch `WriteAsync`), announce Trip, or ReTrip when the view held a live `orders` entry before this trip, from the view (row 2), log a Warning, and keep the topics unshared. Catch every store exception on the write path, set `LastFailure` to its short type name and mark the failed topics unshared (T2.25 marks only timed-out topics).
  - Covers: FR-3.9, FR-5.2, FR-5.7 (row 2), NFR-9, AC-18.
  - Depends on: T2.25, T2.16a
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/SweepableOutbox.cs`. ADR *How a write is announced*, row 2.

- [ ] **CHARACTERISE: T2.28a - A store disposed under a write at host shutdown logs a Warning and throws nothing**
  - **USE COMMAND**: `/tdd:test-first when the store is disposed under an in flight write should log a warning, throw nothing and keep the trip unshared`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_store_is_disposed_under_a_write_should_log_a_warning_and_throw_nothing.cs`
  - Test should verify:
    - A store double that throws `ObjectDisposedException` mid-write: the batch `WriteAsync` and a callback write log a Warning, throw nothing, and leave the topic unshared.
  - 🔁 RED mutation: catch every store exception in the write path except `ObjectDisposedException`. The test must fail on the "throw nothing" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.28 catches every store exception. This test pins ADR 0089's host-shutdown rule (work in flight is abandoned and drops its pending stamps).
  - Covers: FR-5.2, NFR-9. ADR 0089 *`DistributedOutboxCircuitBreaker`* (host shutdown).
  - Depends on: T2.28
  - References: ADR 0089 *`DistributedOutboxCircuitBreaker`*, paragraph after the member table.

- [ ] **TEST + IMPLEMENT: T2.29 - A failed refresh keeps the fallback view, and no trip outlives C**
  - **USE COMMAND**: `/tdd:test-first when every refresh fails should keep the fallback view and still drop the trip at the cooldown duration`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_every_refresh_fails_should_keep_the_fallback_view_and_drop_the_trip_at_the_cooldown.cs`
  - Test should verify:
    - C = 50 s and a store that fails every operation from t = 0. `orders` is tripped locally at t = 0, and `StartRefresh` is called every 5 s.
    - `orders` is listed until t = 49.9 s and not at t = 50 s.
    - Each failed refresh logs one Warning and throws nothing.
    - `StartRefresh` never blocks the caller (it runs through `Task.Run`).
    - While an abandoned refresh is in flight, another `StartRefresh` starts no store call. At 4 × `StoreTimeout` the guard is released with a Warning and the next request reaches the store.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement `IAmASharedOutboxCircuitBreaker.StartRefresh` (thread pool, catch all, `finally`) behind the in-flight guard helper from T2.26.
  - Covers: FR-5.3, FR-5.6, AC-20. OQ-12 (breaker side).
  - Depends on: T2.26
  - References: ADR 0089 *Refresh on every running sweeper*.

- [ ] **TEST + IMPLEMENT: T2.30 - A read failure sets the fallback state, and recovery merges per topic**
  - **USE COMMAND**: `/tdd:test-first when reads fail and then recover should report fallback until a read succeeds and merge the store's trips with the local ones`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_reads_and_writes_fail_and_recover_should_follow_the_fallback_rules.cs`
  - Test should verify:
    - Reads fail and then recover, with no unshared trip: the next successful read lists the store's trips plus local unshared, held and pending trips that have not ended, merged with the later stamp winning, and `IsUsingFallback` becomes false.
    - `LastFailure` persists until fallback ends, and is cleared when it ends.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - On a successful read, clear the read-failure part and (once no unshared trip remains) `LastFailure`, and merge the store's trips with this instance's unshared, held and pending trips per *The view after a successful read*. `StoreStatus` and the failure recording already exist (T2.25, T2.27).
  - Covers: FR-5.5 (read-failure part), FR-8.4 (state), AC-21 (first clause).
  - Depends on: T2.27, T2.28
  - References: ADR 0089 *Store failure and the health signals* (the paragraph after the ladder and the bullets).

- [ ] **TEST + IMPLEMENT: T2.30a - An unshared trip keeps the fallback state through successful reads until a write of its topic succeeds or it ends**
  - **USE COMMAND**: `/tdd:test-first when reads succeed and writes fail should stay in fallback until a write of the topic succeeds or the trip ends`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_reads_succeed_and_writes_fail_should_stay_in_fallback_until_a_write_succeeds.cs`
  - Test should verify:
    - Reads succeed and writes fail: after tripping `orders`, `IsUsingFallback` is true and stays true through successful reads.
    - A later successful write of `orders` ends it, and so does the trip's end.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Make `IsUsingFallback` true while the most recent read failed or an unshared trip exists. A successful write of a topic, or the trip's end, ends that unshared trip. A successful read does not.
  - Covers: FR-5.5, FR-8.4 (state), AC-21 (second clause).
  - Depends on: T2.30, T2.28
  - References: ADR 0089 *Store failure and the health signals*, the paragraph after the ladder.

- [ ] **CHARACTERISE: T2.30b - Writes that succeed do not end a read failure**
  - **USE COMMAND**: `/tdd:test-first when writes succeed and reads fail should stay in fallback until a read succeeds`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_writes_succeed_and_reads_fail_should_stay_in_fallback_until_a_read_succeeds.cs`
  - Test should verify:
    - Writes succeed while reads fail: `IsUsingFallback` is true until a read succeeds, and a successful write changes nothing.
  - 🔁 RED mutation: clear the read-failure flag when a write succeeds. The test must fail on the "`IsUsingFallback` is true" assertion after the successful write. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.30 sets the read-failure part from reads only, and T2.30a clears unshared status from writes only.
  - Covers: FR-5.5, FR-8.4 (state), AC-21 (third clause).
  - Depends on: T2.30a
  - References: ADR 0089 *Store failure and the health signals*.

- [ ] **TEST + IMPLEMENT: T2.31 - A slow, stale read never undoes a newer read or drops a trip just written**
  - **USE COMMAND**: `/tdd:test-first when a slow refresh completes after a newer sweep read should discard its live rows but still announce its ended rows`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_slow_refresh_completes_after_a_newer_read_should_discard_its_live_rows.cs`
  - Test should verify:
    - A refresh that started before a sweep read, and completes after it, does not change the view's live entries. Its `Ended` rows are still announced once each.
    - A topic this instance wrote stays in the view until a read that started after the write completed has been applied.
    - A stale outcome changes neither the fallback state nor `LastFailure`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Give each read a sequence number, track recently written trips, and remove an ended topic only when the view's stamp is no later than the ended stamp.
  - Covers: FR-5.5, NFR-4, NFR-5. ADR risk "A slow refresh overwrites a newer sweep read".
  - Depends on: T2.30, T2.29
  - References: ADR 0089 bullets *Stale reads*, *Ended rows are always announced*, *Recently written trips*.

- [ ] **TEST + IMPLEMENT: T2.32 - Unshared trips are written after recovery**
  - **USE COMMAND**: `/tdd:test-first when a read succeeds while unshared trips remain should write them in the background without the sweep awaiting it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_read_succeeds_with_unshared_trips_should_write_them_in_the_background.cs`
  - Test should verify:
    - After a write failure and then a successful read, one recovery batch write of the unshared, unended trips is made in the background. `ReadTrippedTopicsAsync` returns without awaiting it, and another instance then reads the trip.
    - A request-only instance (no refresh) whose write of `orders` failed adds `orders` to the topics of its next trip write, driven through the sync `TripTopic` path of another topic, and the store then holds `orders`.
    - Only one recovery write runs at a time.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Submit the recovery write after a successful read, outside the refresh guard, with its own in-flight guard. Apply the stale-write rule.
    - Add the unshared, unended trips to the topics of the next trip write this instance issues, so a request-only instance (which never reads) retries them with that write. On the batch path they join the batch's single store call. On the sync pending-write path (before T2.39 and T2.43 a request-only instance has only this path) that write then carries several topics in one store call, while the per-topic pending/in-flight rule of T2.11 still applies to the topics the callback tripped.
  - Covers: FR-5.5 (OQ-5), FR-3.9, OQ-5.
  - Depends on: T2.30a, T2.16a
  - References: ADR 0089 *How the remaining small points are settled*: "Recovery writes".

- [ ] **TEST + IMPLEMENT: T2.33 - Telemetry under failure: at most one Reset per instance per unshared trip, and no second announcement**
  - **USE COMMAND**: `/tdd:test-first when two instances hold the same unshared trip and it ends should each announce one reset from the first announcing call`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_two_instances_hold_an_unshared_trip_and_it_ends_should_each_announce_one_reset.cs`
  - Test should verify:
    - A and B each tripped `orders` while their writes failed. After C, each produces exactly one Reset, from the first `CoolDown`, `TripTopic`, sweep read or refresh. The deployment produces at most one per instance plus one for the store's entry.
    - Gauge, check and sync `TrippedTopics` reads in between produce none.
    - Reads succeed throughout, so `IsUsingFallback` is already false after C and before the announcing call, and the first `CoolDown` still produces exactly one Reset.
    - A trip announced from the fallback view is not announced again when a later write shares it, but a row-4 Reset still is.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Announce ended unshared trips at the head of `CoolDown`, sync `TripTopic`, batch `TripTopic`, `ReadTrippedTopicsAsync` and `StartRefresh`. Track the announced state per trip (row 7).
  - Covers: FR-5.7, FR-8.6, AC-22. OQ-14 (no second announcement).
  - Depends on: T2.30a, T2.29
  - References: ADR 0089 *How a write is announced*, row 7. *`DistributedOutboxCircuitBreaker`* member table.

- [ ] **CHARACTERISE: T2.34 - A breaker started during an outage leaves out only its own trips, then uses the store's**
  - **USE COMMAND**: `/tdd:test-first when a breaker starts while the store cannot be read should exclude only its own trips and use the store's trips once a read succeeds`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_breaker_starts_during_a_store_outage_should_exclude_only_its_own_trips.cs`
  - Test should verify:
    - The store throws on every read and the breaker has never read successfully. `ReadTrippedTopicsAsync` returns nothing, a Warning is logged, and `IsUsingFallback` is true.
    - When the store then answers with `orders`, the next read excludes `orders`.
  - 🔁 RED mutation: clear the read-failure flag when the very first read throws (the never-read case). The test must fail on "`IsUsingFallback` is true". Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.27 and T2.30 deliver the first-read and fallback state (ladder rows 7 and 8). This test pins FR-5.8 at breaker level.
  - Covers: FR-5.8, AC-23 (breaker part; the sweep and check parts are T2.40 and T2.55).
  - Depends on: T2.30
  - References: ADR 0089 ladder rows 7-8.

- [ ] **CHARACTERISE: T2.35 - TrippedTopics and StoreStatus never wait on a blocked store**
  - **USE COMMAND**: `/tdd:test-first when the store blocks indefinitely should return tripped topics and store status within a second from the fallback view`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_store_blocks_indefinitely_should_return_tripped_topics_and_store_status_at_once.cs`
  - Test should verify:
    - With a blocking store and a read in flight, `TrippedTopics` returns `[orders]` and `StoreStatus` returns, each within 1 s.
  - 🔁 RED mutation: make `TrippedTopics` wait on the in-flight read (`GetAwaiter().GetResult()`). The test must fail on the "within 1 s" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.10 and T2.30 copy state under the short lock only. This test pins NFR-4 at breaker level.
  - Covers: NFR-4, AC-29 (breaker part; the gauge and check parts are T2.56).
  - Depends on: T2.30
  - References: ADR 0089 Phase 2 invariant: "No read waits on the store from a health signal".

- [ ] **CHARACTERISE: T2.36 - A trip that races the read ending the topic is never lost (distributed)**
  - **USE COMMAND**: `/tdd:test-first when a distributed trip races the read that ends the topic should leave the topic tripped with its cooldown restarted at the trip`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_distributed_trip_races_the_ending_read_should_restart_the_cooldown_at_the_trip.cs`
  - Test should verify:
    - Batch `TripTopic` with `WriteAsync`, `ReadTrippedTopicsAsync`, sync `TrippedTopics` and `CoolDown` run concurrently from many tasks, repeated 1,000 times, as the topic reaches its trip length.
    - No exception is thrown, the topic is still tripped after the race with its C clock restarted at the trip, and no reader sees a half-updated state.
  - 🔁 RED mutation: remove an ended topic from the view by name, ignoring the view's stamp. The test must fail on the "still tripped after the race" assertion within its 1,000 repetitions. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.31 removes an ended topic only when the view's stamp is no later than the ended stamp. This test pins NFR-5 for the distributed breaker.
  - Covers: NFR-5, NFR-8, AC-37 (distributed half).
  - Depends on: T2.31
  - References: ADR 0089 *How a read ends trips* (re-trip race).

- [ ] **CHARACTERISE: T2.38 - A failed write on a request-only instance or a callback is logged, and the message stays**
  - **USE COMMAND**: `/tdd:test-first when a request only instance cannot write a trip should log a warning, throw nothing and leave the message outstanding`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_request_only_instance_cannot_write_a_trip_should_log_a_warning_and_keep_the_message.cs`
  - Test should verify:
    - No sweeper, and the store throws on write. A `PostAsync` that fails to publish `orders` throws nothing, a Warning names the failure, and the message is still outstanding.
    - The same holds for a pending write from a confirmation callback.
    - The `PostAsync` scenario runs through the sync `TripTopic` path until T2.43 moves `ClearOutboxAsync` to a batch. T2.44 re-runs the request-path write through the batch and pins that route; this test must stay green after T2.43.
  - 🔁 RED mutation: swallow the failed callback write's exception without logging it. The test must fail on the "a Warning names the failure" assertion for the callback scenario. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.28 logs a Warning on every write failure path, and T2.11 runs the callback write through the same path. This test pins AC-44 for a request-only instance and a callback.
  - Covers: FR-3.9, NFR-9, AC-44.
  - Depends on: T2.28, T2.11
  - References: `tests/Paramore.Brighter.Core.Tests/Confirmation/When_a_confirmation_fails_should_trip_topic_on_wire_topic.cs`.

### Phase 2e - The mediator uses the async role

- [ ] **TEST + IMPLEMENT: T2.39 - A sweep reads tripped topics asynchronously and writes one batch before it returns**
  - **USE COMMAND**: `/tdd:test-first when a sweep runs with a breaker that implements the async role should read tripped topics asynchronously and write one trip batch before returning`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_sweep_runs_with_an_async_breaker_should_read_asynchronously_and_write_one_batch.cs`
  - Test should verify:
    - Breakers A and B, each with its own mediator, over one store. A's sweep fails to publish `orders`, and when it returns the store holds `orders`.
    - B's sweep passes `orders` as tripped to `OutstandingMessagesAsync` and returns `invoices`.
    - Trips go through batch `TripTopic`, and only `WriteAsync` is awaited, not each trip.
    - `CoolDown()` is still called once per sweep.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `BackgroundDispatchUsingAsync` (`:740-790`), keep `CoolDown()` at `:749`.
    - Replace the `TrippedTopics` argument at `:763` with `await ReadTrippedTopicsAsync` when the breaker implements `IAmAnOutboxCircuitBreakerAsync`.
    - Create a batch around the dispatch and pass it as `trips` at both call sites, `:776` (bulk) and `:780` (non-bulk). Call `WriteAsync` first in the `finally` at `:791`.
  - Covers: FR-3.1, FR-3.6, FR-3.7, AC-8, AC-30 (part 3), FR-3.7 (cost of coalescing). ADR step 10. AC-43 is T2.39c; the pass-through spot check is T2.39d.
  - Depends on: T2.1, T2.12, T2.20
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:740-791`.

- [ ] **CHARACTERISE: T2.39c - A trip lost with its process is retried by the next sweep**
  - **USE COMMAND**: `/tdd:test-first when a batch is discarded without being written should leave the store without the topic and let the next sweep trip it again`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_trip_batch_is_discarded_without_a_write_should_let_the_next_sweep_trip_it_again.cs`
  - Test should verify:
    - Breakers and mediators A and B over one store. A's batch from `BeginTripBatch` trips `orders` and is dropped without `WriteAsync`, as when a process dies, and the store is left without `orders`.
    - B's next sweep, with the broker still down, attempts `orders`, fails, trips it through its batch, and the store holds it when the sweep returns (AC-43).
  - 🔁 RED mutation: write each trip to the store as soon as `TripTopic` is called on the batch instead of at `WriteAsync`. The test must fail on "the store is without `orders`" after the discarded batch. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.39 writes only at `WriteAsync`. This test pins AC-43.
  - Covers: AC-43.
  - Depends on: T2.39
  - References: ADR 0089 step 10.

- [ ] **CHARACTERISE: T2.39d - After the cooldown duration a sweep through the mediator includes the topic again**
  - **USE COMMAND**: `/tdd:test-first when the cooldown duration has passed a sweep through the mediator should include the tripped topic again`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_cooldown_duration_has_passed_a_sweep_should_include_the_topic_again.cs`
  - Test should verify:
    - Breakers and mediators A and B over one store. A trips `orders` at t = 0 and writes it, and B's sweep before C excludes `orders`.
    - After t = C, a later sweep through B includes `orders`. This is a spot check of the mediator's pass-through. AC-12 to AC-15 are verified at breaker level (T2.20-T2.23), because the mediator hands `ReadTrippedTopicsAsync` straight to `OutstandingMessagesAsync`.
  - 🔁 RED mutation: in the mediator, pass the breaker's synchronous `TrippedTopics` to `OutstandingMessagesAsync` instead of `ReadTrippedTopicsAsync`. The test must fail on the "B's sweep before C excludes `orders`" assertion, because B's own view is empty. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.39 hands `ReadTrippedTopicsAsync` straight to `OutstandingMessagesAsync`.
  - Covers: AC-12 to AC-15 (mediator pass-through).
  - Depends on: T2.39
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:763`.

- [ ] **CHARACTERISE: T2.40 - A sweep whose store read fails uses the fallback view and completes**
  - **USE COMMAND**: `/tdd:test-first when the trip store cannot be read during a sweep should sweep with the fallback view and end completed`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_trip_store_cannot_be_read_during_a_sweep_should_use_the_fallback_view_and_complete.cs`
  - Test should verify:
    - A `TimedOutboxSweeper` sweep with a fallback view holding `orders` and a store double that throws on read excludes `orders`, dispatches `invoices`, records `completed` and logs a Warning.
    - A breaker that has never read successfully leaves out no topic.
    - When the store then answers with `orders`, the next sweep excludes it.
  - 🔁 RED mutation: let a store exception from the sweep's read escape `ReadTrippedTopicsAsync`. The test must fail on the "records `completed`" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.27 and T2.39 deliver this. This test proves it through a sweeper.
  - Covers: FR-5.1, FR-5.8, FR-6.1, NFR-9, AC-17, AC-23 (sweep parts).
  - Depends on: T2.39, T2.34
  - References: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs`. `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/SweepableOutbox.cs`.

- [ ] **CHARACTERISE: T2.39a - A bulk sweep trips through the batch and writes once**
  - **USE COMMAND**: `/tdd:test-first when a bulk sweep runs with a breaker that implements the async role should trip through the batch and make one store write`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_bulk_sweep_runs_with_an_async_breaker_should_trip_through_the_batch_and_write_once.cs`
  - Test should verify:
    - A sweep with `UseBulk = true` over 100 `orders` messages that all fail makes exactly 1 store write for `orders`, completed before the sweep returns.
    - The sync `TripTopic` is not called from the bulk path (`:1240`).
  - 🔁 RED mutation: pass `null` as the batch at the bulk call site (`:776`). The test must fail on the "sync `TripTopic` is not called from the bulk path" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.39 passes the batch at both call sites (`:776` and `:780`). This test guards the bulk path.
  - Covers: FR-3.7 (bulk dispatch call), NFR-2, AC-36 (write parts).
  - Depends on: T2.39
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:776,:1152,:1240`.

- [ ] **CHARACTERISE: T2.41 - The batch is written before the lock is released, and when dispatch throws**
  - **USE COMMAND**: `/tdd:test-first when a sweep trips a topic should write the trip before the sweeper releases the lock even when dispatch throws`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_trips_a_topic_should_write_it_before_the_lock_is_released.cs`
  - Test should verify:
    - The store holds `orders` before the lock double's `ReleaseLockAsync` is called. The store double completes its write asynchronously (a gated `TaskCompletionSource` released after a yield), so an unawaited write would still be pending.
    - With a working store, when `DispatchAsync` throws after tripping `orders`, the store holds `orders` and the exception that propagates is the dispatch's.
    - With a store double that throws on write, when `DispatchAsync` throws after tripping `orders`, the write is attempted and the exception that propagates is still the dispatch's, not the store's. The breaker swallows the store failure (T2.28); a third-party batch that throws is T2.42.
  - 🔁 RED mutation: move `WriteAsync` out of the `finally` to the end of the `try` block, and (in a second run) discard its task instead of awaiting it. The test must fail on the "the store holds `orders` when dispatch throws" assertion in the first run and on the "holds `orders` before `ReleaseLockAsync`" assertion in the second. Revert both.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.39 calls `WriteAsync` first in the `finally`. This test pins FR-3.7 and AC-39.
  - Covers: FR-3.7 (finally, before the lock release), NFR-9, AC-39.
  - Depends on: T2.39
  - References: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs` (lock released in `finally`). `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/CountingDistributedLock.cs`.

- [ ] **TEST + IMPLEMENT: T2.42 - A third-party async breaker whose batch throws is contained**
  - **USE COMMAND**: `/tdd:test-first when a third party async breaker throws from begin trip batch or write should log a warning, keep the dispatch exception and release the semaphore`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_third_party_trip_batch_throws_should_contain_it_and_release_the_semaphore.cs`
  - Test should verify:
    - When `BeginTripBatch` throws, the call trips through sync `TripTopic` and a Warning is logged.
    - When `WriteAsync` throws, a Warning is logged, it does not replace a dispatch exception, and the next `ClearOutstandingFromOutboxAsync` is not blocked by the semaphore.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Wrap `BeginTripBatch` and `WriteAsync` in try/catch with a Warning, and release the semaphore in a nested `finally`.
  - Covers: NFR-9. ADR step 10 (containment).
  - Depends on: T2.41
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:791`.

- [ ] **TEST + IMPLEMENT: T2.43 - One write for a whole ClearOutbox call**
  - **USE COMMAND**: `/tdd:test-first when clear outbox async dispatches many failing messages for one topic should make one store write before returning`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_clear_outbox_dispatches_many_failing_messages_should_make_one_store_write.cs`
  - Test should verify:
    - A counting store and 100 outstanding `orders` messages that all fail. `ClearOutboxAsync` makes exactly 1 write for `orders`, completed before the call returns.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `ClearOutboxAsync` (`:447-498`), create one batch around the whole loop and write it in the outer `finally`.
  - Covers: FR-3.7, NFR-2, AC-41.
  - Depends on: T2.39
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:447-498`. `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Clear/`.

- [ ] **TEST + IMPLEMENT: T2.43a - A third-party async breaker whose batch throws cannot escape a ClearOutbox or Post call**
  - **USE COMMAND**: `/tdd:test-first when a third party async breaker throws from begin trip batch or write during clear outbox should log a warning and throw nothing out of post async`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_a_third_party_trip_batch_throws_during_clear_outbox_should_throw_nothing_out_of_post_async.cs`
  - Test should verify:
    - A third-party async breaker whose `BeginTripBatch` throws: `PostAsync` and `ClearOutboxAsync` log a Warning, trip through sync `TripTopic` and throw nothing.
    - One whose `WriteAsync` throws: the call logs a Warning and throws nothing. When `ClearOutboxAsync` is called directly and a producer double throws a non-send exception during dispatch, that dispatch exception propagates and is not replaced by the batch's.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Wrap the `BeginTripBatch` and `WriteAsync` that T2.43 added to `ClearOutboxAsync` in the same try/catch with a Warning as T2.42, falling back to sync `TripTopic` when the begin fails.
  - Covers: NFR-9 ("a Post / ClearOutbox call"). ADR step 10 (containment).
  - Depends on: T2.43, T2.42
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:447-498`.

- [ ] **CHARACTERISE: T2.44 - A request-path trip waits for its write and affects another instance's sweep**
  - **USE COMMAND**: `/tdd:test-first when post async on a request only instance fails to publish should wait for the store write and another instance's sweep should exclude the topic`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_post_async_fails_to_publish_should_wait_for_the_write_and_exclude_the_topic_elsewhere.cs`
  - Test should verify:
    - Instance A (no sweeper) and instance B (sweeper) on one store whose write completes asynchronously (a gated `TaskCompletionSource` released after a yield). `PostAsync` on A does not return until the write has completed, and B's next sweep excludes `orders`.
    - AC-19 part 2: with a store that never completes, `ClearOutboxAsync` over two ids on two distinct topics, the dispatch call that `PostAsync` delegates to, returns within 5 s.
  - 🔁 RED mutation: discard the task of `WriteAsync` in `ClearOutboxAsync` instead of awaiting it. The test must fail on the "does not return until the write has completed" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.43 writes the batch in `ClearOutboxAsync`'s outer `finally`, and `PostAsync` goes through it. This test pins FR-3.5 and AC-9.
  - Covers: FR-3.5, NFR-3, AC-9, AC-19 (part 2).
  - Depends on: T2.43, T2.25
  - References: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/`.

- [ ] **CHARACTERISE: T2.45 - Messages later in a dispatch call are still attempted after a trip**
  - **USE COMMAND**: `/tdd:test-first when the first message of a batch trips its topic should still attempt the later messages for that topic`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_the_first_message_trips_its_topic_should_still_attempt_later_messages_for_it.cs`
  - Test should verify:
    - Batch `orders`, `orders`, `invoices`, where the first send fails. The second `orders` is attempted, `TrippedTopics` lists `orders` from the first failure, and the store sees it only at the end of the call.
  - 🔁 RED mutation: skip messages whose topic is already tripped in the batch. The test must fail on the "second `orders` is attempted" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. Mid-call dispatch is unchanged from today. This test pins AC-40 at mediator level.
  - Covers: FR-3.7 (mid-call dispatch unchanged), AC-40 (mediator half).
  - Depends on: T2.39
  - References: `src/Paramore.Brighter/OutboxProducerMediator.cs:1257-1313`.

- [ ] **CHARACTERISE: T2.46 - A tripped topic does not hold back other topics, with either breaker**
  - **USE COMMAND**: `/tdd:test-first when one topic is tripped should sweep a full batch of other topics with either breaker`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_one_topic_is_tripped_should_sweep_a_full_batch_of_other_topics.cs`
  - Test should verify:
    - 200 outstanding `orders` messages, 100 `invoices`, BatchSize 100 and `orders` tripped. Once with the in-memory breaker and once with the distributed breaker, 100 `invoices` and no `orders` are dispatched.
    - With 50 tripped topics, a 51st topic's messages are dispatched.
  - 🔁 RED mutation: pass `trippedTopics: null` at `OutboxProducerMediator.cs:763` instead of the breaker's topics. The test must fail on "no `orders` are dispatched" for both the in-memory and the distributed breaker. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. ADR 0028's filter on `OutstandingMessagesAsync` stands.
  - Covers: FR-6, FR-6.2, AC-24.
  - Depends on: T2.39
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/SweepableOutbox.cs`. `docs/adr/0028-support-circuit-breaking-of-topics.md`.

- [ ] **CHARACTERISE: T2.47 - Store reads per sweep and per refresh are a fixed number**
  - **USE COMMAND**: `/tdd:test-first when sweeps run with ten and with ten thousand outstanding messages should make the same fixed number of store reads and no writes`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CircuitBreaker/Distributed"
  - Test file: `When_sweeps_run_with_many_outstanding_messages_should_make_a_fixed_number_of_store_reads.cs`
  - Test should verify:
    - With a counting store, a sweep with 10 and a sweep with 10,000 outstanding messages, none failing, make the same number of reads (one) and no writes.
    - A `StartRefresh` makes the same fixed number of reads.
  - 🔁 RED mutation: call `ReadTrippedTopicsAsync` once per dispatched message instead of once per sweep. The test must fail on the "same number of reads" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.39 makes one read per sweep and T2.29 one per refresh. This test pins NFR-2 and AC-36.
  - Covers: NFR-2, AC-36 (read parts).
  - Depends on: T2.39, T2.29
  - References: ADR 0089 *Testing strategy* (counting double).

- [ ] **CHARACTERISE: T2.48 - Breaker registration stays opt in**
  - **USE COMMAND**: `/tdd:test-first when no circuit breaker is registered and a publish fails should exclude no topic from later sweeps`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_no_circuit_breaker_is_registered_and_a_publish_fails_should_exclude_no_topic.cs`
  - Test should verify:
    - With no `IAmAnOutboxCircuitBreaker` registered, a failed publish leaves later sweeps with no tripped topics.
    - With `InMemoryOutboxCircuitBreaker` registered as a singleton, `TrippedTopics` reports only that instance's trips.
  - 🔁 RED mutation: make `ServiceCollectionExtensions.OutboxCircuitBreaker` fall back to a new `InMemoryOutboxCircuitBreaker`. The test must fail on the "no topic is excluded" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. Brighter still registers no breaker (OQ-3).
  - Covers: FR-7, FR-7.1, FR-7.2, AC-25. OQ-3.
  - Depends on: T2.39
  - References: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` (`OutboxCircuitBreaker`, `GetService`).

### Phase 2f - Sweeper-driven refresh

- [ ] **TEST + IMPLEMENT: T2.49 - Every running sweeper refreshes the view on each tick, whatever the lock outcome**
  - **USE COMMAND**: `/tdd:test-first when every sweep is lock unavailable should still refresh the shared breaker's view each tick`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_every_sweep_is_lock_unavailable_should_still_refresh_the_shared_breakers_view.cs`
  - Test should verify:
    - Instance B's every tick is `lock_unavailable`, and A's write of `orders` completed at t = 0. B's `TrippedTopics` lists `orders` by `TimerInterval` + `StoreTimeout`.
    - Requests closer together than half a `TimerInterval` are ignored.
    - A sweeper resolved through `UseOutboxSweeper` with a registered distributed breaker requests a refresh, so the optional constructor parameter is filled from DI.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add a trailing `IAmAnOutboxCircuitBreaker? circuitBreaker = null` parameter to the `TimedOutboxSweeper` constructor (`:75-95`).
    - In `SweepUntilStopped` (`:164-187`), call `StartRefresh` before `Sweep()` at `:178` when the breaker is `IAmASharedOutboxCircuitBreaker`.
    - Guard each request as `Record` does (`:286-290`), so an exception from the breaker never reaches the loop.
    - The sweeper, which knows `TimerInterval`, ignores a request made less than half a `TimerInterval` after the previous one, judged on its `TimeProvider`. The breaker does not know `TimerInterval` and has no such guard.
  - Covers: FR-4A, NFR-4 (freshness), AC-16 (part 1). OQ-12. ADR step 11. ADR 0085 amendment (one constructor parameter, refresh).
  - Depends on: T2.29
  - References: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs:75-95,:164-187,:286-290`. `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/HeldDistributedLock.cs`. `src/Paramore.Brighter.Outbox.Hosting/HostedServiceCollectionExtensions.cs:54`.

- [ ] **TEST + IMPLEMENT: T2.50 - An overrun timer refreshes the view while a sweep is held**
  - **USE COMMAND**: `/tdd:test-first when a sweep overruns the timer interval should refresh the shared breaker's view from the overrun timer`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_overruns_the_timer_interval_should_refresh_the_view_from_the_overrun_timer.cs`
  - Test should verify:
    - A sweep held for 3 × `TimerInterval` by a dispatch double. Another instance's write of `orders` completes 0.5 × `TimerInterval` into the sweep.
    - This instance's `TrippedTopics` lists `orders` within `TimerInterval` + `StoreTimeout` of that write, before the held sweep ends.
    - After stop, the timer is disposed and no further refresh is requested.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Create the timer once from the sweeper's `TimeProvider` with an infinite due time, and only when the breaker is an `IAmASharedOutboxCircuitBreaker` (the same check as T2.49's refresh request).
    - Arm it at the start of `Sweep()` (`:224`), disarm it in a `finally`, and dispose it in the loop's `finally` (`:183`).
  - Covers: FR-4A (overrun), AC-16 (held-sweep part). ADR risk: "overrun timer fires late".
  - Depends on: T2.49
  - References: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs:183,:224`. `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/GatedOutboxProducerMediator.cs`.

- [ ] **CHARACTERISE: T2.51 - A sweeper with an in-memory or no breaker creates no refresh timer**
  - **USE COMMAND**: `/tdd:test-first when the sweeper's breaker does not share trips should create no overrun timer and request no refresh`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweepers_breaker_does_not_share_trips_should_create_no_overrun_timer.cs`
  - Test should verify:
    - With `InMemoryOutboxCircuitBreaker`, and with no breaker, the sweeper's `TimeProvider` creates no timers beyond today's.
  - 🔁 RED mutation: create the overrun timer for every breaker type (move its creation out of the shared-breaker check). The test must fail on the "creates no timers beyond today's" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.50 creates the overrun timer only for a shared breaker. This test pins NFR-1 for the in-memory and no-breaker cases.
  - Covers: NFR-1. ADR *Why the sweeper's tick drives refresh*.
  - Depends on: T2.50
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/TimerCountingTimeProvider.cs`.

### Phase 2g - Health signals for a shared breaker

- [ ] **TEST + IMPLEMENT: T2.52 - The tripped-topics gauge reports the shared view on every running sweeper**
  - **USE COMMAND**: `/tdd:test-first when the breaker shares trips should report the tripped topics gauge on an inactive running sweeper`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_the_breaker_shares_trips_should_report_the_tripped_topics_gauge_on_an_inactive_sweeper.cs`
  - Test should verify:
    - A (active) and B (last sweep `lock_unavailable`) share `orders` and `invoices`. B's gauge records 2, and still records 2 part-way through obtaining the lock.
    - Once stopped, B records 0. With no sweeper record, the gauge records 0.
    - Before any successful read, a sweeper that has tripped nothing records 0, and records 1 after tripping `orders` itself.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Change `SweeperMeter.ObserveTrippedTopics` (`SweeperMeter.cs:123-130`) to the ladder: a shared breaker is read on any running sweeper, and other breakers are gated on `IsActive` as today.
  - Covers: FR-8, FR-8.1, AC-26 (gauge parts), AC-16 (gauge parts). ADR step 12. ADR 0085 amendment.
  - Depends on: T2.49
  - References: `src/Paramore.Brighter/Observability/SweeperMeter.cs:123-130`. `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/When_the_sweeper_is_not_active_should_report_zero_tripped_topics.cs`.

- [ ] **TEST + IMPLEMENT: T2.53 - The investigate check reports shared trips on every running sweeper**
  - **USE COMMAND**: `/tdd:test-first when the breaker shares trips should report the tripped topics condition on an inactive running sweeper`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_breaker_shares_trips_should_report_tripped_topics_on_an_inactive_sweeper.cs`
  - Test should verify:
    - B is inactive. The check is Degraded with `tripped_topics` naming `invoices, orders`, with `Data` `count` and `topics` and remedy kind investigate unchanged.
    - Once stopped, B's check does not report the condition.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Change `TrippedTopicsConditionOrNone` (`OutboxSweeperInvestigateHealthCheck.cs:104-117`) to the ladder.
  - Covers: FR-8.2, AC-26 (check parts). ADR 0086 amendment.
  - Depends on: T2.52
  - References: `src/Paramore.Brighter.Outbox.Hosting/HealthChecks/OutboxSweeperInvestigateHealthCheck.cs:104-117`.

- [ ] **CHARACTERISE: T2.54 - Gating is unchanged for in-memory and third-party breakers**
  - **USE COMMAND**: `/tdd:test-first when an in memory breaker has a trip on an inactive sweeper should report zero and healthy as before`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_an_in_memory_breaker_has_a_trip_on_an_inactive_sweeper_should_report_zero_and_healthy.cs`
  - Test should verify:
    - `InMemoryOutboxCircuitBreaker` with `orders` tripped and the sweeper inactive: the gauge records 0 and the check is Healthy.
    - The same for `ListedTopicsCircuitBreaker`, a third-party breaker that does not implement `IAmASharedOutboxCircuitBreaker`.
  - 🔁 RED mutation: remove the `IsActive` gate for every breaker type. The test must fail on "the gauge records 0" for both the in-memory and the third-party case. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.52 and T2.53 gate only breakers that implement `IAmASharedOutboxCircuitBreaker`.
  - Covers: FR-8.3, AC-27. OQ-8.
  - Depends on: T2.53
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/When_the_sweeper_is_not_active_should_not_report_tripped_topics.cs`, `When_a_third_party_circuit_breaker_reports_tripped_topics_should_report_them.cs`.

- [ ] **TEST + IMPLEMENT: T2.55 - The investigate check reports the store-unavailable condition with exception_type**
  - **USE COMMAND**: `/tdd:test-first when the shared breaker is using its fallback view should report tripped topics store unavailable with the exception type`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_shared_breaker_is_using_its_fallback_view_should_report_store_unavailable.cs`
  - Test should verify:
    - The last store operation threw `InvalidOperationException`: the check is Degraded with `tripped_topics_store_unavailable`, remedy kind investigate, `Data` `exception_type` = `InvalidOperationException`, and the description text from the ADR.
    - After a timeout: `exception_type` = `timeout`.
    - `consecutive_failed_sweeps` is reported alongside it, in the order the ADR gives.
    - An instance that started during an outage reports the condition.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `TrippedTopicsStoreUnavailable = "tripped_topics_store_unavailable"` beside `:49-55`, and evaluate it from `StoreStatus` through the shared `RemedyCondition` data builder.
  - Covers: FR-8.4 (report), FR-5.8 (check part), AC-28, AC-23 (check part). OQ-10. ADR 0086 amendment. ADR 0088 not amended.
  - Depends on: T2.53, T2.30a
  - References: `src/Paramore.Brighter.Outbox.Hosting/HealthChecks/OutboxSweeperInvestigateHealthCheck.cs:49-55`. `docs/adr/0088-shared-remedy-check-data-and-condition.md`.

- [ ] **CHARACTERISE: T2.55a - The store-unavailable condition clears with the fallback state and is not reported before a read fails**
  - **USE COMMAND**: `/tdd:test-first when the fallback state ends should stop reporting the store unavailable condition and not report it before any read has failed`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_fallback_state_ends_should_stop_reporting_store_unavailable.cs`
  - Test should verify:
    - After a successful read and no unshared trip, the check no longer reports `tripped_topics_store_unavailable`.
    - A shared breaker that has not yet read and holds no unshared trip does not report it (ladder row 8).
  - 🔁 RED mutation: make `StoreStatus.IsUsingFallback` ignore a successful read, so the read-failure part is never cleared. The test must fail on the "no longer reports" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.55 reports the condition from `StoreStatus.IsUsingFallback` only.
  - Covers: FR-8.4 (clears), AC-21, ADR ladder rows 6 and 8.
  - Depends on: T2.55
  - References: `src/Paramore.Brighter.Outbox.Hosting/HealthChecks/OutboxSweeperInvestigateHealthCheck.cs:104-117`.

- [ ] **CHARACTERISE: T2.56 - Health signals follow the fallback view and never block**
  - **USE COMMAND**: `/tdd:test-first when the trip store blocks indefinitely should observe the gauge and run the check within a second from the fallback view`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_trip_store_blocks_indefinitely_should_complete_the_gauge_and_check_within_a_second.cs`
  - Test should verify:
    - With a blocking store and a fallback view holding `orders`, `RecordObservableInstruments()` and `CheckHealthAsync` each return within 1 s, reporting 1 and `orders`.
  - 🔁 RED mutation: make `TrippedTopics` wait on the in-flight read (`GetAwaiter().GetResult()`). The test must fail on the "within 1 s" assertion for the gauge and the check. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.35 and T2.52-T2.55 read only the view and `StoreStatus`. This test pins AC-29 through the gauge and the check.
  - Covers: FR-8.5, NFR-4, AC-29.
  - Depends on: T2.55a, T2.35
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/When_the_sweep_is_blocked_indefinitely_should_complete_both_health_checks_within_a_second.cs`.

### Phase 2h - SQL trip stores on real databases

- [ ] **TEST + IMPLEMENT: T2.57 - The MsSql trip store passes the write contract against SQL Server**
  - **USE COMMAND**: `/tdd:test-first when the mssql trip store runs the write contract against a real database should pass every write contract case`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/CircuitBreaker"
  - Test file: `When_the_mssql_trip_store_runs_the_write_contract_should_pass.cs`
  - Test should verify:
    - Every `TrippedTopicStoreWriteContractTest` case (T2.4, T2.5, T2.8) passes on a real SQL Server.
    - `GetDDL(table, schema)` creates the table and the secondary index, and the test creates its table from it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `MsSqlTrippedTopicStore(IAmARelationalDbConnectionProvider, string tableName = "BrighterTrippedTopics", string? schemaName = null, TimeSpan? commandTimeout = null)` in `src/Paramore.Brighter.Outbox.MsSql/`.
    - `WriteAsync`: the UPDATE `WITH (UPDLOCK, SERIALIZABLE)` ... `OUTPUT` then INSERT pair per topic, in ordinal topic order, in one transaction and one command batch.
    - `ReadAsync`: return the unmarked rows (a `SELECT ... WHERE EndedAt IS NULL`); `Ended` is empty until T2.57a.
    - Add `public static string GetDDL(string tableName, string? schemaName = null)` creating the four-column table (`NVARCHAR(255)` topic key, `DATETIMEOFFSET(7)` stamps) and the secondary index on the ended marker and stamp.
  - Covers: FR-7.4, NFR-7, AC-33 (write contract part). ADR step 13. OQ-1.
  - Depends on: T2.8, T2.5, T2.2
  - References: `src/Paramore.Brighter.Outbox.MsSql/SqlOutboxBuilder.cs` (`GetDDL` pattern). `tests/Paramore.Brighter.MSSQL.Tests/MsSqlTestHelper.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.57a - The MsSql trip store marks ended rows and gives each to one caller**
  - **USE COMMAND**: `/tdd:test-first when the mssql trip store runs the ended contract against a real database should mark ended rows for exactly one caller`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/CircuitBreaker"
  - Test file: `When_the_mssql_trip_store_runs_the_ended_contract_should_pass.cs`
  - Test should verify:
    - Every `TrippedTopicStoreEndedContractTest` case (T2.6, T2.7) passes on a real SQL Server: a read marks ended rows and returns each to exactly one caller across 1,000 parallel repetitions, and a write over an ended row revives it only with a later stamp.
    - The store never deletes a row.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `UPDATE ... SET EndedAt = @now OUTPUT INSERTED.Topic, INSERTED.TripTime WHERE EndedAt IS NULL AND TripTime <= @cutoff`, then the unmarked `SELECT`, in one batch.
  - Covers: FR-4.4, FR-7.4, NFR-5, AC-33 (ended contract part). ADR steps 13 and 15.
  - Depends on: T2.57, T2.6, T2.7
  - References: `src/Paramore.Brighter.Outbox.MsSql/SqlOutboxBuilder.cs` (`GetDDL` pattern). `tests/Paramore.Brighter.MSSQL.Tests/MsSqlTestHelper.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.57b - The MsSql trip store purges only old ended rows and rounds its command timeout**
  - **USE COMMAND**: `/tdd:test-first when the mssql trip store is configured should round its command timeout up to whole seconds and offer a purge that deletes only old ended rows`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/CircuitBreaker"
  - Test file: `When_the_mssql_trip_store_is_configured_should_round_the_command_timeout_and_offer_a_purge.cs`
  - Test should verify:
    - `commandTimeout` is rounded up to whole seconds with a minimum of 1, observed on the executed command through a recording connection provider (a test double this task adds that wraps `DbConnection` and `DbCommand` and records `CommandTimeout`).
    - `GetPurgeSql` returns a parameterised `DELETE` with the parameter `@EndedBefore`. Run on a real SQL Server under a connection that may delete, with a bound cutoff, it removes only marked rows whose `EndedAt` is earlier than the cutoff, and never an unmarked row.
    - The store's own commands contain no `DELETE`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Apply the rounding to the command timeout of every command the store runs, creating every command through `connection.CreateCommand()` (never `new SqlCommand`/`new NpgsqlCommand`/`new MySqlCommand`) so the recording connection provider can see its `CommandTimeout`.
    - Add `public static string GetPurgeSql(string tableName, string? schemaName = null)`.
  - Covers: FR-7.4, NFR-3 (command timeout). ADR 0089 *The trip table and the SQL stores*, Alternative 15.
  - Depends on: T2.57a
  - References: `src/Paramore.Brighter.Outbox.MsSql/SqlOutboxBuilder.cs` (`GetDDL` pattern). `tests/Paramore.Brighter.MSSQL.Tests/MsSqlTestHelper.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.58 - The PostgreSql trip store passes the write contract against PostgreSQL**
  - **USE COMMAND**: `/tdd:test-first when the postgresql trip store runs the write contract against a real database should pass every write contract case`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/CircuitBreaker"
  - Test file: `When_the_postgresql_trip_store_runs_the_write_contract_should_pass.cs`
  - Test should verify:
    - Every `TrippedTopicStoreWriteContractTest` case (T2.4, T2.5, T2.8) passes on a real PostgreSQL, with lower-case column names.
    - `GetDDL(table, schema)` creates the table and the secondary index, and the test creates its table from it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `PostgreSqlTrippedTopicStore` in `src/Paramore.Brighter.Outbox.PostgreSql/` with the constructor shape used by the MsSql store.
    - `WriteAsync`: one `INSERT ... VALUES (...) ON CONFLICT (topic) DO UPDATE SET ... RETURNING topic, prior_trip_time, trip_time, ended_at` in one transaction, topics in ordinal order.
    - `ReadAsync`: return the unmarked rows; `Ended` is empty until T2.58a.
    - Add `GetDDL(tableName, schemaName)` creating the table (`character varying(255)` key, `timestamptz` lower-case columns) and the secondary index.
  - Covers: FR-7.4, NFR-7, AC-33 (write contract part). ADR step 13.
  - Depends on: T2.8, T2.5, T2.2
  - References: `src/Paramore.Brighter.Outbox.PostgreSql/PostgreSqlOutboxBuilder.cs`. `tests/Paramore.Brighter.PostgresSQL.Tests/PostgresSqlTestHelper.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.58a - The PostgreSql trip store marks ended rows and gives each to one caller**
  - **USE COMMAND**: `/tdd:test-first when the postgresql trip store runs the ended contract against a real database should mark ended rows for exactly one caller`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/CircuitBreaker"
  - Test file: `When_the_postgresql_trip_store_runs_the_ended_contract_should_pass.cs`
  - Test should verify:
    - Every `TrippedTopicStoreEndedContractTest` case (T2.6, T2.7) passes on a real PostgreSQL: a read marks ended rows and returns each to exactly one caller across 1,000 parallel repetitions, and a write over an ended row revives it only with a later stamp.
    - The store never deletes a row.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `UPDATE ... SET ended_at = @now WHERE ended_at IS NULL AND trip_time <= @cutoff RETURNING ...`, then the unmarked `SELECT`, in one batch.
  - Covers: FR-4.4, FR-7.4, NFR-5, AC-33 (ended contract part). ADR steps 13 and 15.
  - Depends on: T2.58, T2.6, T2.7
  - References: `src/Paramore.Brighter.Outbox.PostgreSql/PostgreSqlOutboxBuilder.cs`. `tests/Paramore.Brighter.PostgresSQL.Tests/PostgresSqlTestHelper.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.58b - The PostgreSql trip store purges only old ended rows and rounds its command timeout**
  - **USE COMMAND**: `/tdd:test-first when the postgresql trip store is configured should round its command timeout up to whole seconds and offer a purge that deletes only old ended rows`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/CircuitBreaker"
  - Test file: `When_the_postgresql_trip_store_is_configured_should_round_the_command_timeout_and_offer_a_purge.cs`
  - Test should verify:
    - `commandTimeout` is rounded up to whole seconds with a minimum of 1, observed on the executed command through a recording connection provider (a test double this task adds that wraps `DbConnection` and `DbCommand` and records `CommandTimeout`).
    - `GetPurgeSql` returns a parameterised `DELETE` with the parameter `@EndedBefore`. Run on a real PostgreSQL under a connection that may delete, with a bound cutoff, it removes only marked rows whose `EndedAt` is earlier than the cutoff, and never an unmarked row.
    - The store's own commands contain no `DELETE`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Apply the rounding to the command timeout of every command the store runs, creating every command through `connection.CreateCommand()` (never `new SqlCommand`/`new NpgsqlCommand`/`new MySqlCommand`) so the recording connection provider can see its `CommandTimeout`.
    - Add `public static string GetPurgeSql(string tableName, string? schemaName = null)`.
  - Covers: FR-7.4, NFR-3 (command timeout). ADR 0089 *The trip table and the SQL stores*, Alternative 15.
  - Depends on: T2.58a
  - References: `src/Paramore.Brighter.Outbox.PostgreSql/PostgreSqlOutboxBuilder.cs`. `tests/Paramore.Brighter.PostgresSQL.Tests/PostgresSqlTestHelper.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.59 - The MySql trip store passes the write contract against MySQL**
  - **USE COMMAND**: `/tdd:test-first when the mysql trip store runs the write contract against a real database should pass every write contract case`
  - Test location: "tests/Paramore.Brighter.MySQL.Tests/CircuitBreaker"
  - Test file: `When_the_mysql_trip_store_runs_the_write_contract_should_pass.cs`
  - Test should verify:
    - Every `TrippedTopicStoreWriteContractTest` case (T2.4, T2.5, T2.8) passes on a real MySQL, with `DATETIME(6)` UTC stamps round-tripping exactly.
    - `GetDDL(table, schema)` creates the table and the secondary index, and the test creates its table from it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `MySqlTrippedTopicStore` in `src/Paramore.Brighter.Outbox.MySql/` with the constructor shape used by the MsSql store.
    - `WriteAsync`: `INSERT ... ON DUPLICATE KEY UPDATE` with the left-to-right assignment order `PriorTripTime`, `EndedAt`, `TripTime` and `VALUES()`, then a `SELECT` in the same transaction, topics in ordinal order.
    - `ReadAsync`: return the unmarked rows; `Ended` is empty until T2.59a.
    - Add `GetDDL(tableName, schemaName)` creating the table (`VARCHAR(255)` key, `DATETIME(6)` UTC stamps) and the secondary index.
  - Covers: FR-7.4, NFR-7, AC-33 (write contract part). ADR step 13.
  - Depends on: T2.8, T2.5, T2.2
  - References: `src/Paramore.Brighter.Outbox.MySql/MySqlOutboxBuilder.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.59a - The MySql trip store marks ended rows and gives each to one caller**
  - **USE COMMAND**: `/tdd:test-first when the mysql trip store runs the ended contract against a real database should mark ended rows for exactly one caller`
  - Test location: "tests/Paramore.Brighter.MySQL.Tests/CircuitBreaker"
  - Test file: `When_the_mysql_trip_store_runs_the_ended_contract_should_pass.cs`
  - Test should verify:
    - Every `TrippedTopicStoreEndedContractTest` case (T2.6, T2.7) passes on a real MySQL: a read marks ended rows and returns each to exactly one caller across 1,000 parallel repetitions, and a write over an ended row revives it only with a later stamp.
    - The store never deletes a row.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Run the read as an explicit `REPEATABLE READ` transaction: `SELECT ... FOR UPDATE`, the marking `UPDATE` with the same predicate, then the unmarked `SELECT`.
  - Covers: FR-4.4, FR-7.4, NFR-5, AC-33 (ended contract part). ADR steps 13 and 15.
  - Depends on: T2.59, T2.6, T2.7
  - References: `src/Paramore.Brighter.Outbox.MySql/MySqlOutboxBuilder.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.59b - The MySql trip store purges only old ended rows and rounds its command timeout**
  - **USE COMMAND**: `/tdd:test-first when the mysql trip store is configured should round its command timeout up to whole seconds and offer a purge that deletes only old ended rows`
  - Test location: "tests/Paramore.Brighter.MySQL.Tests/CircuitBreaker"
  - Test file: `When_the_mysql_trip_store_is_configured_should_round_the_command_timeout_and_offer_a_purge.cs`
  - Test should verify:
    - `commandTimeout` is rounded up to whole seconds with a minimum of 1, observed on the executed command through a recording connection provider (a test double this task adds that wraps `DbConnection` and `DbCommand` and records `CommandTimeout`).
    - `GetPurgeSql` returns a parameterised `DELETE` with the parameter `@EndedBefore`. Run on a real MySQL under a connection that may delete, with a bound cutoff, it removes only marked rows whose `EndedAt` is earlier than the cutoff, and never an unmarked row.
    - The store's own commands contain no `DELETE`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Apply the rounding to the command timeout of every command the store runs, creating every command through `connection.CreateCommand()` (never `new SqlCommand`/`new NpgsqlCommand`/`new MySqlCommand`) so the recording connection provider can see its `CommandTimeout`.
    - Add `public static string GetPurgeSql(string tableName, string? schemaName = null)`.
  - Covers: FR-7.4, NFR-3 (command timeout). ADR 0089 *The trip table and the SQL stores*, Alternative 15.
  - Depends on: T2.59a
  - References: `src/Paramore.Brighter.Outbox.MySql/MySqlOutboxBuilder.cs`. ADR 0089 *The trip table and the SQL stores*.

- [ ] **TEST + IMPLEMENT: T2.60a - The MsSql trip store retries a deadlock once against SQL Server**
  - **USE COMMAND**: `/tdd:test-first when the mssql trip store write is chosen as a deadlock victim on a real database should retry once and succeed`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/CircuitBreaker"
  - Test file: `When_the_mssql_trip_store_is_chosen_as_a_deadlock_victim_should_retry_once_and_succeed.cs`
  - Test should verify:
    - Open a test connection and transaction that updates the row of the last topic in ordinal order (say `z`) and runs `SET DEADLOCK_PRIORITY HIGH`, so SQL Server picks the store as the deadlock victim. Start the store's write of `[a, z]`: it locks `a` and blocks on `z`. Then have the test connection update `a`, which closes the cycle.
    - Build the store with a `commandTimeout` of 30 s, well above SQL Server's 5 s deadlock-monitor interval, so the blocked command is chosen as the victim before it times out.
    - The store's write raises SQL Server error 1205 on its first attempt, is retried once after the test connection commits, and then succeeds: the call returns and the stored stamps are the written ones.
    - A non-deadlock failure (the trip table does not exist) is thrown to the caller as it is, so the breaker treats it as a store failure.
    - The same deadlock provoked against the store's read is retried once and succeeds.
    - Only the retry-once case is tested. A second deadlock cannot be provoked on demand on a real database, so that the helper retries exactly once and never loops is checked in review, not by a test.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Wrap the store's write and read transactions in a retry-once on SQL Server error 1205. Use no test-only seam and no `InternalsVisibleTo`; the test drives a real database.
  - Covers: NFR-9. ADR risk "Two instances' batches deadlock".
  - Depends on: T2.57b
  - References: `src/Paramore.Brighter.Outbox.MsSql/`. ADR 0089 *Risks and Mitigations*.

- [ ] **TEST + IMPLEMENT: T2.60b - The PostgreSql trip store retries a deadlock once against PostgreSQL**
  - **USE COMMAND**: `/tdd:test-first when the postgresql trip store write is chosen as a deadlock victim on a real database should retry once and succeed`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/CircuitBreaker"
  - Test file: `When_the_postgresql_trip_store_is_chosen_as_a_deadlock_victim_should_retry_once_and_succeed.cs`
  - Test should verify:
    - Open a test connection and transaction that updates the row of the last topic in ordinal order (`z`). Start the store's write of `[a, z]`: it locks `a` and blocks on `z`. About 200 ms later, well inside the default `deadlock_timeout` of 1 s, have the test connection update `a`. The store has been waiting longest, so its deadlock check fires first and it is the victim.
    - The store's write raises SQLSTATE 40P01 on its first attempt, is retried once after the test connection commits, and then succeeds: the call returns and the stored stamps are the written ones.
    - A non-deadlock failure (the trip table does not exist) is thrown to the caller as it is, so the breaker treats it as a store failure.
    - The same deadlock provoked against the store's read is retried once and succeeds.
    - Only the retry-once case is tested. A second deadlock cannot be provoked on demand on a real database, so that the helper retries exactly once and never loops is checked in review, not by a test.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Wrap the store's write and read transactions in a retry-once on SQLSTATE 40P01. Use no test-only seam and no `InternalsVisibleTo`; the test drives a real database.
  - Covers: NFR-9. ADR risk "Two instances' batches deadlock".
  - Depends on: T2.58b
  - References: `src/Paramore.Brighter.Outbox.PostgreSql/`. ADR 0089 *Risks and Mitigations*.

- [ ] **TEST + IMPLEMENT: T2.60c - The MySql trip store retries a deadlock once against MySQL**
  - **USE COMMAND**: `/tdd:test-first when the mysql trip store write is chosen as a deadlock victim on a real database should retry once and succeed`
  - Test location: "tests/Paramore.Brighter.MySQL.Tests/CircuitBreaker"
  - Test file: `When_the_mysql_trip_store_is_chosen_as_a_deadlock_victim_should_retry_once_and_succeed.cs`
  - Test should verify:
    - Open a test connection and transaction that updates the row of the last topic in ordinal order (`z`) and also modifies several padding rows, so InnoDB, which picks the transaction that has changed fewer rows, chooses the store as the victim. Start the store's write of `[a, z]`: it locks `a` and blocks on `z`. Then have the test connection update `a`.
    - The store's write raises ER_LOCK_DEADLOCK (1213) on its first attempt, is retried once after the test connection commits, and then succeeds: the call returns and the stored stamps are the written ones.
    - A non-deadlock failure (the trip table does not exist) is thrown to the caller as it is, so the breaker treats it as a store failure.
    - The same deadlock provoked against the store's read is retried once and succeeds.
    - Only the retry-once case is tested. A second deadlock cannot be provoked on demand on a real database, so that the helper retries exactly once and never loops is checked in review, not by a test.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Wrap the store's write and read transactions in a retry-once on ER_LOCK_DEADLOCK (1213). Use no test-only seam and no `InternalsVisibleTo`; the test drives a real database.
  - Covers: NFR-9. ADR risk "Two instances' batches deadlock".
  - Depends on: T2.59b
  - References: `src/Paramore.Brighter.Outbox.MySql/`. ADR 0089 *Risks and Mitigations*.

- [ ] **CHARACTERISE: T2.60 - Overlapping batch writes and reads do not deadlock on any backend**
  - **USE COMMAND**: `/tdd:test-first when overlapping trip batches and reads run concurrently against the database should complete without a deadlock failure`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/CircuitBreaker", "tests/Paramore.Brighter.PostgresSQL.Tests/CircuitBreaker", "tests/Paramore.Brighter.MySQL.Tests/CircuitBreaker"
  - Test file: `When_overlapping_trip_batches_run_concurrently_should_not_deadlock.cs` (one per backend)
  - Test should verify:
    - Parallel batches over overlapping topic sets, submitted in different orders, plus concurrent reads, repeated many times, all complete.
    - The stored stamps equal the latest written per topic.
  - 🔁 RED mutation: write topics in the order the batch supplies them, with no ordinal sort, and make the deadlock retry call the operation only once, so the retry from T2.60a-T2.60c cannot absorb the deadlock, in one store (MsSql first, then the others). The test submits overlapping topic sets in different orders for a fixed 200 repetitions and must fail on the "all complete" assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.57-T2.59a write topics in ordinal order inside one transaction. This test pins it on each real database.
  - Covers: NFR-5, NFR-9. ADR risk "Two instances' batches deadlock".
  - Depends on: T2.60a, T2.60b, T2.60c, T2.57a, T2.58a, T2.59a
  - References: ADR 0089 *Risks and Mitigations*.

### Phase 2i - Provisioning the trip table

- [ ] **TEST + IMPLEMENT: T2.61 - BoxType gains TrippedTopics, ordered after outboxes and before inboxes**
  - **USE COMMAND**: `/tdd:test-first when box provisioning runs a trip table provisioner should run it after outboxes and before inboxes`
  - Test location: "tests/Paramore.Brighter.Core.Tests/BoxProvisioning"
  - Test file: `When_box_provisioning_hosted_service_starts_it_should_provision_tripped_topics_after_outbox_before_inbox.cs`
  - Test should verify:
    - Provisioners for Inbox, TrippedTopics and Outbox run in the order Outbox, TrippedTopics, Inbox.
    - The existing `BoxType` values keep their numbers.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Append `TrippedTopics` to `BoxType.cs:29-41`, and add it to `OrderingOrdinal` in `BoxProvisioningHostedService.cs:111-118`.
  - Covers: FR-7.4 (provisioning). OQ-1 (box provisioning). ADR step 14.
  - Depends on: T2.1
  - References: `src/Paramore.Brighter.BoxProvisioning/BoxType.cs:29-41`. `src/Paramore.Brighter.BoxProvisioning/BoxProvisioningHostedService.cs:111-118`. `tests/Paramore.Brighter.Core.Tests/BoxProvisioning/When_box_provisioning_hosted_service_starts_it_should_provision_outbox_before_inbox.cs`.

- [ ] **TEST + IMPLEMENT: T2.62 - TrippedTopicsProvisioner runs the backend migration runner for the trip table**
  - **USE COMMAND**: `/tdd:test-first when the tripped topics provisioner runs should guard the identifiers and run the backend migration runner for the trip table`
  - Test location: "tests/Paramore.Brighter.Core.Tests/BoxProvisioning"
  - Test file: `When_the_tripped_topics_provisioner_runs_it_should_invoke_the_migration_runner_for_the_trip_table.cs`
  - Test should verify:
    - The provisioner reports `BoxType.TrippedTopics`, rejects unsafe table or schema names, and calls the runner once with the trip table's name, the schema and `BoxType.TrippedTopics`. The catalog is bound when the runner is built and is T2.63-T2.65's.
    - No payload validation is performed.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `TrippedTopicsProvisioner : IAmABoxProvisioner` in `src/Paramore.Brighter.BoxProvisioning/`, as the Spanner provisioners do.
  - Covers: FR-7.4. ADR step 14. Alternative 11 (do not generalise `SqlBoxProvisioner`).
  - Depends on: T2.61
  - References: `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxProvisioner.cs`. `docs/adr/0058-box-provisioning-rdd-role-interfaces.md`. `tests/Paramore.Brighter.Core.Tests/BoxProvisioning/TestDoubles/`.

- [ ] **TEST + IMPLEMENT: T2.63 - MsSql trip-table provisioning: fresh install, re-run and hand-created table**
  - **USE COMMAND**: `/tdd:test-first when the mssql trip table provisioner runs on a fresh database should create the trip table and be idempotent on re-run`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning"
  - Test file: `When_mssql_tripped_topics_provisioner_runs_on_fresh_database_it_should_create_the_trip_table.cs`
  - Test should verify:
    - A fresh install creates the table that `GetDDL` defines and records history keyed by the trip table's name.
    - A re-run is idempotent.
    - A table created by hand from `GetDDL` is bootstrapped, detected through the `TripTime` discriminator.
    - A non-default schema is honoured.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `MsSqlTrippedTopicsMigrationCatalog(tableName, schemaName)`, which calls `MsSqlTrippedTopicStore.GetDDL` as `MsSqlOutboxMigrationCatalog.cs:97` calls `SqlOutboxBuilder.GetDDL`.
    - Add `AddMsSqlTrippedTopics(...)` beside `AddMsSqlOutbox` (`MsSqlBoxProvisioningExtensions.cs:17`).
    - Add a `TrippedTopics => "TripTime"` case to `MsSqlBoxDetectionHelper.DiscriminatorFor` (`:199-204`).
  - Covers: FR-7.4. ADR steps 14 and 15. ADR risk "`GetDDL` and the catalog drift apart".
  - Depends on: T2.62, T2.57
  - References: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrationCatalog.cs:97`, `MsSqlBoxProvisioningExtensions.cs:17`, `MsSqlBoxDetectionHelper.cs:199-204`. `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_mssql_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`.

- [ ] **TEST + IMPLEMENT: T2.64 - PostgreSql trip-table provisioning: fresh install, re-run and hand-created table**
  - **USE COMMAND**: `/tdd:test-first when the postgresql trip table provisioner runs on a fresh database should create the trip table and be idempotent on re-run`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning"
  - Test file: `When_postgresql_tripped_topics_provisioner_runs_on_fresh_database_it_should_create_the_trip_table.cs`
  - Test should verify:
    - Fresh install, re-run idempotence, a hand-created table bootstrapped through the `trip_time` discriminator, and a non-default schema.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `PostgreSqlTrippedTopicsMigrationCatalog` and `AddPostgreSqlTrippedTopics`.
    - Add a `TrippedTopics => "trip_time"` case to `PostgreSqlBoxDetectionHelper.DiscriminatorFor` (`:245-247`).
  - Covers: FR-7.4. ADR steps 14 and 15.
  - Depends on: T2.62, T2.58
  - References: `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxDetectionHelper.cs:245-247`, `PostgreSqlOutboxMigrationCatalog.cs`, `PostgreSqlBoxProvisioningExtensions.cs`.

- [ ] **TEST + IMPLEMENT: T2.65 - MySql trip-table provisioning: fresh install, re-run and hand-created table**
  - **USE COMMAND**: `/tdd:test-first when the mysql trip table provisioner runs on a fresh database should create the trip table and be idempotent on re-run`
  - Test location: "tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning"
  - Test file: `When_mysql_tripped_topics_provisioner_runs_on_fresh_database_it_should_create_the_trip_table.cs`
  - Test should verify:
    - Fresh install, re-run idempotence, and a hand-created table bootstrapped through the `TripTime` discriminator.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `MySqlTrippedTopicsMigrationCatalog` and `AddMySqlTrippedTopics`.
    - Add a `TrippedTopics => "TripTime"` case to `MySqlBoxDetectionHelper.DiscriminatorFor` (`:194`).
  - Covers: FR-7.4. ADR steps 14 and 15.
  - Depends on: T2.62, T2.59
  - References: `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxDetectionHelper.cs:194`, `MySqlOutboxMigrationCatalog.cs`, `MySqlBoxProvisioningExtensions.cs`.

### Phase 2j - Real-database acceptance

- [ ] **CHARACTERISE: T2.66 - On each real database, a trip is shared between breakers, ends by C and survives a restart**
  - **USE COMMAND**: `/tdd:test-first when two distributed breakers share a real database trip table should share a trip, end it by the cooldown duration and keep it across a restart`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/CircuitBreaker", "tests/Paramore.Brighter.PostgresSQL.Tests/CircuitBreaker", "tests/Paramore.Brighter.MySQL.Tests/CircuitBreaker"
  - Test file: `When_two_breakers_share_a_trip_table_should_share_end_and_survive_restart.cs` (one per backend)
  - Test should verify:
    - The trip table is created through the provisioning extensions (T2.63-T2.65).
    - Breaker A trips `orders` and writes it, and breaker B reads it.
    - A recreated breaker lists it after its first read.
    - After C (shared `FakeTimeProvider`), one read marks it ended with exactly one Reset, and neither breaker lists it.
  - 🔁 RED mutation: skip the `UPDATE` that marks ended rows in the SQL read, so ended rows are never marked. The test must fail on the "exactly one Reset" assertion. Revert (per backend).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T2.57a-T2.59a implement the read. This test proves the acceptance criteria on each real database.
  - Covers: AC-33, FR-3.4, FR-4.4, FR-7.4. ADR step 15.
  - Depends on: T2.63, T2.64, T2.65, T2.57a, T2.58a, T2.59a, T2.23
  - References: ADR 0089 *Testing strategy*.

- [ ] **CHARACTERISE: T2.67 - Concurrent first trips announce one Trip and one ReTrip on each real database**
  - **USE COMMAND**: `/tdd:test-first when two breakers trip the same topic concurrently against a real database should announce exactly one trip and one retrip`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/CircuitBreaker", "tests/Paramore.Brighter.PostgresSQL.Tests/CircuitBreaker", "tests/Paramore.Brighter.MySQL.Tests/CircuitBreaker"
  - Test file: `When_two_breakers_trip_a_topic_concurrently_against_the_database_should_announce_one_trip_and_one_retrip.cs` (one per backend)
  - Test should verify:
    - The AC-11 assertions (listed once, one Trip and one ReTrip, ends C after the later stamp), repeated 1,000 times against each database, with both breakers tripping through trip batches.
  - 🔁 RED mutation: replace the atomic upsert with a read-then-insert/update, so two writers can both insert. The test must fail on the "exactly one Trip and one ReTrip" assertion within its 1,000 repetitions. Revert (per backend).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. The key-range lock (MsSql) or native upsert (PostgreSql, MySql) from T2.57-T2.59a delivers this.
  - Covers: FR-3.3, NFR-5, AC-11 (database parity). ADR risk "Two first trips both insert".
  - Depends on: T2.66
  - References: ADR 0089 *Testing strategy*, final paragraph.

### Phase 2k - Dependency guard and Phase 2 verification

- [ ] **CHARACTERISE: T2.68 - A core-only project gains no new package**
  - **USE COMMAND**: `/tdd:test-first when a project references only paramore brighter should have no new package in its dependency graph`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Architecture"
  - Test file: `When_a_project_references_only_core_should_have_no_new_package_dependency.cs`
  - Test should verify:
    - The package dependencies of `Paramore.Brighter`, read from its `obj/project.assets.json`, match a checked-in expected list (`ExpectedCorePackages.txt`, beside the test, one `Id Version` per line) created by the T2.68 author from `dotnet list src/Paramore.Brighter package --include-transitive` run at the T1.15 merge commit, in the same form the test reads from `project.assets.json` (direct plus transitive, per target framework). The test runs offline.
  - 🔁 RED mutation: temporarily add a `PackageReference` to `src/Paramore.Brighter/Paramore.Brighter.csproj`. The test must fail on the package-list assertion. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. The stores live in existing Outbox packages, and core uses `Microsoft.Bcl.TimeProvider`, which it already references.
  - Covers: NFR-7, AC-38. ADR *Testing strategy* (AC-38 package-graph test).
  - Depends on: T2.59b
  - References: `tests/Paramore.Brighter.Core.Tests/Architecture/When_the_solution_is_built_it_should_declare_no_container_types_in_core.cs`. `src/Directory.Build.props`.

- [ ] **PROJECT: T2.69 - Phase 2 verification: compatibility, full suites and database parity**
  - Build the solution. Run `Core.Tests` (`CircuitBreaker`, `Sweeping`, `Observability/Metrics`, `Confirmation`, `CommandProcessors`, `BoxProvisioning`, `Architecture`) and the MSSQL, PostgresSQL and MySQL test projects against real databases (`/test-infra:run-tests`).
  - AC-31: `ListedTopicsCircuitBreaker` and `ThrowingOutboxCircuitBreaker` are unchanged, `IAmAnOutboxCircuitBreaker` is unchanged, and every existing Sweeping and Metrics test passes unchanged.
  - Confirm the unchanged list in ADR 0089 *Where each type is touched* (`OutboxSweeperState`, `RemedyCondition`, the counter attributes, the sync `Dispatch()` path, the control-bus breakers).
  - Database parity: every contract case and acceptance test passes on all three backends.
  - Covers: NFR-1, AC-31, NFR-9 (suite-wide), FR-7.4.
  - Depends on: T2.1-T2.68
  - References: `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/ListedTopicsCircuitBreaker.cs`, `ThrowingOutboxCircuitBreaker.cs`.

### Phase 2l - Decision records, release notes and Docs list

- [ ] **DOC: T2.70 - Add amendment notes to ADRs 0028, 0058, 0085 and 0086**
  - Add or extend a short amendment note in the Status section of each ADR that points to ADR 0089 and names the parts that change:
    - `docs/adr/0028-support-circuit-breaking-of-topics.md`: extend the T1.14a note to all three replaced decisions.
    - `docs/adr/0058-outbox-circuit-breaker-trip-logging.md`: extend the T1.14a note with the distributed announcer rules and the `circuit_breaker.cooldown_duration` span attribute.
    - `docs/adr/0085-track-outbox-sweeper-state-and-publish-gauges.md`: no `IsActive` gating for shared breakers. Not `0085-gcp-purge-server-boundary.md`.
    - `docs/adr/0086-sweeper-remedy-health-checks.md`: the new `tripped_topics` applies-when rule and `tripped_topics_store_unavailable`.
  - Update frontmatter with `/adr:write_adr_metadata` where relevant.
  - Confirm that ADR 0089 is Accepted and that ADR 0088 needs no amendment.
  - Covers: FR-9.1, AC-34 (ADR part). OQ-11. ADR step 16. Amendments to 0028/0058/0085/0086.
  - Depends on: T2.69
  - References: `docs/adr/0089-distributed-outbox-circuit-breaker.md` (*Amends*).

- [ ] **DOC: T2.71 - Complete the FR-9.2 release notes in the existing marked section**
  - Check the `<!-- spec: 0039-circuit-breaker-stale-tripped-topics -->` section under `## Master` in `release_notes.md` (from commit 7f650ff19, possibly narrowed in T1.14). Adjust it with `/spec:write_release_notes`, without rewriting it blindly, so that it names:
    - the new breaker, `IAmAnOutboxCircuitBreakerAsync`, `IAmATripBatch`, `IAmASharedOutboxCircuitBreaker`, and the options `CooldownDuration`, `ClockSkewLeeway` and `StoreTimeout`;
    - the D default and its effect on existing users (FR-7.2);
    - the request-path change, the latency of up to `StoreTimeout`, and write amplification of one write per distinct topic per dispatch call (FR-3.5);
    - the health-signal changes: no gating for shared breakers, and `tripped_topics_store_unavailable` (FR-8);
    - the breaking-change bullets for the `TimedOutboxSweeper` constructor and `BoxType`;
    - the SQL stores, provisioning extensions, `GetDDL` and `GetPurgeSql`.
  - Covers: FR-9.2, AC-34 (release-notes part). ADR step 16.
  - Depends on: T2.70
  - References: `release_notes.md:3-17`.

- [ ] **DOC: T2.72 - List the BrighterCommand/Docs changes in the PR description**
  - In the Phase 2 PR description, list the pages to change in BrighterCommand/Docs (writing them is tracked separately):
    - Circuit-breaker page: register `DistributedOutboxCircuitBreaker` (and `InMemoryOutboxCircuitBreaker`) as a singleton shared with the mediator (FR-7.3). The clock assumption: hosts run NTP, and `ClockSkewLeeway` covers residual skew. The table name passed to the store must match the one passed to `Add{Backend}TrippedTopics`. Set the store's `commandTimeout` equal to `StoreTimeout`, and keep `StoreTimeout` at or under half of `TimerInterval`. The purge script (`GetPurgeSql`, with the `@EndedBefore` cutoff the operator binds) runs under a separate account with `DELETE` permission, so the application account needs only `SELECT`, `INSERT` and `UPDATE`.
    - Telemetry page: with the distributed breaker every instance reports the deployment-wide count, so alert on `max` across instances, not `sum`. Counts agree within `TimerInterval` + `StoreTimeout` (FR-8.7). The new span attribute. The difference in announcement rate between breakers.
    - Health-check page: the `tripped_topics_store_unavailable` condition, its `exception_type`, and the changed applies-when rule for `tripped_topics`.
  - End the PR description with the attribution line required by the session.
  - Covers: FR-9.3, FR-7.3, FR-8.7, AC-35. ADR step 16.
  - Depends on: T2.71
  - References: ADR 0089 *Constructing and registering the pieces* and *Why 2 seconds for the store timeout*.

---

## Coverage cross-reference

### 1. Requirements to tasks

| Requirement | Tasks |
| --- | --- |
| FR-1 | T1.4, T1.5, T1.13 |
| FR-1.1 | T1.6 |
| FR-1.2 | T1.4, T1.13 |
| FR-1.3 | T1.4 |
| FR-1.4 | T1.7, T1.8, T1.9, T1.10 |
| FR-2 | T1.2, T2.3 |
| FR-2.1 | T1.2, T1.3, T2.9 |
| FR-2.2 | T1.2, T1.12 |
| FR-2.3 | T1.2, T1.5 |
| FR-2.4 | T2.3, T2.20 |
| FR-2.5 | T1.1, T2.9 |
| FR-3 | T2.10 |
| FR-3.1 | T2.13, T2.39 |
| FR-3.2 | T2.19 |
| FR-3.3 | T2.14, T2.16a, T2.18, T2.67 |
| FR-3.4 | T2.23, T2.66 |
| FR-3.5 | T2.44 |
| FR-3.6 | T2.10, T2.12, T2.39 |
| FR-3.7 | T2.8, T2.10, T2.11, T2.12, T2.13, T2.16a, T2.39, T2.39a, T2.41, T2.43, T2.45 |
| FR-3.8 | T2.4, T2.7, T2.14, T2.15, T2.16, T2.16a |
| FR-3.9 | T2.28, T2.32, T2.38 |
| FR-4 | T2.20, T2.21 |
| FR-4.1 | T2.22 |
| FR-4.2 | T2.23 |
| FR-4.3 | T2.20 |
| FR-4.4 | T2.6, T2.7, T2.10, T2.16, T2.20, T2.57a, T2.58a, T2.59a, T2.66 |
| FR-4A | T2.49, T2.50 |
| FR-5 | T2.27 |
| FR-5.1 | T2.27, T2.40 |
| FR-5.2 | T2.28, T2.28a |
| FR-5.3 | T2.29 |
| FR-5.4 | T2.25 |
| FR-5.5 | T2.30, T2.30a, T2.30b, T2.31, T2.32 |
| FR-5.6 | T2.24, T2.29 |
| FR-5.7 | T2.28, T2.33 |
| FR-5.8 | T2.34, T2.40, T2.55 |
| FR-6 | T2.46 |
| FR-6.1 | T2.27, T2.40 |
| FR-6.2 | T2.46 |
| FR-7 | T2.48 |
| FR-7.1 | T2.48 |
| FR-7.2 | T1.7, T1.12, T1.14, T2.48 |
| FR-7.3 | T2.72 |
| FR-7.4 | T2.4, T2.57-T2.59b, T2.61-T2.66, T2.69 |
| FR-8 | T2.52 |
| FR-8.1 | T2.52 |
| FR-8.2 | T2.53 |
| FR-8.3 | T2.54 |
| FR-8.4 | T2.30, T2.30a, T2.30b, T2.55, T2.55a |
| FR-8.5 | T2.56 |
| FR-8.6 | T1.7, T2.16, T2.16a, T2.17, T2.33 |
| FR-8.7 | T2.72 |
| FR-9 | T1.14, T1.14a, T2.70-T2.72 (sub-items below) |
| FR-9.1 | T1.14a, T2.70 |
| FR-9.2 | T1.14, T2.71 |
| FR-9.3 | T2.72 |
| NFR-1 | T1.1, T1.3, T1.15, T2.1, T2.51, T2.69 |
| NFR-2 | T2.8, T2.11, T2.12, T2.26, T2.39a, T2.43, T2.47 |
| NFR-3 | T2.3, T2.10, T2.25, T2.26, T2.44, T2.57b, T2.58b, T2.59b |
| NFR-4 | T2.31, T2.35, T2.49, T2.56 |
| NFR-5 | T1.1, T1.10, T1.11, T2.6, T2.7, T2.18, T2.31, T2.36, T2.57a, T2.58a, T2.59a, T2.60, T2.67 |
| NFR-6 | T1.4, T2.5, T2.10, T2.14, T2.24 |
| NFR-7 | T1.15, T2.57, T2.58, T2.59, T2.68 |
| NFR-8 | T1.1, T1.10, T1.11, T2.10, T2.36 |
| NFR-9 | T2.25, T2.26, T2.27, T2.28, T2.28a, T2.38, T2.40, T2.41, T2.42, T2.43a, T2.60a-T2.60c, T2.69 |
| AC-1 | T1.4 |
| AC-2 | T1.5 |
| AC-3 | T1.6 |
| AC-4 | T1.13 |
| AC-5 | T1.7, T1.8, T1.9 |
| AC-6 | T1.2, T1.3, T2.9 |
| AC-7 | T1.12 |
| AC-8 | T2.39 |
| AC-9 | T2.44 |
| AC-10 | T2.19 |
| AC-11 | T2.18, T2.67 |
| AC-12 | T2.23, T2.39d |
| AC-13 | T2.20, T2.39d |
| AC-14 | T2.21, T2.39d |
| AC-15 | T2.22, T2.39d |
| AC-16 | T2.49, T2.50, T2.52 |
| AC-17 | T2.27, T2.40 |
| AC-18 | T2.28 |
| AC-19 | T2.25, T2.44 |
| AC-20 | T2.29 |
| AC-21 | T2.30, T2.30a, T2.30b, T2.55a |
| AC-22 | T2.33 |
| AC-23 | T2.34, T2.40, T2.55 |
| AC-24 | T2.46 |
| AC-25 | T2.48 |
| AC-26 | T2.52, T2.53 |
| AC-27 | T2.54 |
| AC-28 | T2.55 |
| AC-29 | T2.35, T2.56 |
| AC-30 | T2.10, T2.11, T2.39 |
| AC-31 | T1.15, T2.69 |
| AC-32 | T2.24 |
| AC-33 | T2.57, T2.57a, T2.58, T2.58a, T2.59, T2.59a, T2.66 |
| AC-34 | T2.70, T2.71 |
| AC-35 | T2.72 |
| AC-36 | T2.12, T2.39a, T2.47 |
| AC-37 | T1.11, T2.36 |
| AC-38 | T1.15, T2.68 |
| AC-39 | T2.41 |
| AC-40 | T2.13, T2.45 |
| AC-41 | T2.43 |
| AC-42 | T2.14, T2.15, T2.16 |
| AC-43 | T2.39c |
| AC-44 | T2.38 |

Every requirement and acceptance criterion maps to at least one task.

### 2. ADR 0089 decisions to tasks

| ADR decision | Tasks |
| --- | --- |
| Decision: every trip ends after a fixed elapsed time; two phases, Phase 1 mergeable alone | T1.4, T2.20, T1.15 |
| Phase 1 table rows 1-5 and the three invariants | T1.4 (row 1), T1.5 (row 2), T1.8 (row 3), T1.6 (row 4), T1.10 and T1.11 (invariants), T1.9 (time wins) |
| Phase 2 sequence diagram and its six invariants | T2.13, T2.12, T2.14, T2.20, T2.6 and T2.20 (one Reset), T2.35 |
| *How a write is announced*, rows 1-7 and the pre-check | T2.15 (1), T2.28 (2), T2.14 (3, 5), T2.16 (4, 6, pre-check), T2.16a (1, 3-6, sync/callback path), T2.33 (7) |
| *How a read ends trips* (mark, never delete, race) | T2.6, T2.20, T2.36 |
| *Refresh on every running sweeper* (tick, overrun timer, request guard, in-flight guard, Task.Run) | T2.29, T2.26, T2.49, T2.50, T2.51 |
| Health ladder rows 1-8, sequence numbers, `LastFailure`, merge, stale reads, ended rows, recently written, recovery write | T2.52-T2.55, T2.30, T2.31, T2.32, T2.34 |
| Roles: `IAmAnOutboxCircuitBreakerAsync`, `IAmATripBatch`, `IAmASharedOutboxCircuitBreaker`, `IAmATrippedTopicStore` | T2.1, T2.4, T2.10, T2.12, T2.19, T2.25, T2.29 |
| `OutboxCircuitBreakerOptions` table and validation | T1.2, T1.3, T2.3, T2.9 |
| `InMemoryOutboxCircuitBreaker` signature, monotonic clock, `{ResetCause}` | T1.1, T1.4, T1.7 |
| `DistributedOutboxCircuitBreaker` members, lock off I/O, not `IDisposable`, span `CooldownDuration` attribute | T2.9, T2.10, T2.33, T2.17 |
| Timeout through `CreateCancellationTokenSource` and `WaitAsync`; abandoned task observed | T2.25, T2.26 |
| Trip table schema, SQL operations, ordinal order, deadlock retry, `GetDDL`, `GetPurgeSql`, no `DELETE` | T2.57-T2.59b, T2.60, T2.60a-T2.60c |
| Provisioning (`BoxType.TrippedTopics`, ordering, `TrippedTopicsProvisioner`, catalogs, extensions, `DiscriminatorFor`) | T2.61-T2.65 |
| Constructing and registering (no extension; same table name; `commandTimeout` = `StoreTimeout`) | T2.48, T2.57b, T2.72 |
| OQ-1 store | T2.57-T2.59b, T2.61 |
| OQ-2 default D | T1.2 |
| OQ-3 opt in | T2.48 |
| OQ-4 leeway | T2.3, T2.24 |
| OQ-5 recovery writes | T2.32 |
| OQ-6 store timeout | T2.3, T2.9, T2.25 |
| OQ-7 interfaces | T2.1, T2.12 |
| OQ-8 keep gating | T2.54 |
| OQ-9 mark-ended update and later-of upsert | T2.4, T2.6 |
| OQ-10 names | T1.2, T2.3, T2.9, T2.55 |
| OQ-11 amend ADR 0028 | T2.70 |
| OQ-12 refresh from the tick plus an overrun timer | T2.29, T2.49, T2.50 |
| OQ-13 one spec, two phases | T1.15 |
| OQ-14 no second announcement; write ignores the token; no overlapping calls | T2.33, T2.25, T2.26 |
| Step 1 | T1.1 |
| Step 2 | T1.2, T1.3 |
| Step 3 | T1.4-T1.12 |
| Step 4 | T1.13 |
| Step 5 | T1.14 |
| Step 6 | T2.1 |
| Step 7 | T2.2-T2.8 |
| Step 8 | T2.9-T2.24 |
| Step 9 | T2.25-T2.36, T2.38 (T2.28a, T2.30a, T2.30b included), T2.39c (AC-43) |
| Step 10 | T2.39-T2.48, T2.39a, T2.39c, T2.39d, T2.43a |
| Step 11 | T2.49-T2.51 |
| Step 12 | T2.52-T2.56 (T2.55a included) |
| Step 13 | T2.57-T2.59b |
| Step 14 | T2.61-T2.65 |
| Step 15 | T2.57-T2.60c, T2.63-T2.67 |
| Step 16 | T1.14a, T2.70, T2.71, T2.72 |
| Amend ADR 0028 | T1.14a, T2.70 (notes); behaviour in T1.4, T2.20 |
| Amend ADR 0058 | T1.7, T1.14a, T2.17, T2.33, T2.70 |
| Amend ADR 0085 | T2.49, T2.52, T2.70 |
| Amend ADR 0086 | T2.53, T2.55, T2.70 |
| ADR 0088 not amended | T2.55, T2.70 (confirmation) |
| Risks and Mitigations table | T2.26 (cancellation), T2.60, T2.60a-T2.60c (deadlock), T2.67 (double insert), T2.31 (stale refresh), T2.50 (late timer), T2.63 (DDL drift), T2.72 (singleton, NTP) |

### Verification level

AC-12 to AC-15 and AC-19 part 1 are verified at breaker level (T2.20-T2.23, T2.25). The mediator hands `ReadTrippedTopicsAsync` straight to `OutstandingMessagesAsync`, and T2.39d spot-checks that pass-through with a sweep. Testing the elapsed-time rules again through a sweeper would repeat them without adding a failure mode.

### 3. Scope-creep check

No task lacks a trace to a requirement or an ADR decision. Several tasks trace to the ADR rather than to a single FR, and are kept on purpose:

- **T2.2** (Core.Tests references Base.Test): needed for ADR step 7's reusable contract suite. It follows the existing `Paramore.Brighter.Base.Test/Outbox/OutboxTest.cs` pattern.
- **T2.26, T2.28a, T2.31, T2.60, T2.60a-T2.60c**: risk mitigations named in ADR 0089 *Risks and Mitigations* and *How the remaining small points are settled*.
- **T2.17**: the `circuit_breaker.cooldown_duration` span attribute is an ADR 0058 extension that ADR 0089 decides. It is not in the requirements.

No speculative tasks were added.

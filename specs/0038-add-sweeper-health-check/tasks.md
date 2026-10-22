# Tasks

**Specification**: `0038-add-sweeper-health-check`. Detect a hung or failing outbox sweep and tripped topics, and tell an operator what kind of response each health condition needs (restart, take out of service, investigate).

**Linked issue**: [#4573](https://github.com/BrighterCommand/Brighter/issues/4573)

**Design**: three Accepted ADRs. Each one's Implementation Approach is the skeleton of the phases below.
- [ADR 0085 — Track outbox sweeper state and publish gauges](../../docs/adr/0085-track-outbox-sweeper-state-and-publish-gauges.md): **Phases 1–5, 10**
- [ADR 0086 — Sweeper remedy health checks](../../docs/adr/0086-sweeper-remedy-health-checks.md): **Phases 1, 6–10, 16–18**
- [ADR 0087 — Consumer remedy health checks](../../docs/adr/0087-consumer-remedy-health-checks.md): **Phases 1, 11–18**

## How to use this list

- **Tags** (the label `/spec:implement` and `/spec:ralph-implement` dispatch on):
  - `TEST + IMPLEMENT`: use `/test-first`; the ⛔ gate applies; the test must be RED before any production code exists.
  - `CHARACTERISE`: use `/test-first`; the ⛔ gate applies. The test is expected to be **green on arrival** because earlier tasks already deliver the behaviour. Each such task names a 🔁 RED mutation. Apply it to *production* code (never the test), check the test fails **on the assertion the task is about**, revert, check green, and run the full suite. Commit the test alone as `test:`. The mutation is never committed. If a `CHARACTERISE` test is RED on arrival, or a `TEST + IMPLEMENT` test is green on arrival, **stop and ask** (`.agent_instructions/testing.md`, *When a new test passes on first run*).
  - `STRUCTURAL`: no behaviour change (test fixtures, constants, tidy-first moves). Commit as `refactor:` / `test:`.
  - `PROJECT`: project or package wiring and build or verification steps. Commit as `chore:`.
  - `DOC`: release notes and BrighterCommand/Docs. Commit as `docs:`.
- **Scoping the gear**: phase headings are stable. A `/spec:gear` scope is the heading text without the `#`s, e.g. `Phase 8 — Sweeper investigate check`.
- **Standing obligations on every task**:
  - Test names: method `When_[condition]_should_[behaviour]`, file named after the method, class `[Behaviour]Tests`, one test per file where possible, explicit `//Arrange //Act //Assert`, Evident Data.
  - No mocks for isolation. Use doubles only for I/O (lock, mediator, channel factory, circuit breaker). Prefer the real `InMemoryOutboxCircuitBreaker`, `InMemoryLock` and `InMemoryOutbox`. One test double per file.
  - Time comes from `FakeTimeProvider`, with no real delays (NFR-4). The only wall-clock *measure* is the "completes within 1 s" assertion (NFR-1). Waits for a real-thread signal (the sweep reaches the lock or the mediator, "3 sweeps complete") carry a bounded safety timeout of at most 10 s, so a regression fails the test and does not hang the run; they are guards, not measurements.
  - **AC-20**: every test in Phases 7–10 that runs a sweeper check also asserts that its status is `Healthy` or `Degraded`, never `Unhealthy`. In Phase 17, a test that reads the `HealthReport` asserts it too; a test that asserts HTTP status codes alone is exempt, because a 503 does not distinguish Degraded from Unhealthy.
  - **CS0618**: from T15.1 onward, every test file that names `BrighterServiceActivatorHealthCheck` starts with `#pragma warning disable CS0618`. **No file under `src/` may use the obsolete type** (`src/Directory.Build.props:17` sets `TreatWarningsAsErrors`).
  - **Sweeper loop timing**: the sweeper runs one sweep at a time on one thread and schedules by elapsed time. A clock advance of a whole `TimerInterval` or more starts a sweep, and when a sweep ends after its next due time has already passed, the loop starts one more sweep at once (`TimedOutboxSweeper.cs:166-215`; only the one after that re-anchors the schedule). A test that observes the record after either event makes the loop deterministic by one of these rules:
    - **(a) Releasing a gated sweep.** Request the stop first, then open the gate, then await the stop (`var stopping = sweeper.StopAsync(CancellationToken.None); gate.Open(); await stopping.WaitAsync(TimeSpan.FromSeconds(10));`). The token is uncancellable, so the await returns only when the loop thread has ended. The loop ends when the released sweep ends, so no catch-up sweep runs and the sweeper does not retake the lock. This makes the record inactive (`Stopped()`), so it suits tests of completion, failures and running time. A test never releases the sweep whose activeness or tripped topics it observes: it keeps that sweep blocked at a gate and observes it there, using T3.1's doubles that pass earlier sweeps first (T10.19 opens the lock gate, which is not a release, because the sweep stays blocked in the mediator). T10.10 releases A by this rule only to let B take the lock, and observes B.
    - **(b) Advancing the clock past a threshold with no sweep in flight.** Stop the sweeper first and await it with an uncancellable token and the same 10 s guard (`await sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))`), then advance the clock, so no sweep starts and a record left in progress shows its running time.
    - **(c) Observing a record that is not in progress.** After the sweep of interest has ended and the sweeper is idle, advance the clock by less than the time left until the next due sweep (half a `TimerInterval` is enough when each sweep was triggered by an advance that landed on its due time, or when the schedule has just re-anchored) before observing, so a record stuck in progress shows a running time above 0.
  - **Remedy kind (AC-49 c)**: every test in Phases 7, 8, 9, 10, 12, 13, 14 and 15 that runs a remedy check and returns a result also asserts `Data["remedy_kind"]` equals `RemedyCheckConvention.Name` of the kind its tag names. This is ADR 0086 step 7's rule that each per-check scenario test asserts the remedy kind. Exempt: T13.9 (the check throws, so there is no `Data`), the restart entry in T14.3 (AC-70's service-built result has empty `Data`; its take-out-of-service entry is still asserted), T15.3 (it compares the existing check's results) and the Phase 17 endpoint tests that assert HTTP status codes and the default-writer body only; T17.3, T17.4 and T17.5, which read the report, assert it.
  - **Bounded waits (NFR-1)**: each "completes within 1 s" assertion (T4.1, T10.8, T10.11) runs the call as `Task.Run(...).WaitAsync(TimeSpan.FromSeconds(1))`, so a blocked call fails the assertion and does not hang the run. Every test that gates a sweep opens the gate in a `finally` block.
  - **Stopping a gated sweep**: `StopAsync` abandons a sweep in flight only when its token is cancellable (`TimedOutboxSweeper.cs:146-149`). A test that abandons a sweep which stays blocked (T3.6, T10.13, T10.14) calls `await sweeper.StopAsync(new CancellationToken(canceled: true))`, never `CancellationToken.None`. A test that needs the loop thread to have ended (rules a and b of *Sweeper loop timing*) does the opposite: an uncancellable token, because a cancelled token returns before the thread has exited.
  - **Instrument isolation**: xUnit runs classes in parallel and a `MeterListener` sees every `Meter` in the process. Every gauge or instrument test enables only instruments whose `Meter.Scope` is its own `IMeterFactory` (the container's, for container tests), as `RecordedSweeperMeasurements` does (`Sweeping/TestDoubles/RecordedSweeperMeasurements.cs:31-35`). T3.1 extends it to observable instruments.
  - **Log capture**: `TimedOutboxSweeper` logs from its dedicated thread, which starts under `ExecutionContext.SuppressFlow()` (`TimedOutboxSweeper.cs:104`), and its logger is static. The AsyncLocal Serilog `TestCorrelator` context therefore never sees those events. Tests use T3.1's `CapturingLogSink`, a process-wide sink filtered by `SourceContext` and by a marker the test puts in the **exception message** of the double it throws from (`ThrowingSweeperMeter`, `ThrowingOutboxCircuitBreaker` and the throwing lookup lambdas take a message). Do not use a `LogContext` property or logger scope as the marker: it is AsyncLocal and is lost on the sweeper thread, and `Initializer.cs` does not enrich from `LogContext`. Classes run in parallel and other tests log the same Warning from the same `SourceContext`, so a test counts only events carrying its own marker.
  - Descriptions: tests assert the **values** in a description, not its exact wording (FR-8), except the shared phrases AC-62 compares.

---

## Phase 1 — Project wiring and names

*ADR 0086 step 1, ADR 0087 step 1, ADR 0085 step 1, the ADR 0086 step 7 test-project references, and AC-24.*

- [x] **PROJECT: T1.1 — Reference `Microsoft.Extensions.Diagnostics.HealthChecks` from `Paramore.Brighter.Outbox.Hosting`**
  - Add `<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks"/>` to `src/Paramore.Brighter.Outbox.Hosting/Paramore.Brighter.Outbox.Hosting.csproj`. No version attribute: central package management pins 10.0.12 at `Directory.Packages.props:93`.
  - No code change. Build `src` for every target in `$(BrighterTargetFrameworks)` with warnings as errors.
  - References: ADR 0086 *Technology Choices: Why the checks live in Outbox.Hosting*; NFR-5.
  - Depends on: nothing.

- [x] **PROJECT: T1.2 — Reference `Microsoft.Extensions.Diagnostics.HealthChecks` from `Paramore.Brighter.ServiceActivator.Extensions.Diagnostics`**
  - Add the same `<PackageReference/>` to `src/Paramore.Brighter.ServiceActivator.Extensions.Diagnostics/Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.csproj`, beside the existing `...HealthChecks.Abstractions` reference.
  - No code change. Build `src` with warnings as errors.
  - References: ADR 0087 step 1; NFR-5.
  - Depends on: nothing.

- [x] **PROJECT: T1.3 — Wire `tests/Paramore.Brighter.Extensions.AspNetCore.Tests` to the packages its guard and endpoint tests need**
  - Add `ProjectReference`s to `src/Paramore.Brighter.Outbox.Hosting` and `src/Paramore.Brighter.ServiceActivator.Extensions.Diagnostics`. Add `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing"/>`, which the project lacks today.
  - Build the test project. No new warnings.
  - References: ADR 0086 step 7 (the AC-49 and endpoint tests name both assemblies); ADR 0087 steps 9–10.
  - Depends on: T1.1, T1.2.

- [x] **STRUCTURAL: T1.4 — Gauge-name constants in `BrighterSemanticConventions`**
  - Add `OutboxSweeperSweepRunningTime = "paramore.brighter.outbox_sweeper.sweep.running_time"` and `OutboxSweeperTrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics"` beside the existing sweeper names (`src/Paramore.Brighter/Observability/BrighterSemanticConventions.cs:109-112`), with XML docs.
  - References: ADR 0085 step 1; NFR-8; ADR 0010.
  - Depends on: nothing.

- [x] **CHARACTERISE: T1.5 — `Paramore.Brighter.Outbox.Hosting` references no ASP.NET Core, OpenTelemetry or ServiceActivator diagnostics assembly**
  - **USE COMMAND**: `/test-first when the outbox hosting assembly is loaded it should reference no asp net core opentelemetry or service activator diagnostics assembly`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_outbox_hosting_assembly_is_loaded_should_reference_no_aspnetcore_opentelemetry_or_consumer_diagnostics.cs`
  - Test should verify:
    - `typeof(TimedOutboxSweeper).Assembly.GetReferencedAssemblies()` contains no name starting `Microsoft.AspNetCore`, no name starting `OpenTelemetry`, and not `Paramore.Brighter.ServiceActivator.Extensions.Diagnostics`.
  - 🔁 RED mutation: temporarily add a `ProjectReference` from Outbox.Hosting to `Paramore.Brighter.ServiceActivator.Extensions.Diagnostics` and use one of its types in a source file. The test must fail on the forbidden-reference assertion. Revert both.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. T1.1 already holds. This test guards AC-24 against regression.
  - References: AC-24 (first half); ADR 0086 step 1.
  - Depends on: T1.1.

- [x] **CHARACTERISE: T1.6 — `Paramore.Brighter.ServiceActivator.Extensions.Diagnostics` references no ASP.NET Core or OpenTelemetry assembly**
  - **USE COMMAND**: `/test-first when the consumer diagnostics assembly is loaded it should reference no asp net core or opentelemetry assembly`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_consumer_diagnostics_assembly_is_loaded_should_reference_no_aspnetcore_or_opentelemetry.cs`
  - Test should verify:
    - The referenced assemblies of the assembly holding `BrighterServiceActivatorHealthCheck` include no `Microsoft.AspNetCore*` and no `OpenTelemetry*`. Reach the type through `Type.GetType` with its assembly-qualified name, or write the pragma, because T15.1 will make the type obsolete.
  - 🔁 RED mutation: temporarily add `<PackageReference Include="OpenTelemetry"/>` and use one OpenTelemetry type in a source file of the package. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing. This is a guard for AC-24 (second half).
  - References: AC-24; ADR 0087 step 1.
  - Depends on: T1.2.

- [x] **PROJECT: T1.7 — Record the resolved package graph before and after, for both packages (AC-24)**
  - Run `dotnet list <csproj> package --include-transitive` for `Paramore.Brighter.Outbox.Hosting` and for `Paramore.Brighter.ServiceActivator.Extensions.Diagnostics`, at the branch point (`git merge-base HEAD master`) and after T1.1/T1.2. Diff the outputs.
  - Pass condition: there are no new direct references other than `Microsoft.Extensions.Diagnostics.HealthChecks`, and the only new transitive packages are those it brings (expected: its `.Abstractions`, plus `Microsoft.Extensions.Hosting.Abstractions` and its dependencies for the ServiceActivator diagnostics package, as ADR 0087 *Negative* states). Paste the diff into the PR description.
  - References: AC-24; NFR-5; ADR 0086 *Why the checks live in Outbox.Hosting*.
  - Depends on: T1.1, T1.2.

- [x] **PROJECT: T1.8 — Reference `Paramore.Brighter.Outbox.Hosting` from `tests/Paramore.Brighter.Extensions.Tests`**
  - Add a `ProjectReference` to `src/Paramore.Brighter.Outbox.Hosting` to `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj`, which does not reference it today. T15.3 calls `AddBrighterOutboxSweeperHealthChecks` from this project.
  - Build the test project. No new warnings.
  - References: AC-55; T15.3.
  - Depends on: T1.1.

---

## Phase 2 — OutboxSweeperState

*ADR 0085 step 2. These tests drive the record directly on the test thread with `FakeTimeProvider`.*

- [x] **TEST + IMPLEMENT: T2.1 — A new sweeper state reports nothing recorded**
  - **USE COMMAND**: `/test-first when a sweeper state has recorded nothing it should report no sweep in progress no failures no last outcome and not active`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/SweeperState"
  - Test file: `When_a_sweeper_state_has_recorded_nothing_should_report_no_sweep_in_progress_and_not_active.cs`
  - Test should verify:
    - `Read()` gives `IsSweepInProgress == false`, `RunningTime == TimeSpan.Zero`, `HoldsLock == false`, `ConsecutiveFailedSweeps == 0`, `LastOutcome == null`, `IsStopped == false` and `IsActive == false`.
    - `new OutboxSweeperState(null!)` throws `ArgumentNullException`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter/Observability/OutboxSweeperSnapshot.cs`: a public `readonly record struct` with `IsSweepInProgress`, `RunningTime`, `HoldsLock`, `ConsecutiveFailedSweeps`, `LastOutcome` (`SweepOutcome?`), `IsStopped` and `IsActive`.
    - Add `src/Paramore.Brighter/Observability/OutboxSweeperState.cs`: a public sealed class taking a `TimeProvider clock`, guarded by a `private readonly object` lock (not `System.Threading.Lock`, which is unavailable on netstandard2.0). `Read()` copies the fields under the lock. XML `<remarks>` say the sweeper is the only intended writer.
  - References: ADR 0085 *Key Components: OutboxSweeperState, OutboxSweeperSnapshot*; FR-2 (before the first sweep ends).
  - Depends on: nothing.

- [x] **TEST + IMPLEMENT: T2.2 — Running time is measured on the record's clock from sweep start, and returns to 0 when the sweep ends**
  - **USE COMMAND**: `/test-first when a sweep has started and the clock advances the sweeper state should report its running time and zero once it ends`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/SweeperState"
  - Test file: `When_a_sweep_has_started_and_the_clock_advances_should_report_its_running_time.cs`
  - Test should verify:
    - After `SweepStarted()`, advancing the `FakeTimeProvider` 25 s gives `IsSweepInProgress == true` and `RunningTime == 25 s`.
    - After `SweepEnded(SweepOutcome.Completed)`, `IsSweepInProgress == false` and `RunningTime == TimeSpan.Zero`, even after further advances.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `SweepStarted` stores `clock.GetTimestamp()`, sets in-progress true and holds-lock false.
    - `SweepEnded` clears in-progress.
    - `Read` computes running time as `clock.GetElapsedTime(start)` only when the copied in-progress flag is true, clamped at zero (ADR 0085 invariant). The clamp is defensive and untested, because `TimeProvider.GetElapsedTime` over a `FakeTimeProvider` never goes backwards.
  - References: FR-1, FR-3.1, FR-3.2, NFR-4; ADR 0085 *operations table*.
  - Depends on: T2.1.

- [x] **TEST + IMPLEMENT: T2.3 — The record counts consecutive failed sweeps and keeps the last outcome**
  - **USE COMMAND**: `/test-first when sweeps end with mixed outcomes the sweeper state should count consecutive failures and keep the last outcome`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/SweeperState"
  - Test file: `When_sweeps_end_with_mixed_outcomes_should_count_consecutive_failures_and_keep_the_last_outcome.cs`
  - Test should verify:
    - failed, failed, lock_unavailable, failed gives count 3 and `LastOutcome == Failed`.
    - failed, failed, completed, failed gives count 1.
    - A completed sweep sets the count to 0. A lock_unavailable sweep leaves it unchanged.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `SweepEnded(outcome)` records the last outcome. Completed sets the count to 0, failed adds 1, lock_unavailable leaves it.
  - References: FR-2 examples; ADR 0085 *operations table*, invariant *the failed count changes only on SweepEnded*.
  - Depends on: T2.2.

- [x] **TEST + IMPLEMENT: T2.4 — The record decides "active sweeper" by the decision ladder**
  - **USE COMMAND**: `/test-first when the sweeper state moves through lock and outcome transitions it should report active exactly as the active sweeper ladder decides`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/SweeperState"
  - Test file: `When_the_sweeper_state_moves_through_lock_transitions_should_decide_active_by_the_ladder.cs`
  - Test should verify (one assertion per ladder row 2–5 and per FR-2 example):
    - in progress with `LockObtained()` gives active and `HoldsLock == true`;
    - in progress still acquiring, even after a completed sweep, gives not active;
    - no sweep in progress and the last sweep obtained the lock (completed, or failed after `LockObtained`) gives active;
    - completed then lock_unavailable gives not active;
    - lock_unavailable, then a sweep that obtained the lock and failed, gives active;
    - a sweep that failed with no `LockObtained` (failed inside `ObtainLockAsync`) gives not active;
    - `LockObtained()` with no sweep in progress is ignored.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `LockObtained()` (sets holds-lock only when in progress).
    - `SweepEnded` copies holds-lock into last-sweep-obtained-lock and clears holds-lock.
    - `Read` derives `IsActive` from the copied fields in ladder order (row 1, stopped, comes in T2.5).
  - References: FR-2, the *Active sweeper* definition; ADR 0085 *decision ladder* rows 2–5.
  - Depends on: T2.3.

- [x] **TEST + IMPLEMENT: T2.5 — A stop makes the sweeper inactive and resets nothing else**
  - **USE COMMAND**: `/test-first when stop is requested the sweeper state should become inactive and keep its count last outcome and sweep in progress`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/SweeperState"
  - Test file: `When_stop_is_requested_should_become_inactive_and_keep_everything_else.cs`
  - Test should verify:
    - After 3 failed sweeps then `Stopped()`: count 3, last outcome failed, `IsStopped`, not active.
    - After a completed sweep then `Stopped()`: not active (ladder row 1).
    - A sweep in progress at 25 s, then `Stopped()`, then +31 s gives running time 56 s, still in progress (FR-1.1).
    - A later `SweepEnded` records its outcome and the sweeper stays inactive.
    - Calling `Stopped()` twice is idempotent.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `Stopped()`, which sets one flag. Ladder row 1 is evaluated first in `Read`.
  - References: FR-1.1, FR-1.2; AC-45 and AC-64 at record level; ADR 0085 invariant *a stop changes one field*.
  - Depends on: T2.4.

- [x] **CHARACTERISE: T2.6 — Tracking transitions allocate nothing**
  - **USE COMMAND**: `/test-first when the sweeper state is driven through ten thousand sweeps it should allocate nothing`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/SweeperState"
  - Test file: `When_the_sweeper_state_is_driven_through_ten_thousand_sweeps_should_allocate_nothing.cs`
  - Test should verify:
    - After a warm-up pass, `GC.GetAllocatedBytesForCurrentThread()` shows 0 bytes across 10,000 rounds of `SweepStarted`, `LockObtained`, `SweepEnded` (rotating completed, failed, lock_unavailable) and `Read()`.
  - 🔁 RED mutation: make `SweepEnded` rebuild a history array on every call (`_history = _history.Append(outcome).ToArray()`). The assertion must fail on bytes > 0. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected. If the test is RED on arrival, stop and ask.
  - References: NFR-3; AC-48 bullet 1; ADR 0085 *Why a short lock*.
  - Depends on: T2.5.

---

## Phase 3 — TimedOutboxSweeper writes its record

*ADR 0085 step 3. Real `TimedOutboxSweeper`, `FakeTimeProvider`, and blocking doubles at each blocking point.*

- [x] **STRUCTURAL: T3.1 — Test doubles for blocked, failing and tripped sweeps**
  - Location: `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/`, one class per file, each with an XML summary.
  - Reuse the existing `HeldDistributedLock` (always lock-unavailable), `ThrowingSweeperMeter`, `RecordedSweeperMeasurements` and `SweepableOutbox`.
  - Extend the existing `GatedDistributedLock` (`Sweeping/TestDoubles/GatedDistributedLock.cs`), which starts closed and cannot be closed again, so it can pass the first N calls before it gates (`PassFirst(n)`). Existing users keep their behaviour by default.
  - Add:
    - `GatedOutboxProducerMediator`: an `IAmAnOutboxProducerMediator` that signals when the sweep reaches it (after the lock is obtained) and blocks until `Open()`. It can be told to complete or throw on release, and to complete or throw its first N calls before it gates (`CompleteFirst(n)`, `ThrowFirst(n)`). The "sweep reached the gate" signal of both gated doubles fires only for a call that is held at the gate, never for a call that passes, so a test waiting on it observes the gated sweep and not an earlier one.
    - `ThrowingDistributedLock`: `ObtainLockAsync` throws on every call.
    - `SwitchableOutboxProducerMediator`: completes or throws, switched by the test between sweeps (T10.4, T10.5, T10.21).
    - `SwitchableDistributedLock`: grants the lock or reports it unavailable, switched by the test between sweeps.
    - Give the existing `ThrowingSweeperMeter` an optional message parameter (its current fixed message stays the default), so a test can plant a unique marker.
    - `ThrowingOutboxCircuitBreaker`: `TrippedTopics` throws `InvalidOperationException` with a caller-supplied message; `TripTopic` and `CoolDown` do nothing.
    - A `SweepableOutbox` overload that takes an `IAmAnOutboxCircuitBreaker` and passes it to the mediator's optional `outboxCircuitBreaker` argument (`OutboxProducerMediator.cs:146`), so a test can share one breaker between the mediator and a check.
    - A container helper (`AddBrighter`, producers, a registered `IDistributedLock` double, a `TimeProvider`, and an optional `UseOutboxSweeper`) shared by T3.7, T5.2–T5.4 and T9.5–T9.7. T9.5 calls it without the sweeper. No test calls `UseOutboxSweeper` today.
    - `CapturingLogSink`: a process-wide Serilog sink, wired where `tests/Paramore.Brighter.Core.Tests/Initializer.cs` configures Serilog for `ApplicationLogging`, that keeps only events that carry an exception at Warning or above (the marker lives in the exception message, and Core.Tests logs heavily at Debug), recording level, `SourceContext`, message and exception, and lets a test filter by `SourceContext` and by exception message (the per-test marker; see Standing obligations).
    - An extension of `RecordedSweeperMeasurements` that records observable-instrument measurements (`RecordObservableInstruments()`), enabling only instruments whose `Meter.Scope` is the test's `IMeterFactory`.
  - Add a throwing-mediator registration helper only if the existing `AddTransient<IAmAnOutboxProducerMediator>(_ => throw ...)` pattern (`When_a_sweep_throws_should_record_a_failed_sweep.cs:20`) does not fit.
  - Why no test of its own: these are fixtures. They are consumed by Phases 3, 4, 5, 8, 9 and 10. Phase 17 is in another project and uses T16.0. The pass-first, gated-after-N and switchable doubles serve T10.4, T10.5, T10.7, T10.11, T10.12, T10.15, T10.17, T10.18, T10.19 and T10.21.
  - Depends on: nothing.

- [x] **TEST + IMPLEMENT: T3.2 — A sweep blocked in lock acquisition is in progress, with its running time, and not active**
  - **USE COMMAND**: `/test-first when a sweep is blocked acquiring the lock the sweeper state should report it in progress with its running time and not active`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_is_blocked_acquiring_the_lock_should_record_it_in_progress_and_not_active.cs`
  - Test should verify:
    - Build the sweeper with an `OutboxSweeperState` the test holds, sharing the sweeper's `FakeTimeProvider`, and a `GatedDistributedLock`.
    - Once the sweep reaches the lock and the clock advances 40 s, `state.Read()` shows in progress, `RunningTime == 40 s`, `HoldsLock == false`, `IsActive == false`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs`: add the optional `OutboxSweeperState? state = null` constructor parameter (`:73-79`). When it is null, create `new OutboxSweeperState(_timeProvider)` (`:88`).
    - Call `SweepStarted()` in `Sweep()` after tick lag is recorded and before lock acquisition (`:219-221`).
  - References: FR-1 (the sweep includes lock acquisition), AC-2 at record level; ADR 0085 step 3.
  - Depends on: T2.5, T3.1.

- [x] **TEST + IMPLEMENT: T3.3 — A sweep blocked in the outbox holds the lock and is active**
  - **USE COMMAND**: `/test-first when a sweep is blocked in the outbox after obtaining the lock the sweeper state should report it holds the lock and is active`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_is_blocked_in_the_outbox_should_record_that_it_holds_the_lock.cs`
  - Test should verify:
    - With `InMemoryLock` and `GatedOutboxProducerMediator`, once the mediator is reached and the clock advances 25 s: in progress, 25 s, `HoldsLock == true`, `IsActive == true`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `SweepAsync()`, call `LockObtained()` after the null check on `lockId` (`:244-249`).
  - References: FR-1, FR-2, AC-1 at record level; ADR 0085 step 3.
  - Depends on: T3.2.

- [x] **TEST + IMPLEMENT: T3.4 — Each sweep outcome ends the sweep in the record**
  - **USE COMMAND**: `/test-first when a sweep ends completed failed or lock unavailable the sweeper state should record the outcome and no sweep in progress`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_ends_should_record_its_outcome_and_no_sweep_in_progress.cs`
  - Test should verify:
    - A completed sweep (`SweepableOutbox`), a failed sweep (mediator resolution throws) and a lock-unavailable sweep (`HeldDistributedLock`) each leave running time 0 and not in progress, with `LastOutcome` equal to that outcome.
    - The failed sweep gives count 1 and active. The completed sweep gives count 0 and active. Lock-unavailable gives not active.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `Sweep()`, call `SweepEnded(outcome)` immediately after `SweepOnce()` returns, before `RecordSweep`.
  - References: FR-1, FR-2, FR-3.2 (AC-3 at record level); ADR 0085 step 3.
  - Depends on: T3.3.

- [x] **CHARACTERISE: T3.5 — A failure thrown by the lock counts as failed and leaves the sweeper inactive**
  - **USE COMMAND**: `/test-first when obtaining the lock throws the sweeper state should count a failed sweep and not be active`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_obtaining_the_lock_throws_should_count_a_failed_sweep_and_not_be_active.cs`
  - Test should verify:
    - With `ThrowingDistributedLock`, after 3 sweeps: count 3, last outcome failed, `IsActive == false`.
  - 🔁 RED mutation: in `TimedOutboxSweeper`, call `LockObtained()` before `ObtainLockAsync` instead of after it. The assertion on `IsActive == false` must fail. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: Definitions (a failed sweep includes lock acquisition); AC-15 and AC-52 at record level.
  - Depends on: T3.4.

- [x] **TEST + IMPLEMENT: T3.6 — Stopping the sweeper marks the record stopped, even before it started, and an abandoned sweep keeps running**
  - **USE COMMAND**: `/test-first when the sweeper is stopped with a sweep in flight the sweeper state should be stopped and the abandoned sweep keep its running time`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweeper_is_stopped_with_a_sweep_in_flight_should_mark_stopped_and_keep_the_sweep_running.cs`
  - Test should verify:
    - A sweep blocked by `GatedOutboxProducerMediator` for 25 s. Call `StopAsync` with a cancelled token, which abandons the sweep (ADR 0081; see Standing obligations). Advance 31 s: `IsStopped`, not active, in progress, running time 56 s.
    - A separate sweeper on which `StopAsync` is called before `StartAsync` gives `IsStopped`.
    - A sweeper that is `Dispose`d gives `IsStopped`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Call `Stopped()` first in `StopAsync` (`:115`), before the early return for a never-started sweeper, and first in `Dispose` (`:129`), before the `_disposed` guard.
  - References: FR-1.1, FR-1.2; AC-45 at record level; ADR 0085 step 3.
  - Depends on: T3.4.

- [x] **TEST + IMPLEMENT: T3.7 — `UseOutboxSweeper` registers one record, built from the container's clock, and the sweeper writes to it**
  - **USE COMMAND**: `/test-first when use outbox sweeper is called it should register one sweeper state the hosted sweeper writes to even when called twice`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_use_outbox_sweeper_is_called_should_register_one_sweeper_state_the_sweeper_writes_to.cs`
  - Test should verify:
    - The container has a `FakeTimeProvider` registered as `TimeProvider`. After `UseOutboxSweeper`, resolving `OutboxSweeperState` twice gives the same instance.
    - Starting the resolved hosted sweeper with a blocked sweep changes that instance (in progress; running time follows the container's `FakeTimeProvider`).
    - Calling `UseOutboxSweeper` twice registers one `OutboxSweeperState` and one sweeper hosted service.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter.Outbox.Hosting/HostedServiceCollectionExtensions.cs` (`:47`): `TryAddSingleton` the record, built from `sp.GetService<TimeProvider>() ?? TimeProvider.System`. The hosted-service registration is unchanged. The user's call signature is unchanged.
  - References: FR-4A, FR-4A.2; NFR-4; ADR 0085 *Why the container creates the record*.
  - Depends on: T3.4.

- [x] **CHARACTERISE: T3.8 — The sweeper's per-sweep allocation does not grow with sweeps**
  - **USE COMMAND**: `/test-first when ten thousand sweeps run the sweeper per sweep allocation should not grow`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_ten_thousand_sweeps_run_should_not_grow_the_per_sweep_allocation.cs`
  - Test should verify:
    - A meter double (new `AllocationReadingSweeperMeter` in `Sweeping/TestDoubles/`) reads `GC.GetAllocatedBytesForCurrentThread` in `RecordTickLag` and `RecordSweep`. The doubles allocate nothing.
    - After a warm-up of 100 sweeps, the mean bytes per sweep over the last 100 sweeps is ≤ the mean over sweeps 101–200.
  - 🔁 RED mutation: make `OutboxSweeperState.SweepEnded` rebuild a history array on every sweep (`_history = _history.Append(outcome).ToArray()`), so the cost per sweep grows with the sweep count. The mean over the last 100 sweeps must exceed the earlier mean. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: NFR-3; AC-48 bullet 2.
  - Depends on: T3.6.

---

## Phase 4 — Sweeper gauges on SweeperMeter

*ADR 0085 step 4. `SweeperMeter` is built over the `IMeterFactory` from `ServiceCollection.AddMetrics()`, with a record and a breaker lambda. Observe with `MeterListener.RecordObservableInstruments()`.*

- [x] **TEST + IMPLEMENT: T4.1 — The running-time gauge reports the running time while the sweep blocks in the outbox**
  - **USE COMMAND**: `/test-first when a sweep blocks in the outbox the running time gauge should report its running time in seconds`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_a_sweep_blocks_in_the_outbox_should_report_the_running_time_gauge.cs`
  - Test should verify:
    - A `MeterListener` subscribed to `paramore.brighter.outbox_sweeper.sweep.running_time`. The sweeper is blocked by `GatedOutboxProducerMediator` and the clock advances 25 s.
    - `RecordObservableInstruments()` returns within 1 s of wall-clock time and records 25.0 with unit `s` and no attributes.
    - A meter built with `state: null` publishes the gauge, records 0 and does not throw (FR-4A.1).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter/Observability/SweeperMeter.cs`: add the optional constructor parameters `OutboxSweeperState? state = null` and `Func<IAmAnOutboxCircuitBreaker?>? findCircuitBreaker = null`.
    - Create `ObservableGauge<double>` `BrighterSemanticConventions.OutboxSweeperSweepRunningTime`, unit `s`, whose callback returns 0 when the `state` field is null (the field is nullable and `src` builds with warnings as errors) and otherwise reads `_state.Read().RunningTime.TotalSeconds`.
    - The existing `SweeperMeter(IMeterFactory)` callers compile unchanged.
  - References: FR-3, FR-3.1, FR-3.4, FR-4A.1 (null state), NFR-1; AC-1; ADR 0085 *SweeperMeter*.
  - Depends on: T1.4, T3.3.

- [x] **CHARACTERISE: T4.2 — The running-time gauge includes lock acquisition**
  - **USE COMMAND**: `/test-first when a sweep blocks acquiring the lock the running time gauge should include the time spent waiting`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_a_sweep_blocks_acquiring_the_lock_should_include_it_in_the_running_time_gauge.cs`
  - Test should verify:
    - With `GatedDistributedLock` and +40 s, the gauge records 40.0.
  - 🔁 RED mutation: in `TimedOutboxSweeper`, move `SweepStarted()` to after `ObtainLockAsync` returns. The gauge must record 0 instead of 40. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-1, FR-3.1; AC-2.
  - Depends on: T4.1.

- [x] **CHARACTERISE: T4.3 — The running-time gauge is 0 whenever no sweep is in progress**
  - **USE COMMAND**: `/test-first when no sweep is in progress the running time gauge should report zero`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_no_sweep_is_in_progress_should_report_a_zero_running_time_gauge.cs`
  - Test should verify (AC-3 a–e):
    - The gauge records 0 (a) before the first sweep, (b) after a completed sweep, (c) after a failed sweep, (d) after a lock-unavailable sweep, and (e) after a blocked sweep is released and ends. (a) reads a bare `OutboxSweeperState` or an unstarted sweeper's meter, because a started sweeper sweeps at once at t = 0. (b)–(e) follow a clock advance of half a `TimerInterval` (rule c), and (e) releases the blocked sweep by rule (a).
  - 🔁 RED mutation: in `OutboxSweeperState.Read`, compute running time from the last start without checking the in-progress flag. Observations (b)–(e) must be non-zero. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-3.2; AC-3.
  - Depends on: T4.1.

- [x] **TEST + IMPLEMENT: T4.4 — The tripped-topics gauge reports the number of tripped topics on an active sweeper**
  - **USE COMMAND**: `/test-first when an active sweeper has topics tripped the tripped topics gauge should report how many`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_an_active_sweeper_has_topics_tripped_should_report_the_tripped_topics_gauge.cs`
  - Test should verify:
    - The record is driven to active (completed sweep that obtained the lock), with a real `InMemoryOutboxCircuitBreaker` behind the lambda.
    - 2 tripped topics record 2 (`ObservableGauge<long>`, unit `{topic}`, no attributes). None tripped records 0. A lambda returning null records 0. A meter built with `state: null` publishes the gauge, records 0 and does not throw, even when the lambda would report trips.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `SweeperMeter`, add the gauge `BrighterSemanticConventions.OutboxSweeperTrippedTopics`. The callback calls `findCircuitBreaker` on every observation and counts `TrippedTopics`. A null breaker gives 0. Return 0 when the `state` field is null. Do not read `IsActive` here: T4.5 adds that gate.
  - References: FR-3A, FR-6A.3, FR-6A.4; AC-33 at meter level; FR-4A.1 and AC-66 at meter level (the null-state half); ADR 0085 *Why the breaker is read on each observation*.
  - Depends on: T4.1.

- [x] **TEST + IMPLEMENT: T4.5 — The tripped-topics gauge reports 0, without reading the breaker, when the sweeper is not active**
  - **USE COMMAND**: `/test-first when the sweeper is not active the tripped topics gauge should report zero without reading the circuit breaker`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_the_sweeper_is_not_active_should_report_zero_tripped_topics.cs`
  - Test should verify:
    - With `orders` tripped, the gauge records 0 for a record that is: not yet ended a sweep; last outcome lock_unavailable; acquiring the lock after a completed sweep; failed without obtaining the lock; stopped after a completed sweep.
    - With a `ThrowingOutboxCircuitBreaker` behind the lambda on an inactive record, `CapturingLogSink` records nothing.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - The tripped-topics callback reads `Read().IsActive` first and touches the breaker only when it is true.
  - References: FR-3A, FR-6A.5, FR-6A.6, FR-1.2; AC-52, AC-54, AC-64 and AC-65 (gauge halves at meter level), AC-68 (inactive half).
  - Depends on: T4.4.

- [x] **TEST + IMPLEMENT: T4.7 — A failing breaker read is caught, logged at Warning, and reported as 0**
  - **USE COMMAND**: `/test-first when reading the circuit breaker throws on an active sweeper the tripped topics gauge should log a warning and report zero`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_reading_the_circuit_breaker_throws_should_log_and_report_zero_tripped_topics.cs`
  - Test should verify:
    - On an active record with `ThrowingOutboxCircuitBreaker`, `RecordObservableInstruments()` does not throw. The tripped-topics gauge records 0, a Warning is recorded by `CapturingLogSink`, and the running-time gauge still records its value.
    - The same holds when the lookup lambda itself throws.
    - A real sweeper wired to this meter keeps sweeping (a later sweep completes).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Wrap each callback body in try/catch and log at Warning through `ApplicationLogging`, passing the caught exception object to the log call (`CapturingLogSink` keeps only events that carry an exception), as the sweeper logs meter failures (`TimedOutboxSweeper.cs:301-302`).
    - Capture logs with T3.1's `CapturingLogSink` (see Standing obligations).
  - References: FR-13.2 (gauge), FR-13.3, NFR-9; AC-68 (gauge half), AC-69 (the circuit-breaker alternative, because the record is concrete).
  - Depends on: T4.5.

- [x] **CHARACTERISE: T4.8 — Both gauges have a fixed attribute set and `outbox_sweeper` names**
  - **USE COMMAND**: `/test-first when the gauges are observed after many sweeps and topics they should publish one attribute set each and outbox sweeper names`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_the_gauges_are_observed_after_many_sweeps_and_topics_should_keep_a_fixed_attribute_set.cs`
  - Test should verify:
    - Drive the record through 1,000 sweeps and trip 100 distinct topics over the run, observing throughout.
    - Each gauge publishes exactly one distinct attribute set (empty), and both names start with `paramore.brighter.outbox_sweeper.`.
  - 🔁 RED mutation: add a `topic` tag per tripped topic to the tripped-topics measurement. The distinct-set count must exceed 1. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-3.4, FR-3A, NFR-8; AC-47.
  - Depends on: T4.4.

---

## Phase 5 — Gauge registration through AddBrighterInstrumentation

*ADR 0085 step 5. Container tests; `Core.Tests` reaches `AddBrighterInstrumentation` through `Paramore.Brighter.Extensions.OpenTelemetry`.*

- [x] **TEST + IMPLEMENT: T5.1 — With instrumentation and no sweeper, both gauges are published and report 0**
  - **USE COMMAND**: `/test-first when brighter instrumentation is added without an outbox sweeper both sweeper gauges should be published and report zero`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_instrumentation_is_added_without_an_outbox_sweeper_should_publish_both_gauges_at_zero.cs`
  - Test should verify:
    - A container with `AddBrighterInstrumentation` and no `UseOutboxSweeper`. Once the meter provider is built, a `MeterListener` sees both instruments, each records 0, and nothing throws.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter.Extensions.Diagnostics/BrighterMetricsBuilderExtensions.cs`: resolve `IAmABrighterSweeperMeter` in the existing `deferred.Configure` callback (`:50`) beside `PublishConfirmationMeter`, so `SweeperMeter` is created with the meter provider.
  - References: FR-4A.1 (gauges), FR-3.3; AC-66 (gauge half); ADR 0085 *Why SweeperMeter is created when the meter provider is built*.
  - Depends on: T4.5.

- [x] **CHARACTERISE: T5.2 — With `UseOutboxSweeper` as users call it, the gauge reads the sweeper's record**
  - **USE COMMAND**: `/test-first when the sweeper is registered with use outbox sweeper and a sweep is blocked the running time gauge should report its running time`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_the_sweeper_is_registered_with_use_outbox_sweeper_should_report_its_running_time_gauge.cs`
  - Test should verify:
    - A container with an unchanged `UseOutboxSweeper(...)` call, `AddBrighterInstrumentation`, a `FakeTimeProvider` and a blocking lock. After +31 s the gauge records 31.0.
  - 🔁 RED mutation: register `OutboxSweeperState` as transient in `UseOutboxSweeper`. The meter then reads a different record and the gauge records 0 instead of 31.0. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected. Type activation of `SweeperMeter` resolves the record that T3.7 registered. If the test is RED on arrival, stop and ask.
  - References: FR-3.3, FR-4A; AC-67 (gauge half); ADR 0085 step 5; Risk *the sweeper and the meter get different record instances*.
  - Depends on: T5.1, T3.7.

- [x] **TEST + IMPLEMENT: T5.3 — The gauge reads the circuit breaker registered in the container, shared with the mediator**
  - **USE COMMAND**: `/test-first when a circuit breaker is registered as a singleton shared with the mediator the tripped topics gauge should see its trips`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_a_circuit_breaker_is_registered_as_a_shared_singleton_should_report_its_trips_on_the_gauge.cs`
  - Test should verify:
    - A singleton `InMemoryOutboxCircuitBreaker` given to the mediator and an active sweeper (completed sweep). `TripTopic` on the mediator's breaker makes the gauge record 1.
    - With no `IAmAnOutboxCircuitBreaker` registered, the gauge records 0 and nothing throws.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `TryAddSingleton<IAmABrighterSweeperMeter, SweeperMeter>()` (`BrighterMetricsBuilderExtensions.cs:41`) with a factory passing `sp.GetService<OutboxSweeperState>()` and `() => sp.GetService<IAmAnOutboxCircuitBreaker>()`.
  - References: FR-6A.4, FR-3A; AC-44 (gauge half), AC-33 (container half).
  - Depends on: T5.2.

- [x] **CHARACTERISE: T5.4 — Without `AddBrighterInstrumentation`, no sweeper gauge is published and sweeping is unchanged**
  - **USE COMMAND**: `/test-first when brighter instrumentation is not added the sweeper gauges should not be published and the sweep should complete`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_brighter_instrumentation_is_not_added_should_publish_no_sweeper_gauges.cs`
  - Test should verify:
    - Only `UseOutboxSweeper`. A sweep completes. No instrument named `...sweep.running_time` or `...tripped_topics` is published. This container has no `AddMetrics`, so there is no `IMeterFactory` scope to match: the test enables only instruments whose `Meter.Scope` is the factory it resolves from the container after build (and passes when the container has none).
  - 🔁 RED mutation: make `UseOutboxSweeper` also `TryAddSingleton<IAmABrighterSweeperMeter, SweeperMeter>()` plus `AddMetrics()`. The gauges are then published, and the assertion must fail. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-3.3, FR-3A; AC-4, AC-33 (last sentence).
  - Depends on: T5.2.

- [x] **CHARACTERISE: T5.5 — Existing sweeper instruments and circuit-breaker option defaults are unchanged**
  - **USE COMMAND**: `/test-first when a sweep completes with instrumentation the existing sweeper instruments and circuit breaker defaults should be unchanged`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/Metrics"
  - Test file: `When_a_sweep_completes_with_instrumentation_should_keep_the_existing_instruments_unchanged.cs`
  - Test should verify:
    - `tick.lag`, `sweep.duration` and `sweeps` are published with the names, units and attribute keys of the branch point, hard-coded as Evident Data.
    - `new OutboxCircuitBreakerOptions()` default values equal their branch-point values.
  - 🔁 RED mutation: rename the `paramore.brighter.outbox_sweeper.outcome` attribute key in `BrighterSemanticConventions`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: NFR-6; AC-46.
  - Depends on: T5.2.

---

## Phase 6 — Remedy-kind convention

*ADR 0086 step 2. Core values and constants shared by sweeper and consumer checks.*

- [x] **TEST + IMPLEMENT: T6.1 — Each remedy kind maps to its remedy tag and its `Data` name, and an undefined kind is rejected**
  - **USE COMMAND**: `/test-first when mapping a remedy kind it should give its brighter remedy tag and its data name and reject an undefined kind`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/RemedyChecks"
  - Test file: `When_mapping_a_remedy_kind_should_give_its_remedy_tag_and_data_name_and_reject_an_undefined_kind.cs`
  - Test should verify:
    - `Tag(Restart) == "brighter-restart" == RestartTag`; `Tag(TakeOutOfService) == "brighter-take-out-of-service"`; `Tag(Investigate) == "brighter-investigate"`.
    - `Name(...)` gives `restart`, `take_out_of_service` and `investigate`.
    - `RemedyKindKey == "remedy_kind"`, `ConditionsKey == "conditions"`.
    - `Tag((RemedyKind)99)` and `Name((RemedyKind)99)` throw `ArgumentOutOfRangeException`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter/Observability/RemedyKind.cs` (enum) and `RemedyCheckConvention.cs` (static class: `const` tags and keys, `Tag(RemedyKind)`, `Name(RemedyKind)`). Both switches end in a default arm that throws `ArgumentOutOfRangeException`, which the warnings-as-errors build requires anyway (CS8524 for a switch expression, CS0161 for a switch statement). No health-check type is named, and core gains no package reference.
  - References: FR-10.3, FR-10.4, FR-10.5; ADR 0086 *The remedy-kind convention*.
  - Depends on: nothing.

---

## Phase 7 — Sweeper restart check

*ADR 0086 step 3. Check tests construct `OutboxSweeperRestartHealthCheck` directly over a record driven on the test thread.*

- [x] **TEST + IMPLEMENT: T7.1 — With no sweeper state, the restart check is Healthy and says no sweeper is registered**
  - **USE COMMAND**: `/test-first when the restart check has no sweeper state it should report healthy and say no outbox sweeper is registered`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_restart_check_has_no_sweeper_state_should_report_healthy_with_no_sweeper_registered.cs`
  - Test should verify:
    - `new OutboxSweeperRestartHealthCheck(null, null)` gives Healthy, a description saying no outbox sweeper is registered, `Data["remedy_kind"] == "restart"` and `Data["conditions"]` an empty `string[]`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter.Outbox.Hosting/HealthChecks/OutboxSweeperRestartHealthCheck.cs` (namespace `Paramore.Brighter.Outbox.Hosting.HealthChecks`), with `const HungSweep = "hung_sweep"`, the constructor `(OutboxSweeperState? state, TimedOutboxSweeperOptions? options, TimeSpan? hangThreshold = null)`, and a synchronous `CheckHealthAsync` returning `Task.FromResult`.
  - References: FR-4A.1, FR-10.5; ADR 0086 *Descriptions*, *Data shape*.
  - Depends on: T1.1, T6.1, T2.2.

- [x] **TEST + IMPLEMENT: T7.2 — The restart check is Degraded only when running time is strictly greater than 3 × TimerInterval**
  - **USE COMMAND**: `/test-first when a sweep runs past the default hang threshold the restart check should report degraded with the running time threshold and timer interval`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_runs_past_the_hang_threshold_should_report_a_hung_sweep.cs`
  - Test should verify:
    - TimerInterval 10 s. Running 29 s gives Healthy; exactly 30 s gives Healthy; no sweep in progress gives Healthy. Each Healthy description says no sweep is hung, and `conditions` is empty.
    - `options == null` (nullable constructor parameter): the threshold is 3 × `new TimedOutboxSweeperOptions().TimerInterval`, Healthy at exactly that value and Degraded just above it, and `timer_interval_seconds` equals the default (ADR 0086: both checks use the defaults).
    - 31 s gives Degraded. The description contains 31 s, 30 s and 10 s. `conditions == ["hung_sweep"]` and `Data["hung_sweep"]` holds `running_time_seconds = 31.0`, `hang_threshold_seconds = 30.0` and `timer_interval_seconds = 10.0` (doubles).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Use `options ??= new TimedOutboxSweeperOptions()` (the parameter is nullable and `src` builds with warnings as errors). Default threshold `3 × TimeSpan.FromSeconds(options.TimerInterval)`. Strict `>` comparison on `snapshot.RunningTime`.
    - Return `HealthStatus.Degraded` explicitly. Build `Data` as a dictionary with nested condition values. Use the ADR 0086 wording.
  - References: ADR 0086 *OutboxSweeperInvestigateHealthCheck* ("When options is null, both checks use the defaults"); FR-5, FR-7.1, FR-8, FR-10.1, FR-10.5; AC-6, AC-19 (restart half), AC-39 (hung and Healthy bullets).
  - Depends on: T7.1.

- [x] **TEST + IMPLEMENT: T7.3 — An overridden hang threshold replaces the default, and seconds format to three decimals**
  - **USE COMMAND**: `/test-first when the hang threshold is overridden or the timer interval is one second the restart check should use that threshold`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_hang_threshold_is_overridden_should_judge_a_hung_sweep_against_it.cs`
  - Test should verify:
    - Override 2 min, TimerInterval 10 s: 90 s Healthy, 121 s Degraded.
    - No override, TimerInterval 1 s: 3 s Healthy, 3.001 s Degraded. The description shows `3.001s` and `3s` (invariant culture, up to 3 decimals).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Use `hangThreshold ?? default`. Add a seconds formatter (`0.###`, `CultureInfo.InvariantCulture`).
  - References: FR-7.1; AC-8, AC-10; ADR 0086 *Descriptions* (formatting).
  - Depends on: T7.2.

- [x] **TEST + IMPLEMENT: T7.4 — Constructing the restart check with a non-positive hang threshold throws**
  - **USE COMMAND**: `/test-first when the restart check is constructed with a zero or negative hang threshold it should throw argument out of range`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_restart_check_is_constructed_with_a_non_positive_hang_threshold_should_throw.cs`
  - Test should verify:
    - `TimeSpan.Zero` and `-1 s` throw `ArgumentOutOfRangeException`.
    - 5 s with TimerInterval 10 s does not throw.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Validate in the constructor.
  - References: FR-7.1, FR-7.3; AC-9 (constructor half).
  - Depends on: T7.2.

---

## Phase 8 — Sweeper investigate check

*ADR 0086 step 4. Check tests construct `OutboxSweeperInvestigateHealthCheck` directly. Sweeper-level investigate ACs are in Phase 10.*

- [x] **TEST + IMPLEMENT: T8.1 — With no sweeper state, the investigate check is Healthy and says no sweeper is registered**
  - **USE COMMAND**: `/test-first when the investigate check has no sweeper state it should report healthy and say no outbox sweeper is registered`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_investigate_check_has_no_sweeper_state_should_report_healthy_with_no_sweeper_registered.cs`
  - Test should verify:
    - Healthy, the no-sweeper description, `remedy_kind == "investigate"`, empty `conditions`.
    - The breaker lookup is never called.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter.Outbox.Hosting/HealthChecks/OutboxSweeperInvestigateHealthCheck.cs` with the three condition `const`s and the constructor `(OutboxSweeperState? state, TimedOutboxSweeperOptions? options, Func<IAmAnOutboxCircuitBreaker?>? findCircuitBreaker = null, int failureThreshold = 3)`.
  - References: FR-4A.1, FR-10.5; ADR 0086 invariant *no sweeper means Healthy*.
  - Depends on: T7.1.

- [x] **TEST + IMPLEMENT: T8.2 — The investigate check is Degraded at N consecutive failed sweeps**
  - **USE COMMAND**: `/test-first when consecutive failed sweeps reach the failure threshold the investigate check should report degraded with the count threshold and timer interval`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_consecutive_failed_sweeps_reach_the_threshold_should_report_degraded.cs`
  - Test should verify:
    - N = 3, TimerInterval 10 s.
    - 2 failures give Healthy, with a description saying none of its conditions applies.
    - 3 failures give Degraded. The description contains 3, 3 and 10 s. `conditions == ["consecutive_failed_sweeps"]` and `Data` holds `count = 3`, `threshold = 3`, `timer_interval_seconds = 10.0`.
    - 4 failures are still Degraded.
    - `options == null`: with a record at the failure threshold the check is Degraded, nothing throws, and `Data["consecutive_failed_sweeps"]["timer_interval_seconds"]` equals `(double)new TimedOutboxSweeperOptions().TimerInterval` (`TimerInterval` is an `int` number of seconds).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Use `options ??= new TimedOutboxSweeperOptions()` (nullable parameter, warnings as errors). Compare `snapshot.ConsecutiveFailedSweeps >= N`. Use the ADR 0086 wording.
  - References: FR-6, FR-8, FR-10.1, FR-10.5; AC-11 at check level, AC-19 (investigate half), AC-39 (failure and Healthy bullets).
  - Depends on: T8.1, T2.3.

- [x] **TEST + IMPLEMENT: T8.3 — The failure threshold can be 1 and must be positive**
  - **USE COMMAND**: `/test-first when the failure threshold is one or not positive the investigate check should degrade on one failure or throw argument out of range`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_failure_threshold_is_one_or_not_positive_should_degrade_or_reject.cs`
  - Test should verify:
    - N = 1 with one failed sweep gives Degraded.
    - Constructing with N = 0 or N = -1 throws `ArgumentOutOfRangeException`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Validate `failureThreshold >= 1` in the constructor.
  - References: FR-7.2, FR-7.3; AC-14 (constructor half).
  - Depends on: T8.2.

- [x] **TEST + IMPLEMENT: T8.4 — An active sweeper with tripped topics is Degraded and names them**
  - **USE COMMAND**: `/test-first when an active sweeper has topics tripped the investigate check should report degraded naming the topics`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_an_active_sweeper_has_topics_tripped_should_report_degraded_naming_them.cs`
  - Test should verify:
    - Record active after 3 completed sweeps. A real `InMemoryOutboxCircuitBreaker` has `orders` and `payments` tripped.
    - Degraded. The description contains 2, `orders` and `payments`, and says they are not being swept until their cooldown ends.
    - `conditions == ["tripped_topics"]` and `Data["tripped_topics"]` holds `count = 2` and `topics = ["orders","payments"]` (ordinal order).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Inside `CheckHealthAsync`, call `findCircuitBreaker()` and read `TrippedTopics`, sorting names with `StringComparer.Ordinal`. The first condition that applies decides the result for now; T8.7 combines them. The check keeps no copy between runs. Null-guard the delegate and the breaker it returns (`findCircuitBreaker?.Invoke()?.TrippedTopics`), because T8.2 and T8.3 build the check with no delegate.
  - References: FR-6A, FR-6A.2, FR-6A.3, FR-8, FR-10.5; AC-28, AC-39 (tripped bullet).
  - Depends on: T8.2, T2.4.

- [x] **CHARACTERISE: T8.5 — No circuit breaker, or none tripped, is Healthy**
  - **USE COMMAND**: `/test-first when no circuit breaker is registered or none is tripped the investigate check should report healthy`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_no_circuit_breaker_is_registered_or_none_is_tripped_should_report_healthy.cs`
  - Test should verify:
    - The Act captures any exception with `Record.ExceptionAsync`, and the first assertion is that none was thrown.
    - On an active record, a null lookup delegate, a lookup returning null, and an empty `TrippedTopics` all give Healthy with empty `conditions`.
  - 🔁 RED mutation: replace `findCircuitBreaker?.Invoke()?.TrippedTopics` with `findCircuitBreaker?.Invoke()!.TrippedTopics` so a lookup that returns null dereferences it (the `!` keeps the mutation compiling under warnings as errors). The "nothing throws" assertion fails on the `NullReferenceException`, because the Act captured it. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected. T8.4 null-guards the delegate and the breaker.
  - References: FR-6A, FR-6A.4; AC-30.
  - Depends on: T8.4.

- [x] **TEST + IMPLEMENT: T8.6 — A sweeper that is not active reports no tripped topics and never reads the breaker**
  - **USE COMMAND**: `/test-first when the sweeper is not active the investigate check should not report tripped topics nor read the circuit breaker`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweeper_is_not_active_should_not_report_tripped_topics.cs`
  - Test should verify:
    - With `orders` tripped, the check is Healthy for a record that is: never ended a sweep; last outcome lock_unavailable; acquiring the lock after a completed sweep; failed without the lock; stopped.
    - With a `ThrowingOutboxCircuitBreaker` on an inactive record, the check is Healthy and `CapturingLogSink` records nothing for the breaker.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Read the breaker only when `snapshot.IsActive`. Use `IsActive` as it is and never re-derive it.
  - References: FR-6A, FR-6A.5, FR-6A.6, FR-1.2; AC-52, AC-54, AC-64, AC-65 (check halves at check level), AC-68 (inactive half); ADR 0086 invariant *an inactive sweeper never touches the breaker*.
  - Depends on: T8.4, T2.5, T3.1.

- [x] **TEST + IMPLEMENT: T8.7 — Failures and tripped topics on one check are both reported, in order**
  - **USE COMMAND**: `/test-first when failures and a tripped topic apply together the investigate check should report both conditions`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_failures_and_a_tripped_topic_apply_together_should_report_both_conditions.cs`
  - Test should verify:
    - 3 failed sweeps that obtained the lock (active), and `orders` tripped.
    - Degraded. The description names the failure count and `orders`, joined with `"; "`.
    - `conditions == ["consecutive_failed_sweeps","tripped_topics"]`, each with its values (the ADR 0086 example `Data`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace T8.4's first-condition-decides shortcut: evaluate every condition in the fixed ADR order and join the descriptions.
  - References: FR-8 (more than one condition), FR-10.5; AC-38, AC-39 (combined bullet).
  - Depends on: T8.4.

- [x] **TEST + IMPLEMENT: T8.8 — More than 10 tripped topics: the description names the first 10 and the total, and `Data` lists all of them**
  - **USE COMMAND**: `/test-first when more than ten topics are tripped the investigate check should name the first ten and the total and list all in data`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_more_than_ten_topics_are_tripped_should_name_the_first_ten_and_the_total.cs`
  - Test should verify:
    - 12 tripped topics. The description names the first 10 in ordinal order and contains `2 more (12 in total)`. `Data` `topics` lists all 12 in ordinal order, and `count == 12`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Truncate the description only.
  - References: FR-8; AC-32.
  - Depends on: T8.4.

- [x] **TEST + IMPLEMENT: T8.9 — An unreadable breaker is a condition, logged at Error, and the other conditions are still reported**
  - **USE COMMAND**: `/test-first when reading tripped topics throws on an active sweeper the investigate check should report the unreadable condition with the exception type and still report failures`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_reading_tripped_topics_throws_should_report_tripped_topics_unreadable.cs`
  - Test should verify:
    - 3 sweeps that obtained the lock and failed (active), with `ThrowingOutboxCircuitBreaker`.
    - Degraded, nothing thrown. `conditions == ["consecutive_failed_sweeps","tripped_topics_unreadable"]` and `Data["tripped_topics_unreadable"]["exception_type"] == "System.InvalidOperationException"`.
    - The description says the tripped topics could not be read. `CapturingLogSink` records one Error that carries the caught exception (the check passes the exception object to the log call).
    - The same holds when the lookup delegate itself throws.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Wrap the lookup and the `TrippedTopics` read in try/catch. Log through a `[LoggerMessage]` method at Error, as `TimedOutboxSweeper.cs:281-303` does.
  - References: FR-13.2; AC-68 (check half); ADR 0086 invariant *a failed breaker read never hides another condition*.
  - Depends on: T8.7, T3.1.

- [x] **CHARACTERISE: T8.10 — Tripped-topic recovery follows the circuit breaker on the next run**
  - **USE COMMAND**: `/test-first when the circuit breaker stops listing a topic the next investigate check should stop naming it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_circuit_breaker_stops_listing_a_topic_should_stop_naming_it.cs`
  - Test should verify:
    - `orders` and `payments` tripped. After `payments` cools down, Degraded naming only `orders`. After `orders` cools down, Healthy.
  - 🔁 RED mutation: cache the topics read on the check's first run in a static field and reuse them. The second run must still name `payments`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-6A.1, FR-6A.3; AC-29.
  - Depends on: T8.4.

- [x] **CHARACTERISE: T8.11 — A re-tripped topic stays counted once**
  - **USE COMMAND**: `/test-first when a tripped topic is tripped again the investigate check should still name it once`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_tripped_topic_is_tripped_again_should_still_name_it_once.cs`
  - Test should verify:
    - `TripTopic("orders")` twice before eviction. After each, Degraded naming `orders`, with `count == 1`.
  - 🔁 RED mutation: build the check's topic list with `topics.Concat(previousRunTopics)` from a static field kept between runs (an instance field would not survive a check built fresh for each run, so the test also builds a fresh check per run). The count must become 2. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-6A.1, FR-6A.2; AC-41.
  - Depends on: T8.4.

- [x] **CHARACTERISE: T8.12 — The check agrees with a real breaker's cooldown shared with the mediator**
  - **USE COMMAND**: `/test-first when a topic trips on the breaker shared with the mediator the investigate check should report it until the cooldown evicts it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_topic_trips_on_the_shared_breaker_should_report_it_until_cooldown_evicts_it.cs`
  - Test should verify:
    - A real `InMemoryOutboxCircuitBreaker` with `CooldownCount = 2`, shared with the mediator. The sweeper has ended a completed sweep. `TripTopic("orders")` gives Degraded.
    - After two `CoolDown()` calls (including any made by later sweeps), still Degraded. After a third, Healthy.
  - 🔁 RED mutation: `OutboxSweeperInvestigateHealthCheck` ignores its `findCircuitBreaker` delegate and reads an `InMemoryOutboxCircuitBreaker` it builds itself. The trip made on the mediator's breaker is never seen, so the first Degraded assertion fails. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-6A.3; AC-31; ADR 0028.
  - Depends on: T8.4, T3.4.

- [x] **CHARACTERISE: T8.13 — A third-party breaker written against the unchanged interface is reported**
  - **USE COMMAND**: `/test-first when a third party circuit breaker reports tripped topics the investigate check should report them`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_third_party_circuit_breaker_reports_tripped_topics_should_report_them.cs`
  - Test should verify:
    - A new test double `ListedTopicsCircuitBreaker` in `Sweeping/TestDoubles/` implements only `CoolDown`, `TripTopic` and `TrippedTopics`. On an active record, its topics are named.
    - The existing circuit-breaker suites pass unmodified (run them).
  - 🔁 RED mutation: the check casts the breaker to `InMemoryOutboxCircuitBreaker` and treats anything else as none. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected. `IAmAnOutboxCircuitBreaker` is not changed.
  - References: NFR-6; AC-36.
  - Depends on: T8.4.

---

## Phase 9 — Sweeper health-check registration

*ADR 0086 step 5. `AddHealthChecks().AddBrighterOutboxSweeperHealthChecks()`; container tests run `HealthCheckService.CheckHealthAsync`.*

- [x] **TEST + IMPLEMENT: T9.1 — One opt-in call registers the restart and investigate checks, one remedy tag each**
  - **USE COMMAND**: `/test-first when the sweeper health checks are added it should register a restart check and an investigate check each with only its remedy tag`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweeper_health_checks_are_added_should_register_two_checks_each_with_its_remedy_tag.cs`
  - Test should verify:
    - `HealthCheckServiceOptions.Registrations` holds `brighter-outbox-sweeper-restart` with tags `{brighter-restart}` only, and `brighter-outbox-sweeper-investigate` with tags `{brighter-investigate}` only.
    - Without the call, neither is registered.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter.Outbox.Hosting/OutboxSweeperHealthChecksBuilderExtensions.cs` (namespace `Paramore.Brighter.Outbox.Hosting`) with `AddBrighterOutboxSweeperHealthChecks(this IHealthChecksBuilder, TimeSpan? hangThreshold = null, int failureThreshold = 3)`.
    - Add two `HealthCheckRegistration`s whose factories `GetService` the record and `TimedOutboxSweeperOptions` on every run. The investigate factory passes `() => sp.GetService<IAmAnOutboxCircuitBreaker>()`. Use `GetService`, never `GetRequiredService`. Pass `failureStatus: null` (the framework default) here; T9.4 sets Degraded.
  - References: FR-4, FR-10.3; AC-37; ADR 0086 *AddBrighterOutboxSweeperHealthChecks*, *Why the factories look up the record on every run*.
  - Depends on: T7.2, T8.9.

- [x] **TEST + IMPLEMENT: T9.2 — The call rejects a non-positive hang threshold before anything is registered**
  - **USE COMMAND**: `/test-first when the sweeper health checks are added with a non positive hang threshold the call should throw before registering anything`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_sweeper_checks_are_added_with_a_non_positive_hang_threshold_should_throw_before_registering.cs`
  - Test should verify:
    - `TimeSpan.Zero` and `-1 s` throw `ArgumentOutOfRangeException` from the call itself, with no `ServiceProvider` built, and `Registrations` stays empty.
    - 5 s with TimerInterval 10 s does not throw.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Validate first, then register.
  - References: FR-7.1; AC-9 (call half).
  - Depends on: T9.1.

- [x] **TEST + IMPLEMENT: T9.3 — The call rejects a non-positive failure threshold before anything is registered**
  - **USE COMMAND**: `/test-first when the sweeper health checks are added with a non positive failure threshold the call should throw before registering anything`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_sweeper_checks_are_added_with_a_non_positive_failure_threshold_should_throw_before_registering.cs`
  - Test should verify:
    - N = 0 and N = -1 throw `ArgumentOutOfRangeException` from the call, with nothing registered.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Validate `failureThreshold`.
  - References: FR-7.2; AC-14 (call half).
  - Depends on: T9.1.

- [x] **TEST + IMPLEMENT: T9.4 — Both sweeper registrations carry failure status Degraded**
  - **USE COMMAND**: `/test-first when the sweeper health checks are registered each registration should have failure status degraded`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweeper_health_checks_are_registered_should_have_failure_status_degraded.cs`
  - Test should verify:
    - `registration.FailureStatus == HealthStatus.Degraded` for both.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Pass `failureStatus: HealthStatus.Degraded` to both registrations in `AddBrighterOutboxSweeperHealthChecks`.
  - References: FR-13.1; AC-70 (sweeper half; the record is concrete, so the registration's failure status is the evidence).
  - Depends on: T9.1.

- [x] **CHARACTERISE: T9.5 — With no sweeper registered, both checks are Healthy and both gauges read 0**
  - **USE COMMAND**: `/test-first when the sweeper health checks are added without use outbox sweeper both checks should report no sweeper registered and both gauges zero`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_sweeper_checks_are_added_without_a_sweeper_should_report_no_sweeper_registered.cs`
  - Test should verify:
    - `AddBrighterOutboxSweeperHealthChecks` and `AddBrighterInstrumentation`, no `UseOutboxSweeper`.
    - `HealthCheckService.CheckHealthAsync` gives both entries Healthy, the no-sweeper description, the remedy kind and empty `conditions`. Both gauges record 0, and nothing throws.
  - 🔁 RED mutation: change the restart factory to `GetRequiredService<OutboxSweeperState>()`. The Act captures any exception with `Record.ExceptionAsync` and the first assertion is that none was thrown; the mutation fails that assertion, not an exception in the Act. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-4A, FR-4A.1; AC-66.
  - Depends on: T9.1, T5.1.

- [x] **CHARACTERISE: T9.6 — The checks find the sweeper registered by an unchanged `UseOutboxSweeper` call**
  - **USE COMMAND**: `/test-first when the sweeper is registered with use outbox sweeper and a sweep is hung the restart check should report the hung sweep`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweeper_is_registered_with_use_outbox_sweeper_should_report_its_hung_sweep.cs`
  - Test should verify:
    - The user's `UseOutboxSweeper(...)` plus the sweeper checks and `AddBrighterInstrumentation`. A sweep is blocked 31 s.
    - The restart check is Degraded for `hung_sweep` (not "no outbox sweeper is registered"), and the running-time gauge records 31.0.
  - 🔁 RED mutation: in `UseOutboxSweeper`, register the record as transient. The sweeper and the checks then see different instances. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-4A; AC-67.
  - Depends on: T9.1, T5.2.

- [x] **CHARACTERISE: T9.7 — The investigate check reads the breaker registered in the container**
  - **USE COMMAND**: `/test-first when a circuit breaker is registered as a singleton shared with the mediator the investigate check should see its trips`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_shared_singleton_breaker_trips_a_topic_should_be_reported_by_the_investigate_check.cs`
  - Test should verify:
    - A singleton breaker given to the mediator and an active sweeper. `TripTopic` gives Degraded naming the topic.
    - With no breaker registered, no tripped-topic condition, and nothing throws.
  - 🔁 RED mutation: the investigate factory passes a null lookup. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected.
  - References: FR-6A.4; AC-44 (check half).
  - Depends on: T9.1, T3.7.

---

## Phase 10 — Sweeper signals end to end

*ADR 0086 step 6 and ADR 0085 step 6. A real `TimedOutboxSweeper` with blocking doubles, driven by the Standing obligation *Sweeper loop timing*; checks and gauges read the same record from another thread. Most tasks are `CHARACTERISE`: the behaviour already exists, and these tests pin the acceptance criteria.*

- [x] **CHARACTERISE: T10.1 — A hung sweep that ends clears the restart check**
  - **USE COMMAND**: `/test-first when a hung sweep is released and completes the restart check should report healthy again`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_hung_sweep_is_released_and_completes_should_report_healthy_again.cs`
  - Test should verify: Degraded at 31 s. Release by rule (a): request the stop, `Open()`, await the stop. Then advance the clock 31 s (rule b: the sweeper has stopped, so no sweep starts) and the next run is Healthy.
  - 🔁 RED mutation: `SweepEnded` leaves in-progress set for the completed outcome. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-5.1; AC-7.
  - Depends on: T7.2, T3.4.

- [x] **CHARACTERISE: T10.2 — A hang inside lock acquisition degrades the restart check**
  - **USE COMMAND**: `/test-first when a sweep hangs acquiring the lock past the threshold the restart check should report a hung sweep`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_hangs_acquiring_the_lock_should_report_a_hung_sweep.cs`
  - Test should verify: with `GatedDistributedLock` at 31 s, Degraded with `hung_sweep` and `remedy_kind == "restart"`.
  - 🔁 RED mutation: move `SweepStarted()` after `ObtainLockAsync`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-1, FR-9; AC-40.
  - Depends on: T7.2, T3.2.

- [x] **CHARACTERISE: T10.3 — Failed sweeps from a throwing mediator reach the failure threshold**
  - **USE COMMAND**: `/test-first when the outbox mediator throws on every sweep the investigate check should degrade at the third failure`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_outbox_mediator_throws_on_every_sweep_should_degrade_at_the_third_failure.cs`
  - Test should verify: after 2 failures Healthy; after the 3rd Degraded (3, 3, 10 s); after the 4th Degraded.
  - 🔁 RED mutation: `SweepEnded(Failed)` does not increment the count. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-2, FR-6; AC-11.
  - Depends on: T8.2, T3.4.

- [x] **CHARACTERISE: T10.4 — A lock-unavailable sweep neither counts nor resets the failures**
  - **USE COMMAND**: `/test-first when a lock unavailable sweep falls between failures the investigate check should neither count nor reset it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_lock_unavailable_sweep_falls_between_failures_should_neither_count_nor_reset.cs`
  - Test should verify: outcomes failed, failed, lock_unavailable, failed give Healthy, Healthy, Healthy, Degraded.
  - 🔁 RED mutation: lock_unavailable resets the count to 0. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-2, FR-6; AC-12.
  - Depends on: T10.3.

- [x] **CHARACTERISE: T10.5 — A completed sweep resets the failures**
  - **USE COMMAND**: `/test-first when a sweep completes after three failures the investigate check should report healthy and count from zero`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_completes_after_three_failures_should_report_healthy_and_count_from_zero.cs`
  - Test should verify: 3 failures give Degraded; completed gives Healthy; one more failure is still Healthy (count 1).
  - 🔁 RED mutation: completed leaves the count unchanged. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-6.1; AC-13.
  - Depends on: T10.3.

- [x] **CHARACTERISE: T10.6 — Failures thrown by the lock count towards the threshold**
  - **USE COMMAND**: `/test-first when obtaining the lock throws on three sweeps the investigate check should report consecutive failures`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_obtaining_the_lock_throws_on_three_sweeps_should_report_consecutive_failures.cs`
  - Test should verify: `ThrowingDistributedLock` × 3 gives Degraded for `consecutive_failed_sweeps`.
  - 🔁 RED mutation: `SweepEnded(Failed)` increments only when holds-lock is true. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-2, Definitions; AC-15.
  - Depends on: T10.3, T3.5.

- [x] **CHARACTERISE: T10.7 — A hung sweep after failures shows each condition on its own check**
  - **USE COMMAND**: `/test-first when a sweep hangs after three failures each check should report only its own condition and remedy kind`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_hangs_after_three_failures_should_report_each_condition_on_its_own_check.cs`
  - Test should verify:
    - The restart check is Degraded with the hang (31, 30, 10) and `remedy_kind restart`. The investigate check is Degraded with failures (3, 3, 10) and `remedy_kind investigate`.
    - Neither description mentions the other's condition.
  - 🔁 RED mutation: the restart check also appends the consecutive-failure condition. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-4, FR-8, FR-10.1; AC-16, AC-39 (bullets 1–2).
  - Depends on: T10.3.

- [x] **CHARACTERISE: T10.8 — Both checks complete while the sweeper's thread is blocked**
  - **USE COMMAND**: `/test-first when the sweep is blocked indefinitely both health checks should complete within one second`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweep_is_blocked_indefinitely_should_complete_both_health_checks_within_a_second.cs`
  - Test should verify: with a sweep held by `GatedOutboxProducerMediator`, `CheckHealthAsync` of each check, called from the test thread through `Task.Run(...).WaitAsync(TimeSpan.FromSeconds(1))`, completes within 1 s of wall-clock time. Under the mutation the call blocks, `WaitAsync` times out, and the assertion fails instead of the run hanging.
  - 🔁 RED mutation: `SweepStarted` takes the record's lock and `SweepEnded` releases it (the lock is then held across sweep work). Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: NFR-1; AC-17.
  - Depends on: T7.2, T8.2.

- [x] **CHARACTERISE: T10.9 — Two sweepers on a shared lock: only the holder is hung, and max equals the holder's running time**
  - **USE COMMAND**: `/test-first when two sweepers share a lock and the holder hangs only the holder should report a hung sweep`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_two_sweepers_share_a_lock_and_the_holder_hangs_should_report_only_the_holder_hung.cs`
  - Test should verify:
    - A and B share one `InMemoryLock` and one `FakeTimeProvider` (the single "advance 31 s" moves both). Start A and wait (bounded, 10 s) for its sweep to reach the mediator gate before starting B, so B cannot win the lock at t = 0. each with its own record and its own `IMeterFactory` (two factories, so the `Meter.Scope` filter can tell A's gauge from B's). A's sweep is blocked in the mediator; B's sweeps are lock-unavailable. Advance 31 s and wait for B's second lock-unavailable sweep to end (its first ran at t = 0). Observe A's check and gauge and the max of the two gauges now. Then advance 5 s more (rule c; B re-anchored its schedule, so no sweep is due) and observe B.
    - At the first observation A's check is Degraded and A's gauge records 31.0, and the max of the two gauges is 31.0. After the extra 5 s B's check is Healthy and B's gauge records 0 (a record stuck in progress by the mutation would read 5.0).
  - 🔁 RED mutation: `SweepEnded(LockUnavailable)` leaves in-progress set. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-9; AC-18.
  - Depends on: T10.2, T4.1.

- [x] **CHARACTERISE: T10.10 — Two sweepers on a shared lock: only the active sweeper reports tripped topics**
  - **USE COMMAND**: `/test-first when two sweepers share a lock only the sweeper that obtains it should report its tripped topics`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_two_sweepers_share_a_lock_should_report_tripped_topics_only_on_the_active_sweeper.cs`
  - Test should verify:
    - The sweepers share one `InMemoryLock` and one `FakeTimeProvider`. Start A and wait (bounded, 10 s) for its sweep to reach the mediator gate before starting B, so B cannot win the lock at t = 0. `orders` tripped on each sweeper's own breaker. A is held by the mediator; B is lock-unavailable. B is Healthy with gauge 0; A is Degraded naming `orders` with gauge 1.
    - After A is released by rule (a), so A does not retake the lock, and B obtains the lock and completes (advance the clock one interval so B's next sweep runs), B is Degraded naming `orders` with gauge 1.
  - 🔁 RED mutation: `IsActive = !IsStopped` (drop ladder rows 2–5). Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-6A, FR-6A.5, FR-9; AC-43.
  - Depends on: T8.6, T4.5.

- [x] **CHARACTERISE: T10.11 — Tripped topics are reported while the active sweeper's thread is blocked in the outbox**
  - **USE COMMAND**: `/test-first when an active sweeper is blocked in the outbox with a topic tripped the gauge and the investigate check should report it within one second`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_an_active_sweeper_is_blocked_in_the_outbox_should_report_its_tripped_topic.cs`
  - Test should verify: previous sweep completed; current sweep held after the lock; one topic tripped. The gauge records 1 and the check names the topic, both within 1 s.
  - 🔁 RED mutation: remove the `LockObtained()` call from `SweepAsync`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-3A, FR-6A, NFR-1; AC-34.
  - Depends on: T8.6, T4.5.

- [x] **CHARACTERISE: T10.12 — A hung active sweep with a tripped topic degrades both checks with their own remedy kinds**
  - **USE COMMAND**: `/test-first when an active sweep hangs with a topic tripped both checks should report degraded with their own remedy kinds`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_an_active_sweep_hangs_with_a_topic_tripped_should_degrade_both_checks.cs`
  - Test should verify: the restart check is Degraded (hang, restart); the investigate check is Degraded naming `orders` (investigate).
  - 🔁 RED mutation: the investigate check returns early when a sweep is in progress. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-4, FR-6A, FR-10.1; AC-42.
  - Depends on: T10.11.

- [x] **CHARACTERISE: T10.13 — An abandoned sweep keeps growing after stop and keeps the restart check Degraded**
  - **USE COMMAND**: `/test-first when stop abandons a blocked sweep the running time gauge should keep growing and the restart check stay degraded`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_stop_abandons_a_blocked_sweep_should_keep_growing_its_running_time.cs`
  - Test should verify: blocked 25 s, `StopAsync` with a cancelled token, +31 s. The gauge records 56.0 and the restart check is Degraded.
  - 🔁 RED mutation: `Stopped()` clears in-progress. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-1.1, FR-3.2; AC-45.
  - Depends on: T3.6, T7.2, T4.1.

- [x] **CHARACTERISE: T10.14 — A stopped sweeper keeps its failures and drops its tripped topics**
  - **USE COMMAND**: `/test-first when the sweeper is stopped it should keep reporting its failures and stop reporting its tripped topics`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweeper_is_stopped_should_keep_its_failures_and_drop_its_tripped_topics.cs`
  - Test should verify:
    - 3 failures then stop: the investigate check is still Degraded for failures.
    - Completed with `orders` tripped, then stop: the investigate check is Healthy, the tripped gauge records 0, and the running-time gauge records 0.
    - The abandoned 25 s sweep (`StopAsync` with a cancelled token) at 56 s: tripped gauge 0, running-time gauge 56.0, restart check Degraded.
  - 🔁 RED mutation: `Stopped()` resets the failure count. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-1.2, FR-6A.5; AC-64.
  - Depends on: T10.13, T8.6.

- [x] **CHARACTERISE: T10.15 — A hung sweep that fails counts as a failure and clears the hang**
  - **USE COMMAND**: `/test-first when a hung sweep ends as failed after two failures the restart check should be healthy and the investigate check count three`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_hung_sweep_ends_as_failed_should_count_it_as_the_third_failure.cs`
  - Test should verify: after 2 failures, a sweep blocked 31 s is released to fail by rule (a); that released sweep is the last to run before the checks. The restart check is Healthy and the investigate check is Degraded with count 3.
  - 🔁 RED mutation: `SweepEnded(Failed)` leaves in-progress set. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-5.1, FR-6; AC-51.
  - Depends on: T10.3.

- [x] **CHARACTERISE: T10.16 — A failing lock store reports failures only, never tripped topics**
  - **USE COMMAND**: `/test-first when the lock store fails on every sweep with a topic tripped the investigate check should report failures only`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_lock_store_fails_with_a_topic_tripped_should_report_failures_only.cs`
  - Test should verify: `ThrowingDistributedLock` × 3 with `orders` tripped. Degraded for failures only, `orders` not named, tripped gauge 0.
  - 🔁 RED mutation: ladder row 4 ignores "last sweep obtained the lock" (any ended sweep counts as active). Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-6A, Definitions; AC-52.
  - Depends on: T10.6, T8.6.

- [x] **CHARACTERISE: T10.17 — A hung lock holder after a lock-unavailable sweep reports its tripped topics**
  - **USE COMMAND**: `/test-first when a sweep obtains the lock after a lock unavailable sweep and hangs it should report the tripped topic throughout the hang`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_obtains_the_lock_after_a_lock_unavailable_sweep_and_hangs_should_report_the_tripped_topic.cs`
  - Test should verify: lock_unavailable, then a sweep holding the lock is blocked with `orders` tripped. The investigate check names `orders` and the gauge records 1 at several points during the hang.
  - 🔁 RED mutation: `IsActive` derived from `LastOutcome` alone (ignoring `HoldsLock`). Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-6A, Definitions; AC-53.
  - Depends on: T10.11.

- [x] **CHARACTERISE: T10.18 — A lock collision gives a one-interval gap in the tripped-topic signals**
  - **USE COMMAND**: `/test-first when an active sweeper's next sweep collides on the lock the tripped topic signals should drop for that interval and return`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_an_active_sweepers_next_sweep_collides_on_the_lock_should_drop_and_restore_tripped_topics.cs`
  - Test should verify: active with `orders`. After a lock_unavailable sweep, no tripped-topic condition and gauge 0. After the next completed sweep, Degraded naming `orders` and gauge 1.
  - 🔁 RED mutation: `SweepEnded(LockUnavailable)` keeps the previous last-sweep-obtained-lock value. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-6A.6; AC-54.
  - Depends on: T10.17.

- [x] **CHARACTERISE: T10.19 — Blocked in lock acquisition is not active, and becomes active once the lock is granted**
  - **USE COMMAND**: `/test-first when a sweep is blocked acquiring the lock after a completed sweep it should not report tripped topics until the lock is obtained`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_a_sweep_is_blocked_acquiring_the_lock_should_not_report_tripped_topics_until_obtained.cs`
  - Test should verify:
    - Previous sweep completed, `orders` tripped, current sweep in `GatedDistributedLock`. Gauge 0 and `orders` not named. After +31 s the restart check is Degraded.
    - After the lock is granted and the mediator holds the sweep, gauge 1 and `orders` named.
  - 🔁 RED mutation: ladder row 3 falls through to row 4 (acquiring the lock is treated by the last outcome). Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-6A, FR-3A, Definitions; AC-65.
  - Depends on: T10.11, T10.2.

- [x] **CHARACTERISE: T10.20 — A throwing sweeper meter does not stop sweeping or disturb the checks**
  - **USE COMMAND**: `/test-first when the sweeper meter throws on every recording the sweeps should complete and both health checks report healthy`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweeper_meter_throws_should_complete_sweeps_and_keep_both_checks_healthy.cs`
  - Test should verify:
    - `ThrowingSweeperMeter`, a mediator that completes, 3 sweeps. All complete and `CapturingLogSink` records a Warning per failed recording.
    - After the third sweep, stop the sweeper and await it, then advance the clock past the hang threshold (rule b). Both checks are Healthy. (A sweep left in progress by the mutation shows its running time only because no further sweep starts.)
  - 🔁 RED mutation: move `_state.SweepEnded(outcome)` inside the `Record(() => _meter.RecordSweep(...))` lambda after the recording call. The throw then skips it, the sweep stays in progress, and the restart check goes Degraded. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: NFR-9; AC-25.
  - Depends on: T10.3.

- [x] **CHARACTERISE: T10.21 — Reads from another thread are never torn**
  - **USE COMMAND**: `/test-first when another thread observes the gauges and checks while sweeps rotate outcomes every read should be internally consistent`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_another_thread_observes_while_sweeps_rotate_outcomes_should_never_see_a_torn_read.cs`
  - Test should verify:
    - Sweeps rotate completed, failed and lock_unavailable, with one topic tripped. The driver advances the clock half a `TimerInterval` and pauses for the observer, then advances the other half so the next sweep runs and ends (rule c). An observer thread takes 1,000 observations of both gauges, both checks and `state.Read()`.
    - No gauge value is negative and no check throws. Tripped readings are 0 or the tripped count.
    - Each snapshot: not in progress implies running time 0; in progress with the lock implies active; in progress without the lock implies not active; the count never decreases except to 0 after a completed sweep.
  - 🔁 RED mutation: in `Read`, return running time from the last start without checking the copied in-progress flag. The assertion "not in progress implies running time 0" fails during the half-interval pauses, where the mutated record reports a running time above 0. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected. This single test covers ADR 0085 step 6 and ADR 0086's extension to the checks.
  - References: NFR-2; AC-26.
  - Depends on: T9.1, T5.2.

- [x] **CHARACTERISE: T10.22 — Reading a fixed state costs the same after 10 sweeps and after 10,000**
  - **USE COMMAND**: `/test-first when the sweeper state is read after ten or ten thousand sweeps the gauge and check should allocate the same per read`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Sweeping"
  - Test file: `When_the_sweeper_state_is_read_after_many_sweeps_should_allocate_the_same_per_read.cs`
  - Test should verify:
    - One record in a fixed end state (last completed, no failures, no tripped topics), after 10 sweeps and again after 10,000, read by one check and one gauge callback synchronously on the test thread.
    - After a warm-up, the bytes per observation and per check run (10,000 each) are equal in both cases.
  - 🔁 RED mutation: the record keeps an outcome history and `Read` copies it into the snapshot. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: NFR-3; AC-48 bullet 3.
  - Depends on: T8.6, T4.5.

---

## Phase 11 — Dispatcher lifecycle snapshot

*ADR 0087 steps 2–3. Real `Dispatcher` over in-memory channels in `tests/Paramore.Brighter.Core.Tests/MessageDispatch`. Admission rules (`_stopping`, `_pendingConsumerOperations`) do not change (ADR 0083).*

- [x] **TEST + IMPLEMENT: T11.1 — A dispatcher returns a consistent lifecycle snapshot that does not change after it is read**
  - **USE COMMAND**: `/test-first when a running dispatcher is asked for its lifecycle snapshot it should copy its state consumers and expected counts`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch"
  - Test file: `When_a_running_dispatcher_is_asked_for_its_lifecycle_snapshot_should_copy_its_state_and_consumers.cs`
  - Test should verify:
    - After `Receive` with two subscriptions: `State == DS_RUNNING`, `EndRequested == false`, `ShutSubscriptions` empty, `ConsumerOperationsInFlight == 0`, `Consumers` holds the running consumers, and `ExpectedConsumers` maps each `SubscriptionName` to its count (subscriptions with the same name are summed).
    - The returned snapshot's collections do not change after a later `SetActivePerformers`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter.ServiceActivator/DispatcherLifecycleSnapshot.cs` (public `readonly record struct`). `Paramore.Brighter.ServiceActivator` targets `netstandard2.0`, where a positional record struct needs `System.Runtime.CompilerServices.IsExternalInit`, and the only polyfill (`src/Paramore.Brighter/Polyfill/CompilerServices.cs`) is internal to core. Declare it as a non-positional `readonly record struct` with get-only properties and an explicit constructor (no `init`), so no polyfill is needed. Build `src` for every target in `$(BrighterTargetFrameworks)` with warnings as errors.
    - Set `ConsumerOperationsInFlight` to `0` explicitly in this task. T11.2 wires it to the counter.
    - Add `Dispatcher.GetLifecycleSnapshot()` (`src/Paramore.Brighter.ServiceActivator/Dispatcher.cs`), copying under `_lifecycleLock`. `IDispatcher` is unchanged.
  - References: FR-12.2 (dispatcher members); ADR 0087 *The new Dispatcher members*.
  - Depends on: nothing.

- [x] **TEST + IMPLEMENT: T11.2 — The snapshot counts consumer operations in progress**
  - **USE COMMAND**: `/test-first when channel creation blocks during receive the lifecycle snapshot should report one consumer operation in flight`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch"
  - Test file: `When_channel_creation_blocks_during_receive_should_report_a_consumer_operation_in_flight.cs`
  - Test should verify:
    - With `InMemoryUnopenedConsumerChannelFactory`'s creation gate closed, `Receive` is in progress and the snapshot reports `ConsumerOperationsInFlight == 1`. After the gate opens, 0.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Copy `_pendingConsumerOperations` into the snapshot.
  - References: FR-12.2 (consumer operation in progress); ADR 0087 *ConsumerOperationsInFlight*.
  - Depends on: T11.1.

- [x] **TEST + IMPLEMENT: T11.3 — `End` is recorded in any state, and `End` before `Receive` still lets `Receive` start**
  - **USE COMMAND**: `/test-first when end is called on an awaiting dispatcher the snapshot should report end requested and a later receive should start and clear it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch"
  - Test file: `When_end_is_called_on_an_awaiting_dispatcher_should_record_it_and_allow_receive.cs`
  - Test should verify:
    - Awaiting, then `End`: `EndRequested == true`.
    - Then `Receive` does not throw, and `EndRequested == false` once running.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add a private `bool` field. `End` sets it inside the lock, before the state test (`:353-357`). `BeginConsumerOperation` clears it in the start branch (`:537-538`). `_stopping` is untouched.
  - References: FR-12.2 (dispatcher running); ADR 0087 step 2, *Why EndRequested is a new field*.
  - Depends on: T11.1.

- [x] **CHARACTERISE: T11.4 — `End` while running, after `DS_STOPPED`, and through `Dispose` all set `EndRequested`; a restart clears it**
  - **USE COMMAND**: `/test-first when end is called in running or stopped states or by dispose the snapshot should report end requested until the next start`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch"
  - Test file: `When_end_is_called_in_running_or_stopped_states_should_report_end_requested_until_restarted.cs`
  - Test should verify:
    - `End` in `DS_RUNNING` gives true. After every pump stops on its own (`DS_STOPPED`), `End` gives true. `Dispose` gives true.
    - After an awaited `End` and a new `Receive`, false.
  - 🔁 RED mutation: set the field only inside `End`'s `DS_RUNNING` branch. The `DS_STOPPED` assertion fails. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-12.2; ADR 0087 step 2 bullets; Risk *EndRequested drifts*.
  - Depends on: T11.3.

- [x] **STRUCTURAL: T11.5 — Tidy-first: move `Shut`'s state test and consumer snapshot under `_lifecycleLock`**
  - In `Dispatcher.Shut(Subscription)` (`:427-439`), take the `DS_RUNNING` test and the copy of the subscription's consumers under `_lifecycleLock`. Tell the consumers to stop **outside** the lock, as `End` does. No behaviour change.
  - The safety net is the existing `MessageDispatch` suite, including `When_consumers_are_shut_before_open_should_dispose_their_transports.cs`. Run it green before and after. Commit as `refactor:`.
  - References: ADR 0087 *Why shut subscriptions are recorded under the lifecycle lock*; ADR 0083; Risk *the new lock in Shut deadlocks*.
  - Depends on: T11.1.

- [x] **TEST + IMPLEMENT: T11.6 — A `Shut` that takes effect records the subscription; a `Shut` while awaiting does not**
  - **USE COMMAND**: `/test-first when a subscription is shut on a running dispatcher the snapshot should list it as shut and not when the dispatcher is awaiting`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch"
  - Test file: `When_a_subscription_is_shut_on_a_running_dispatcher_should_list_it_as_shut.cs`
  - Test should verify:
    - Running, then `Shut(orders)`: `ShutSubscriptions == {orders}`.
    - A second dispatcher, awaiting, then `Shut(orders)`: empty.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add a private `HashSet<SubscriptionName>`. Add the name in `Shut`'s locked block when running. Copy it in the snapshot.
  - References: FR-12.2 (shut on purpose); ADR 0087 step 3.
  - Depends on: T11.5.

- [x] **TEST + IMPLEMENT: T11.7 — Creating a consumer for a shut subscription clears it; creating none leaves it shut**
  - **USE COMMAND**: `/test-first when a shut subscription gets a consumer again the snapshot should stop listing it as shut`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch"
  - Test file: `When_a_shut_subscription_gets_a_consumer_again_should_stop_listing_it_as_shut.cs`
  - Test should verify:
    - After `Shut(orders)`: `Open(orders)` clears it; `SetActivePerformers(orders, n)` that creates consumers clears it; `SetActivePerformers` that creates none leaves it listed.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `ExecuteConsumerOperation` (`:491-495`), remove each added consumer's subscription name under the lock.
  - References: FR-12.2; ADR 0087 step 3.
  - Depends on: T11.6.

- [x] **PROJECT: T11.8 — The full dispatcher suite is green and admission is unchanged**
  - Run all of `tests/Paramore.Brighter.Core.Tests/MessageDispatch` (Reactor and Proactor) and the ADR 0083 shutdown tests. Confirm that `IDispatcher`, `DispatcherState` and the admission rules are untouched (`git diff` shows no change to those members).
  - References: ADR 0087 invariant *admission does not change*; NFR-6.
  - Depends on: T11.7.

---

## Phase 12 — Consumer take-out-of-service check

*ADR 0087 step 4. `tests/Paramore.Brighter.Extensions.Tests`, reusing `ReceiveHealthTestHost` and `InMemoryReceiveHealthChannelFactory`, extended by T12.0.*

- [x] **STRUCTURAL: T12.0 — Multi-subscription fixtures for the consumer-check tests in `Extensions.Tests`**
  - `ReceiveHealthTestHost` (`tests/Paramore.Brighter.Extensions.Tests/TestDoubles/ReceiveHealthTestHost.cs:48-63`) has one subscription, `receive-health`, and one shared `InMemoryReceiveHealthConsumer` transport with a single fixed `failures` count. Extend it, or add a new host beside it, one class per file, so that:
    - it takes several named subscriptions, each with its performer count (T13.3–T13.7: `orders` expecting 2, `payments` expecting 1 or 2);
    - each consumer gets its own channel-failure count (T12.2: counts 0, 2, 2 and 0, 3, 2);
    - a performer can be made to stop on its own, so the dispatcher can reach `DS_STOPPED` and "1 of 2 running" is possible (T13.x, T15.2, T15.4);
    - it can be built awaiting, without calling `Receive`;
    - the existing tests that use the host keep their current defaults and pass unmodified;
    - any new host file that names `BrighterServiceActivatorHealthCheck` carries `#pragma warning disable CS0618` (T15.1 lists the existing files; a file added before T15.1 still needs the pragma once T15.1 lands).
  - Why no test of its own: these are fixtures. T16.0 needs the same capabilities for `Extensions.AspNetCore.Tests` and copies them.
  - Depends on: T1.2.

- [x] **TEST + IMPLEMENT: T12.1 — With no dispatcher, the take-out-of-service check is Healthy**
  - **USE COMMAND**: `/test-first when the take out of service check has no dispatcher it should report healthy and say no dispatcher is registered`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_take_out_of_service_check_has_no_dispatcher_should_report_healthy.cs`
  - Test should verify: Healthy, the description says no dispatcher is registered, `remedy_kind == "take_out_of_service"`, empty `conditions`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter.ServiceActivator.Extensions.Diagnostics/HealthChecks/ConsumerTakeOutOfServiceHealthCheck.cs` with `const PersistentChannelFailures`, the constructor `(IDispatcher? dispatcher, int channelFailureThreshold = 3)`, using `RemedyCheckConvention` from core.
  - References: FR-12.7, FR-10.5; AC-71 (take-out-of-service half).
  - Depends on: T1.2, T6.1.

- [x] **TEST + IMPLEMENT: T12.2 — Degraded when any consumer reaches the channel-failure threshold, never Unhealthy**
  - **USE COMMAND**: `/test-first when a consumer reaches the channel failure threshold the take out of service check should report degraded naming it`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_consumer_reaches_the_channel_failure_threshold_should_report_take_out_of_service.cs`
  - Test should verify:
    - Threshold 3. Counts 0, 2, 2 give Healthy, with a description saying the condition is absent.
    - Counts 0, 3, 2 give Degraded, with a description containing the subscription, the consumer and 3. `Data` holds `persistent_channel_failures` with `threshold = 3` and `consumers = [{subscription, consumer, count = 3}]`.
    - With no consumer running, not Unhealthy.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Read every consumer in `IDispatcher.Consumers` through `IHaveAChannelFailureCount`. Reuse the existing check's phrase.
  - References: FR-12.1, FR-12.4, FR-10.5; AC-59, AC-63 (take-out-of-service half).
  - Depends on: T12.1, T12.0.

- [x] **CHARACTERISE: T12.3 — Failures count whatever the dispatcher state: shut, draining or awaiting**
  - **USE COMMAND**: `/test-first when a shut or draining consumer is at the failure threshold the take out of service check should still report degraded`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_shut_or_draining_consumer_is_at_the_failure_threshold_should_still_report_degraded.cs`
  - Test should verify: a consumer of a shut subscription at the threshold gives Degraded; a draining consumer after `End` at the threshold gives Degraded; an awaiting dispatcher with no consumers gives Healthy.
  - 🔁 RED mutation: skip consumers whose state is `ConsumerState.Shut`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-12.1; AC-59; ADR 0087 *Why the take-out-of-service check reads every consumer in every state*.
  - Depends on: T12.2.

- [x] **TEST + IMPLEMENT: T12.4 — The channel-failure threshold defaults to 3, may be 1, and must be positive**
  - **USE COMMAND**: `/test-first when the take out of service check is constructed with a threshold of one or not positive it should degrade on one failure or throw`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_take_out_of_service_threshold_is_one_or_not_positive_should_degrade_or_reject.cs`
  - Test should verify: threshold 1 with one failure gives Degraded; 0 and -1 throw `ArgumentOutOfRangeException` from the constructor; with no argument, 2 failures are Healthy and 3 are Degraded.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Validate in the constructor.
  - References: FR-12.3, FR-7.3; AC-60 (constructor half).
  - Depends on: T12.2.

---

## Phase 13 — Consumer restart check

*ADR 0087 step 5. The decision ladder rows 1–8.*

- [x] **STRUCTURAL: T13.1 — A gated channel factory for the consumer-check tests**
  - Add `InMemoryGatedReceiveHealthChannelFactory` in `tests/Paramore.Brighter.Extensions.Tests/TestDoubles/` (one class per file). It blocks channel creation until released, modelled on `InMemoryUnopenedConsumerChannelFactory`, which is internal to Core.Tests. Allow the multi-subscription host from T12.0 to be given it (T13.8 needs the gate with `orders` expecting 2).
  - Depends on: T12.0.

- [x] **TEST + IMPLEMENT: T13.2 — With no dispatcher, the restart check is Healthy**
  - **USE COMMAND**: `/test-first when the consumer restart check has no dispatcher it should report healthy and say no dispatcher is registered`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_consumer_restart_check_has_no_dispatcher_should_report_healthy.cs`
  - Test should verify: Healthy, the description says no dispatcher is registered, `remedy_kind == "restart"`, empty `conditions`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter.ServiceActivator.Extensions.Diagnostics/HealthChecks/ConsumerRestartHealthCheck.cs` with `const MissingConsumers` and the constructor `(IDispatcher? dispatcher)` (ladder row 1).
  - References: FR-12.7; AC-71 (restart half).
  - Depends on: T11.7, T6.1.

- [x] **TEST + IMPLEMENT: T13.3 — A running dispatcher missing a consumer is Degraded with the subscription's counts**
  - **USE COMMAND**: `/test-first when a running dispatcher has fewer consumers than expected the consumer restart check should report degraded with the counts`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_running_dispatcher_has_fewer_consumers_than_expected_should_report_missing_consumers.cs`
  - Test should verify:
    - `orders` expects 2. With 2 running, Healthy, and the description contains 2 (the expected count) and says all expected consumers are running.
    - With 1 running, Degraded: the description contains `orders`, 1 and 2. `Data` holds `missing_consumers` with `running_total = 1`, `expected_total = 2`, `subscriptions = [{subscription: "orders", running: 1, expected: 2}]`, and top-level `consumer_operations_in_flight = 0`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Cast to `Dispatcher` and decide from one `GetLifecycleSnapshot()`. Implement ladder rows 6 and 8 with totals across subscriptions. Reuse the existing check's phrase.
  - References: FR-12.2, FR-12.4, FR-12.5; AC-58 (first cases), AC-63 (restart half).
  - Depends on: T13.2, T12.0.

- [x] **TEST + IMPLEMENT: T13.4 — No running consumer while the dispatcher runs is Unhealthy**
  - **USE COMMAND**: `/test-first when a running dispatcher has no consumers running the consumer restart check should report unhealthy`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_running_dispatcher_has_no_consumers_running_should_report_unhealthy.cs`
  - Test should verify:
    - 0 of 2 running gives Unhealthy; the description contains `orders`, 0 and 2, and the shared AC-62 phrase "orders has 0 of 2 expected consumers" (the only literal asserted).
    - Every pump stopped on its own (`DS_STOPPED`, no `End`) gives Unhealthy.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Ladder row 7.
  - References: FR-12.2; AC-58 (Unhealthy and DS_STOPPED cases); ADR 0087 *Why the restart check keeps Unhealthy for zero consumers*.
  - Depends on: T13.3, T12.0.

- [x] **TEST + IMPLEMENT: T13.5 — A dispatcher that is not running is Healthy**
  - **USE COMMAND**: `/test-first when the dispatcher is awaiting or end has been called the consumer restart check should report healthy and say it is not running`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_dispatcher_is_not_running_should_report_healthy_and_say_so.cs`
  - Test should verify:
    - Awaiting with 0 of 2 gives Healthy, with "not running".
    - After `End` (stopping or stopped) gives Healthy.
    - Restarted with 1 of 2 gives Degraded.
    - `DS_STOPPED` by itself and then `End` gives Healthy.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Ladder rows 3 and 4.
  - References: FR-12.2 (dispatcher running); AC-58 (awaiting, End, restart cases).
  - Depends on: T13.4, T11.4, T12.0.

- [x] **TEST + IMPLEMENT: T13.6 — Subscriptions shut on purpose are left out of the totals, the description and `Data`**
  - **USE COMMAND**: `/test-first when a subscription is shut on purpose the consumer restart check should leave it out of totals description and data`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_subscription_is_shut_on_purpose_should_leave_it_out_of_missing_consumers.cs`
  - Test should verify:
    - `orders` shut with 0 running (only subscription) gives Healthy. Reopened with 1 of 2 gives Degraded.
    - `orders` shut and `payments` 1 of 2 gives Degraded, with `orders` in neither the description nor `Data`.
    - `orders` (2 expected) `Shut` while awaiting, then the dispatcher is started by `SetActivePerformers(payments, 1)`: `orders` is still expected, 0 of 2, Degraded naming `orders`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Exclude `ShutSubscriptions` and their consumers from the totals and the `subscriptions` list.
  - References: FR-12.2 (shut on purpose, running consumer); AC-58 (shut cases).
  - Depends on: T13.5, T11.7, T12.0.

- [x] **CHARACTERISE: T13.7 — Excess consumers, none expected, and a partial start by `SetActivePerformers`**
  - **USE COMMAND**: `/test-first when more consumers run than expected or none are expected the consumer restart check should report healthy`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_more_consumers_run_than_expected_should_report_healthy.cs`
  - Test should verify:
    - 3 running of 2 expected (draining after a scale-down) gives Healthy. None expected and none running gives Healthy.
    - `orders` and `payments` each expect 1, and `SetActivePerformers(orders, 1)` starts the dispatcher: 1 of 2, Degraded.
  - 🔁 RED mutation: row 6 uses `running == expected` instead of `>=`. The 3-of-2 case fails. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-12.2 (totals, excess); AC-58 (remaining cases).
  - Depends on: T13.6, T12.0.

- [x] **TEST + IMPLEMENT: T13.8 — A consumer operation in progress is Healthy**
  - **USE COMMAND**: `/test-first when a consumer operation is blocked in channel creation the consumer restart check should report healthy and say an operation is in progress`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_consumer_operation_is_blocked_in_channel_creation_should_report_healthy_in_progress.cs`
  - Test should verify:
    - `Receive` blocked in channel creation (`orders` expects 2, none registered) gives Healthy, saying an operation is in progress, with `consumer_operations_in_flight == 1`.
    - Once it completes with 1 of 2, Degraded.
    - A second operation (`SetActivePerformers` raising `orders` 2 → 3) blocked gives Healthy.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Ladder row 5, before row 6.
  - References: FR-12.2 (consumer operation in progress, transients), FR-12.5; AC-72.
  - Depends on: T13.1, T13.5, T11.2, T12.0.

- [x] **CHARACTERISE: T13.9 — The restart check requires the Brighter `Dispatcher`**
  - **USE COMMAND**: `/test-first when the consumer restart check is given a dispatcher that is not the brighter dispatcher it should throw invalid cast`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_consumer_restart_check_is_given_a_non_brighter_dispatcher_should_throw.cs`
  - Test should verify: with `SpyDispatcher`, `CheckHealthAsync` throws `InvalidCastException`.
  - 🔁 RED mutation: replace the explicit cast with `as Dispatcher` and return Healthy when it is null. The `InvalidCastException` assertion must fail. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected. T13.3 casts explicitly (ladder row 2).
  - References: FR-13.4; ADR 0087 *ConsumerRestartHealthCheck* error conditions.
  - Depends on: T13.3.

---

## Phase 14 — Consumer health-check registration

*ADR 0087 step 6.*

- [x] **TEST + IMPLEMENT: T14.1 — One opt-in call registers both consumer checks, one remedy tag each**
  - **USE COMMAND**: `/test-first when the consumer health checks are added it should register a take out of service check and a restart check each with only its remedy tag`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_consumer_health_checks_are_added_should_register_two_checks_each_with_its_remedy_tag.cs`
  - Test should verify: `brighter-consumer-take-out-of-service` with tags `{brighter-take-out-of-service}` and `brighter-consumer-restart` with tags `{brighter-restart}`, each with exactly one tag. Without the call, neither is registered.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `src/Paramore.Brighter.ServiceActivator.Extensions.Diagnostics/HealthChecks/ConsumerHealthChecksBuilderExtensions.cs` with `AddBrighterConsumerHealthChecks(this IHealthChecksBuilder, int channelFailureThreshold = 3)`. Each factory uses `GetService<IDispatcher>()` on every run. Both registrations have failure status Degraded.
    - Do not reference `BrighterServiceActivatorHealthCheck` from this file.
  - References: FR-12, FR-10.3; AC-61.
  - Depends on: T12.2, T13.3.

- [x] **TEST + IMPLEMENT: T14.2 — The call rejects a non-positive channel-failure threshold before anything is registered**
  - **USE COMMAND**: `/test-first when the consumer health checks are added with a non positive threshold the call should throw before registering anything`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_consumer_checks_are_added_with_a_non_positive_threshold_should_throw_before_registering.cs`
  - Test should verify: 0 and -1 throw `ArgumentOutOfRangeException` from the call, with no provider built and `Registrations` empty. With no override, the registered check degrades at 3.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Validate first.
  - References: FR-12.3; AC-60 (call half).
  - Depends on: T14.1.

- [x] **CHARACTERISE: T14.3 — Consumer registrations fail Degraded, including for a non-Brighter dispatcher**
  - **USE COMMAND**: `/test-first when a non brighter dispatcher is registered the health check service should report the consumer restart check as degraded`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_non_brighter_dispatcher_is_registered_should_report_the_consumer_restart_check_degraded.cs`
  - Test should verify:
    - Both registrations have `FailureStatus == Degraded`.
    - With `SpyDispatcher` registered as `IDispatcher`, `HealthCheckService.CheckHealthAsync` reports the restart entry as Degraded (with empty `Data`) and the take-out-of-service entry as Healthy.
  - 🔁 RED mutation: register the restart check with `failureStatus: null`. The service then reports Unhealthy. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-13.1, FR-13.4; AC-70 (consumer half).
  - Depends on: T14.1, T13.9.

- [x] **CHARACTERISE: T14.4 — With no dispatcher registered, both consumer checks are Healthy through the health-check service**
  - **USE COMMAND**: `/test-first when no dispatcher is registered the health check service should report both consumer checks healthy`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_no_dispatcher_is_registered_should_report_both_consumer_checks_healthy.cs`
  - Test should verify: a publisher-only container plus `AddBrighterConsumerHealthChecks`. Both entries are Healthy with the no-dispatcher description, the remedy kind and empty `conditions`, and nothing throws.
  - 🔁 RED mutation: one factory uses `GetRequiredService<IDispatcher>()`. The Act captures any exception with `Record.ExceptionAsync` and the first assertion is that none was thrown; the mutation fails that assertion, not an exception in the Act. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-12.7; AC-71.
  - Depends on: T14.1.

---

## Phase 15 — Existing consumer check: obsolete marking and agreement

*ADR 0087 steps 7–8. From T15.1 onward, every test file naming `BrighterServiceActivatorHealthCheck` carries `#pragma warning disable CS0618`.*

- [x] **TEST + IMPLEMENT: T15.1 — The existing consumer check is obsolete (warning, not error) and points to the new call**
  - **USE COMMAND**: `/test-first when the existing consumer health check is inspected it should be obsolete as a warning naming add brighter consumer health checks`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_existing_consumer_health_check_is_inspected_should_be_obsolete_naming_the_remedy_checks.cs`
  - Test should verify: `typeof(BrighterServiceActivatorHealthCheck)` carries `ObsoleteAttribute` with `IsError == false` and a `Message` containing `AddBrighterConsumerHealthChecks` (the file starts with `#pragma warning disable CS0618`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the exact ADR 0087 `[Obsolete("...")]` text to `src/.../HealthChecks/BrighterServiceActivatorHealthCheck.cs:36`. No other line changes.
    - Add `#pragma warning disable CS0618` to `tests/Paramore.Brighter.Extensions.Tests/When_a_running_consumer_repeatedly_fails_to_receive_should_report_degraded.cs` and `tests/Paramore.Brighter.Extensions.Tests/TestDoubles/ReceiveHealthTestHost.cs`, and to T1.6 if it names the type.
    - Move `samples/TaskQueue/ASBTaskQueue/GreetingsWorker/Program.cs:88` from `.AddCheck<BrighterServiceActivatorHealthCheck>("Brighter", HealthStatus.Unhealthy)` to `.AddBrighterConsumerHealthChecks()`, and remove any `using` that becomes unused.
    - Confirm with `grep` that no file under `src/` names the type except itself.
    - Run a full solution build (src, tests, samples) and confirm **no new warnings** against the branch point.
  - References: FR-10.2, NFR-6; AC-55 (attribute half), AC-27 (no new warnings); ADR 0087 *The obsolete marking*, step 8.
  - Depends on: T14.1.

- [x] **CHARACTERISE: T15.2 — The existing check reports "some consumers missing" exactly as before**
  - **USE COMMAND**: `/test-first when some expected consumers are missing the existing consumer health check should still report degraded`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_some_expected_consumers_are_missing_should_still_report_degraded_on_the_existing_check.cs`
  - Test should verify: with the existing check registered by `AddCheck` as today, 1 of 2 running gives Degraded (the #4555 state the existing tests do not cover). The existing tests for the other three states pass unmodified apart from the pragma.
  - 🔁 RED mutation: change `activeConsumers > 0 ? Degraded : Unhealthy` to always `Unhealthy` in the existing check. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: NFR-6, FR-10.2; AC-23.
  - Depends on: T15.1, T12.0.

- [ ] **CHARACTERISE: T15.3 — Registering the new checks does not change the existing check's results**
  - **USE COMMAND**: `/test-first when the sweeper or consumer remedy checks are registered the existing consumer check should report the same statuses and descriptions`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_remedy_checks_are_registered_should_leave_the_existing_consumer_check_unchanged.cs`
  - Test should verify:
    - The existing check is registered with `AddCheck` **before** `AddBrighterOutboxSweeperHealthChecks` is called, and its result is read through `HealthCheckService.CheckHealthAsync` (not by calling the check directly), so a mutation that touches the registrations is visible. The first assertion is that the existing check's entry is present in the report (`Assert.True(report.Entries.TryGetValue(...))`); the mutation fails there, and not with a `KeyNotFoundException` in the Act.
    - In each #4555 state (including "some missing", one of two consumers running), the existing check's status and description are equal with and without `AddBrighterOutboxSweeperHealthChecks` registered beside it.
    - They are equal in a separate host that registers `AddBrighterConsumerHealthChecks` instead.
  - 🔁 RED mutation: make `AddBrighterOutboxSweeperHealthChecks` clear `HealthCheckServiceOptions.Registrations` before adding its own two registrations. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Nothing expected. T1.8 gives the project its reference to `Paramore.Brighter.Outbox.Hosting`.
  - References: FR-10.2, NFR-6; AC-55 (remaining clauses).
  - Depends on: T15.1, T9.1, T1.8, T12.0.

- [ ] **CHARACTERISE: T15.4 — In the shared state, the consumer remedy checks agree with the existing check**
  - **USE COMMAND**: `/test-first when the dispatcher is running with nothing shut or in excess the worst consumer remedy status should equal the existing check status`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_dispatcher_is_running_should_agree_with_the_existing_consumer_check.cs`
  - Test should verify:
    - In each #4555 state (short burst, persistent failure, some missing, none running), running with no operation in progress, nothing shut and no excess: the worst of the two remedy statuses equals the existing check's status.
    - Each remedy description contains the values the existing description has for its condition (subscription, consumer, counts).
  - 🔁 RED mutation: the restart check returns Degraded instead of Unhealthy for zero running. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-12.4, NFR-6; AC-62.
  - Depends on: T15.1, T13.4, T12.0.

---

## Phase 16 — Convention and public-API guards

*ADR 0086 step 7, ADR 0087 step 9. `tests/Paramore.Brighter.Extensions.AspNetCore.Tests` (wired in T1.3). Both named registration calls exist by now, so the list holds both from the start.*

- [ ] **STRUCTURAL: T16.0 — Consumer and sweeper fixtures in `Extensions.AspNetCore.Tests`**
  - Test projects do not reference each other, and `tests/Paramore.Brighter.Extensions.AspNetCore.Tests/TestDoubles/` has none of the doubles Phases 16 and 17 need. Add them there, one class per file, each with an XML summary:
    - A dispatcher host with an in-memory channel factory whose consumers can be made to fail, to be missing, or to be absent, modelled on `ReceiveHealthTestHost`, `InMemoryReceiveHealthChannelFactory` and `InMemoryReceiveHealthConsumer` in `tests/Paramore.Brighter.Extensions.Tests/TestDoubles/` as T12.0 extends them (several named subscriptions, per-consumer failure counts, a performer that stops on its own, built awaiting). Any file that names `BrighterServiceActivatorHealthCheck` carries `#pragma warning disable CS0618`. It registers a real `Dispatcher`, so the existing consumer check is activated as today.
    - A helper that registers `OutboxSweeperState` directly with `AddSingleton` (not through `UseOutboxSweeper`, so no hosted sweeper starts and writes the same record) also registers `new TimedOutboxSweeperOptions { TimerInterval = 10 }` (a registered record alone leaves the checks on the default of 5 s, and T17.5 asserts 10), and drives the record through its public transitions with a `FakeTimeProvider` (hung sweep, consecutive failures, active sweeper), with a real `InMemoryOutboxCircuitBreaker` for tripped topics. The scenario values it produces are a 31 s hung sweep and 3 consecutive failures, against a 30 s hang threshold and N = 3. No blocking doubles are needed.
  - Why no test of its own: these are fixtures, consumed by T16.2 and T17.2–T17.7.
  - Depends on: T1.3, T2.5, T12.0.

- [ ] **CHARACTERISE: T16.1 — Every public Brighter `IHealthCheck` is exempt or registered by a named call with exactly one remedy tag**
  - **USE COMMAND**: `/test-first when reflecting over brighter assemblies every public health check should be exempt or registered by a named call with only its remedy tag`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_reflecting_over_brighter_assemblies_every_health_check_should_follow_the_remedy_convention.cs`
  - Test should verify:
    - Call `AddBrighterOutboxSweeperHealthChecks` and `AddBrighterConsumerHealthChecks` on a `ServiceCollection`. Read `HealthCheckServiceOptions.Registrations` and invoke each factory to learn the check type.
    - (a) The public `IHealthCheck` types in every `Paramore.Brighter*` assembly in the output folder, less the exemption list `{BrighterServiceActivatorHealthCheck}`, equal the found types.
    - (b) Each registration has exactly one tag, its remedy tag.
    - The file carries the CS0618 pragma.
  - 🔁 RED mutation: add a temporary public `IHealthCheck` class to `Paramore.Brighter.Outbox.Hosting`. Part (a) fails. Separately, add a second tag to one registration; part (b) fails. Revert both.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-10.3, FR-10.4; AC-49 (a), (b); ADR 0086 step 7.
  - Depends on: T1.3, T9.1, T14.1, T15.1.

- [ ] **CHARACTERISE: T16.2 — Each remedy check's `Data` names the remedy kind its tag identifies, Healthy and Degraded**
  - **USE COMMAND**: `/test-first when each remedy check runs healthy and degraded its data should name the remedy kind its tag identifies`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_each_remedy_check_runs_should_name_the_remedy_kind_its_tag_identifies.cs`
  - Test should verify:
    - For each of the four checks, one Healthy and one Degraded scenario: `Data["remedy_kind"] == RemedyCheckConvention.Name(kind for its tag)` and `Data` holds a `conditions` entry. AC-70's service-built results are excluded.
  - 🔁 RED mutation: `OutboxSweeperRestartHealthCheck` writes `remedy_kind = "investigate"`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-10.5; AC-49 (c).
  - Depends on: T16.1, T16.0.

- [ ] **CHARACTERISE: T16.3 — The public API of the named types is a superset of its branch-point baseline**
  - **USE COMMAND**: `/test-first when comparing public members with the baseline every existing member should still exist and the circuit breaker interface should be unchanged`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_comparing_public_members_with_the_baseline_should_keep_every_existing_member.cs`
  - Test should verify:
    - A hard-coded baseline of public member signatures of `IAmABrighterSweeperMeter`, `IAmAnOutboxCircuitBreaker`, `TimedOutboxSweeper`, `TimedOutboxSweeperOptions`, `BrighterServiceActivatorHealthCheck`, `Dispatcher` and `IDispatcher`, with parameter names and types, taken from the **branch point** (`git merge-base HEAD master`), not from the current tree.
    - Every baseline member exists with a source-compatible signature: the same name, parameter names, parameter types and return type, or the same plus trailing optional parameters on a constructor or non-virtual class method (NFR-6; binary compatibility is not required). A renamed parameter is a failure, because it breaks named-argument calls. `IAmAnOutboxCircuitBreaker` declares exactly the baseline. The only new attribute on `BrighterServiceActivatorHealthCheck` is `Obsolete`. The file carries the CS0618 pragma.
  - 🔁 RED mutation: add a member to `IAmAnOutboxCircuitBreaker`. Separately, change the type of an existing parameter of a `TimedOutboxSweeper` constructor. A trailing optional parameter such as `OutboxSweeperState? state = null` on a `TimedOutboxSweeper` constructor must not fail the test. Separately, rename a parameter of an existing member; the test must fail. A trailing optional parameter on an `IDispatcher` member must fail the test. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: NFR-6; AC-5, AC-36; ADR 0087 *AC-5*.
  - Depends on: T1.3, T15.1.

---

## Phase 17 — Endpoint routing

*ADR 0086 step 8, ADR 0087 step 10. ASP.NET Core test host in `tests/Paramore.Brighter.Extensions.AspNetCore.Tests`, endpoints mapped exactly as the docs show, using only `Predicate` and `ResultStatusCodes`.*

- [ ] **STRUCTURAL: T17.1 — The documented `System.Text.Json` response writer as a test fixture**
  - Add `BrighterHealthReportJsonWriter` in `tests/Paramore.Brighter.Extensions.AspNetCore.Tests/TestDoubles/`. It writes the overall status and, per entry, name, status, tags, description and `Data`. **Its text is the text T18.7 puts on the docs page**, so the docs and the test cannot drift. No Brighter production code ships a writer.
  - Add a helper that maps `/health/live` (restart tag; Degraded and Unhealthy → 503), `/health/ready` (take-out-of-service tag; Degraded and Unhealthy → 503) and `/health/operator` (investigate tag; the writer).
  - Depends on: T1.3.

- [ ] **CHARACTERISE: T17.2 — Sweeper conditions route to liveness or the operator endpoint only**
  - **USE COMMAND**: `/test-first when sweeper conditions apply the liveness readiness and operator endpoints should return the documented status codes`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_sweeper_conditions_apply_should_route_them_to_the_documented_endpoints.cs`
  - Test should verify:
    - A hung sweep gives liveness 503 and readiness 200.
    - Only failures give liveness and readiness 200. Only a tripped topic on an active sweeper gives liveness and readiness 200.
    - The restart check is selected by neither readiness nor operator. The investigate check is selected by neither liveness nor readiness.
  - 🔁 RED mutation: register the restart check with `brighter-investigate`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-10.1, FR-10.3, NFR-7; AC-21.
  - Depends on: T17.1, T9.1, T16.0.

- [ ] **CHARACTERISE: T17.3 — A tripped topic shows as Degraded investigate in the report and fails no probe**
  - **USE COMMAND**: `/test-first when only a tripped topic applies the probes should pass and the report show degraded investigate`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_only_a_tripped_topic_applies_should_pass_the_probes_and_report_investigate.cs`
  - Test should verify: liveness and readiness return 200. The `HealthReport` has the investigate entry Degraded with `remedy_kind investigate`.
  - 🔁 RED mutation: tag the investigate check `brighter-take-out-of-service`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-10.1, FR-10.3; AC-35.
  - Depends on: T17.2, T16.0.

- [ ] **CHARACTERISE: T17.4 — Conditions are visible in the `HealthReport` and through a publisher, without a writer**
  - **USE COMMAND**: `/test-first when the health check service runs with no predicate the report and a publisher should carry each check status description and data`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_health_check_service_runs_with_no_predicate_should_carry_each_check_to_a_publisher.cs`
  - Test should verify: for the AC-21 scenarios, `CheckHealthAsync()` with no predicate has an entry per check with status, description and `Data`, and a registered `IHealthCheckPublisher` (a recording double) receives the same entries.
  - 🔁 RED mutation: the investigate check returns Degraded without `Data`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-10.3, NFR-7; AC-50.
  - Depends on: T17.2, T16.0.

- [ ] **CHARACTERISE: T17.5 — The documented JSON writer produces valid JSON with every check's `Data`**
  - **USE COMMAND**: `/test-first when the operator endpoint uses the documented writer the body should be valid json with each check name status tags description and data`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_operator_endpoint_uses_the_documented_writer_should_write_valid_json_with_data.cs`
  - Test should verify:
    - For the AC-21 scenarios, the operator body parses as JSON and holds the overall status and, per selected check, name, status, tags, description, `remedy_kind`, `conditions` and values (e.g. `timer_interval_seconds` is the number 10).
    - Liveness and readiness still use the default writer and return the AC-21 codes.
  - 🔁 RED mutation: store `timer_interval_seconds` as a `TimeSpan`. It then serializes as the string `"00:00:10"` and the numeric assertion fails. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-10.3, FR-10.5, FR-11; AC-57.
  - Depends on: T17.2, T16.0.

- [ ] **CHARACTERISE: T17.6 — An existing endpoint with the exclusion predicate is unchanged; one without a predicate selects the restart check**
  - **USE COMMAND**: `/test-first when sweeper checks are added beside an existing endpoint with the exclusion predicate its status and body should not change`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_sweeper_checks_are_added_beside_an_existing_endpoint_should_leave_it_unchanged.cs`
  - Test should verify:
    - The existing consumer check registered as today, with a running dispatcher whose expected consumers are all running and no channel failures, so it is Healthy (the test asserts that precondition; otherwise the default writer's body and Degraded's 200 hide the mutation), and an endpoint with the documented exclusion predicate and the default writer. The status code and body are byte-equal with and without the sweeper checks registered while a sweep is hung and a topic is tripped.
    - The same endpoint mapped without a predicate does select the restart check (the documented hazard). The file carries the CS0618 pragma.
  - 🔁 RED mutation: register the restart check with no tags. The exclusion predicate then lets it through. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-11 (migration step 1), NFR-6; AC-56.
  - Depends on: T17.2, T15.1, T16.0.

- [ ] **CHARACTERISE: T17.7 — Consumer conditions route to readiness or liveness only**
  - **USE COMMAND**: `/test-first when consumer conditions apply the liveness and readiness endpoints should return the documented status codes`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_consumer_conditions_apply_should_route_them_to_the_documented_endpoints.cs`
  - Test should verify:
    - A consumer at the threshold gives readiness 503 and liveness 200.
    - 1 of 2 missing gives liveness 503 and readiness 200.
    - None running while the dispatcher runs gives liveness 503 and readiness 200. Dispatcher not started gives liveness 200.
    - The take-out-of-service check is not selected by liveness, and the restart check is not selected by readiness.
  - 🔁 RED mutation: tag the consumer restart check `brighter-take-out-of-service`. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: nothing expected.
  - References: FR-10.3, FR-12, NFR-6; AC-22.
  - Depends on: T17.1, T14.1, T16.0.

---

## Phase 18 — Documentation, release notes and final verification

*ADR 0085 step 7, ADR 0086 step 9, ADR 0087 step 11. BrighterCommand/Docs changes go in a PR to that repo, linked from the Brighter PR.*

- [ ] **DOC: T18.1 — Release notes: the two sweeper gauges**
  - `release_notes.md` under `## Master`: `paramore.brighter.outbox_sweeper.sweep.running_time` (s) and `paramore.brighter.outbox_sweeper.tripped_topics` (`{topic}`), no attributes, registered by `AddBrighterInstrumentation`. State the recommended alerts. Name the additive public types and parameters (`OutboxSweeperState`, `OutboxSweeperSnapshot`, the optional constructor parameters on `SweeperMeter` and `TimedOutboxSweeper`).
  - References: FR-11; AC-27 bullet 1; ADR 0085 step 7.
  - Depends on: T5.3.

- [ ] **DOC: T18.2 — Release notes: sweeper remedy checks and the remedy-kind convention**
  - `AddBrighterOutboxSweeperHealthChecks(hangThreshold, failureThreshold)`, defaults (3 × `TimerInterval`, 3), validation, the four conditions, tags `brighter-restart`, `brighter-take-out-of-service` and `brighter-investigate`, failure status Degraded, `Data` keys, and the new `Microsoft.Extensions.Diagnostics.HealthChecks` dependency of `Paramore.Brighter.Outbox.Hosting`.
  - References: FR-11; AC-27; ADR 0086 step 9.
  - Depends on: T9.4.

- [ ] **DOC: T18.3 — Release notes: consumer remedy checks, the obsolete marking, and the change to #4555's guidance**
  - `AddBrighterConsumerHealthChecks(channelFailureThreshold = 3)`, its two conditions and remedy kinds, `Dispatcher.GetLifecycleSnapshot` / `DispatcherLifecycleSnapshot`, and the new package dependency.
  - `[Obsolete]` on `BrighterServiceActivatorHealthCheck`, **including that builds treating warnings as errors fail with CS0618** until they suppress it or migrate, and retirement in V11.
  - "Map Degraded to 503 on readiness" now applies to the take-out-of-service tag only.
  - References: FR-11, NFR-6; AC-27; ADR 0087 step 11.
  - Depends on: T15.1.

- [ ] **DOC: T18.4 — Telemetry page (BrighterCommand/Docs): both gauges and their alerts**
  - Units. `max(paramore.brighter.outbox_sweeper.sweep.running_time) > 3 × TimerInterval`, and why it is preferred to alerting on the absence of `completed` sweeps. `tripped_topics > 0` at any point in a 3 × `TimerInterval` window (e.g. `max_over_time`). The gauge reads 0 on an instance that is not an active sweeper, and during lock acquisition.
  - References: FR-11; AC-27 bullet 2; ADR 0085 step 7.
  - Depends on: T18.1.

- [ ] **DOC: T18.5 — Health-check page: the sweeper checks**
  - The four conditions and their boundaries; opt-in and threshold validation; hang-threshold sizing (`BatchSize`, send latency, the `sweep.duration` histogram); the duplicate-send risk after a liveness restart; the `InMemoryOutbox` loss warning with the advice to route restart to monitoring.
  - One sweeper per service provider (more is undefined, FR-4A.2). The breaker must be a singleton shared with the mediator (FR-6A.4), and a blocking `TrippedTopics` stalls the signals (FR-13.2). The tripped-topic gaps during lock acquisition and after a collision (FR-6A.6).
  - An unexpected exception in a check reports Degraded, so on a liveness endpoint it restarts the process. A factory that throws is a misconfiguration that fails the whole endpoint.
  - References: FR-11; AC-27 bullets 3–4; ADR 0086 step 9.
  - Depends on: T18.2.

- [ ] **DOC: T18.6 — Health-check page: the remedy-kind convention, the routing example, and the consumer checks**
  - The convention (one kind per registration, one tag, failure status Degraded, `Data` shape). The existing check is the single documented exception: untagged and obsolete.
  - A liveness / readiness / operator example with Degraded **and Unhealthy** mapped to 503 on liveness and readiness, noting that the default `ResultStatusCodes` maps Degraded to 200. Suggested URLs as guidance only.
  - The consumer checks' conditions, remedy kinds, threshold default and option. When the restart check is Healthy (not running, operation in progress, shut on purpose, excess). That a hung consumer operation is not detected, with an alert on `consumer_operations_in_flight`. Probe period and failure-threshold advice for scale-up and reopen. The coexistence rule (FR-10.2.1). Replace #4555's readiness-only advice.
  - References: FR-10.2.1, FR-10.4, FR-11, FR-12.6; AC-27; ADR 0087 step 11.
  - Depends on: T18.3, T18.5.

- [ ] **DOC: T18.7 — Health-check page: the six migration steps and the JSON response writer**
  - The six FR-11 migration steps in order, with the exclusion predicate (step 1) and the silent-drop hazard (step 5). No step points a probe at an endpoint that loses a condition the probe covered before, unless another probe covers it after the same step.
  - The complete `System.Text.Json` writer, **copied verbatim from T17.1's fixture**, used on the operator endpoint only. Mention the community `UIResponseWriter` as an alternative that adds a package.
  - References: FR-10.3, FR-11; AC-27, AC-56, AC-57.
  - Depends on: T18.6, T17.5, T17.6.

- [ ] **PROJECT: T18.8 — Final verification before merge**
  - Full solution build (src, tests, samples) on every target framework: `src` builds with warnings as errors, and there are **no new warnings anywhere** compared with the branch point (NFR-6, AC-27).
  - Run the full suites of `Paramore.Brighter.Core.Tests`, `Paramore.Brighter.Extensions.Tests` and `Paramore.Brighter.Extensions.AspNetCore.Tests`.
  - Confirm `git grep BrighterServiceActivatorHealthCheck -- src samples` finds only the type itself.
  - Re-run T1.7's package-graph diff.
  - Confirm ADRs 0085, 0086 and 0087 are Accepted and reference both ADR 0081 documents (AC-27 last bullet).
  - Depends on: every earlier task.

---

## Dependency summary

- **Critical path (sweeper)**: T1.4 → Phase 2 (T2.1–T2.5) → Phase 3 (T3.2–T3.4, T3.6, T3.7) → Phase 4 (T4.1, T4.4, T4.5, T4.7) → Phase 5 (T5.1–T5.3).
  - Phase 7 and T8.1–T8.11 and T8.13 can run beside Phases 3–5 once T1.1, T2.5, T3.1 and T6.1 are done. T8.12 also needs T3.4. In Phase 9, T9.5 needs T5.1, T9.6 needs T5.2 and T9.7 needs T3.7, so Phase 9 finishes behind the sweeper critical path.
  - Phase 10 needs Phases 4, 8 and 9.
- **Critical path (consumer)**: Phase 11 (independent of the sweeper phases) → Phases 12 and 13 → Phase 14 → T15.1.
- **Shared**: Phase 6 (convention) is needed by Phases 7, 8, 12 and 13. T1.1 is needed by Phase 7; T1.2 by Phase 12; T1.3 by Phases 16 and 17; T1.8 by T15.3; T16.0 by T16.2 and T17.2–T17.7; T12.0 by T12.2, T13.1, T13.3–T13.8, T15.2–T15.4 and T16.0.
- **Ordering hazards**:
  - T15.1 (obsolete) must land before T16.1, T16.3 and T17.6 so those are written with the CS0618 pragma.
  - T15.1 needs T14.1, because the sample moves to the new call.
  - T11.5 (tidy) comes before T11.6.
  - T17.1's writer is the single source for T18.7.

## Coverage cross-reference

### Functional requirements

| Requirement | Tasks |
| --- | --- |
| FR-1 | T2.2, T3.2, T3.3, T4.2, T10.2 |
| FR-1.1 | T2.5, T3.6, T10.13 |
| FR-1.2 | T2.5, T4.5, T8.6, T10.14 |
| FR-2 | T2.3, T2.4, T3.4, T3.5, T10.3, T10.4, T10.6 |
| FR-3, FR-3.1 | T4.1, T4.2 |
| FR-3.2 | T2.2, T4.3, T10.13 |
| FR-3.3 | T4.1, T5.1, T5.2, T5.4 |
| FR-3.4 | T4.1, T4.8 |
| FR-3A | T4.4, T4.5, T4.8, T5.3, T5.4, T10.11, T10.19 |
| FR-4 | T9.1, T10.7, T10.12 |
| FR-4A | T3.7, T9.5, T9.6 |
| FR-4A.1 | T4.1, T4.4, T5.1, T7.1, T8.1, T9.5 |
| FR-4A.2 | T3.7 (one record when called twice), T18.5 (docs). No AC by design. |
| FR-5 | T7.2, T7.3 |
| FR-5.1 | T10.1, T10.15 |
| FR-6 | T8.2, T10.3, T10.4 |
| FR-6.1 | T10.5 |
| FR-6A | T8.4, T8.6, T10.10, T10.16, T10.17, T10.19 |
| FR-6A.1 | T8.10, T8.11 |
| FR-6A.2 | T8.4, T8.11 |
| FR-6A.3 | T4.4, T8.4, T8.12 |
| FR-6A.4 | T5.3, T8.5, T9.7, T18.5 (the non-singleton limitation is docs only, by design) |
| FR-6A.5 | T4.5, T8.6, T10.10, T10.14 |
| FR-6A.6 | T4.5, T8.6, T10.18, T18.4, T18.5 |
| FR-7.1 | T7.2, T7.3, T7.4, T9.2 |
| FR-7.2 | T8.3, T9.3 |
| FR-7.3 | T7.4, T8.3, T12.4 |
| FR-8 | T7.2, T7.3, T8.2, T8.4, T8.7, T8.8, T10.7 |
| FR-9 | T10.9, T10.10, T10.2 |
| FR-10, FR-10.1 | T7.2, T8.2, T10.7, T10.12, T17.2, T17.3 |
| FR-10.2 | T15.1, T15.2, T15.3 |
| FR-10.2.1 | T18.6 (docs only, by design) |
| FR-10.3 | T6.1, T9.1, T14.1, T16.1, T17.1–T17.7 |
| FR-10.4 | T6.1, T16.1, T18.6 |
| FR-10.5 | T6.1, T7.1, T7.2, T8.2, T8.4, T8.7, T12.2, T13.3, T16.2, T17.5 |
| FR-11 | T18.1–T18.7, T17.1, T17.6 |
| FR-12 | T14.1, T17.7 |
| FR-12.1 | T12.2, T12.3 |
| FR-12.2 and its Dispatcher members | T11.1–T11.7, T13.3–T13.8 |
| FR-12.3 | T12.4, T14.2 |
| FR-12.4 | T12.2, T13.3, T15.4 |
| FR-12.5 | T13.3, T13.8 |
| FR-12.6 | T18.6 |
| FR-12.7 | T12.1, T13.2, T14.4 |
| FR-13.1 | T9.4, T14.3, T18.5 |
| FR-13.2 | T4.7, T8.9, T18.5 |
| FR-13.3 | T4.7 |
| FR-13.4 | T13.9, T14.3 |

### Non-functional requirements

| Requirement | Tasks |
| --- | --- |
| NFR-1 | T4.1, T10.8, T10.11 |
| NFR-2 | T10.21 |
| NFR-3 | T2.6, T3.8, T10.22 |
| NFR-4 | T2.2, T3.7, and the standing obligation |
| NFR-5 | T1.1, T1.2, T1.5–T1.7 |
| NFR-6 | T5.5, T8.13, T11.8, T15.1–T15.4, T16.3, T17.6, T18.8 |
| NFR-7 | T17.2, T17.4 |
| NFR-8 | T1.4, T4.8 |
| NFR-9 | T4.7, T10.20 |

### Acceptance criteria

| AC | Tasks | AC | Tasks | AC | Tasks |
| --- | --- | --- | --- | --- | --- |
| AC-1 | T4.1 | AC-25 | T10.20 | AC-49 | T16.1, T16.2, and the remedy-kind standing obligation |
| AC-2 | T4.2 | AC-26 | T10.21 | AC-50 | T17.4 |
| AC-3 | T4.3 | AC-27 | T18.1–T18.8 | AC-51 | T10.15 |
| AC-4 | T5.4 | AC-28 | T8.4 | AC-52 | T10.16 |
| AC-5 | T16.3 | AC-29 | T8.10 | AC-53 | T10.17 |
| AC-6 | T7.2 | AC-30 | T8.5 | AC-54 | T10.18 |
| AC-7 | T10.1 | AC-31 | T8.12 | AC-55 | T15.1, T15.3 |
| AC-8 | T7.3 | AC-32 | T8.8 | AC-56 | T17.6 |
| AC-9 | T7.4, T9.2 | AC-33 | T4.4, T5.3, T5.4 | AC-57 | T17.5 |
| AC-10 | T7.3 | AC-34 | T10.11 | AC-58 | T13.3–T13.7 |
| AC-11 | T8.2, T10.3 | AC-35 | T17.3 | AC-59 | T12.2, T12.3 |
| AC-12 | T10.4 | AC-36 | T8.13, T16.3 | AC-60 | T12.4, T14.2 |
| AC-13 | T10.5 | AC-37 | T9.1 | AC-61 | T14.1 |
| AC-14 | T8.3, T9.3 | AC-38 | T8.7 | AC-62 | T15.4 |
| AC-15 | T10.6 | AC-39 | T7.2, T8.2, T8.4, T8.7 | AC-63 | T12.2, T13.3 |
| AC-16 | T10.7 | AC-40 | T10.2 | AC-64 | T10.14 |
| AC-17 | T10.8 | AC-41 | T8.11 | AC-65 | T10.19 |
| AC-18 | T10.9 | AC-42 | T10.12 | AC-66 | T9.5 (also T4.1, T4.4, T5.1, T7.1, T8.1) |
| AC-19 | T7.2, T8.2 | AC-43 | T10.10 | AC-67 | T9.6 (also T5.2) |
| AC-20 | standing obligation in every Phase 7–10 and 17 sweeper-check test | AC-44 | T5.3, T9.7 | AC-68 | T4.5, T4.7, T8.6, T8.9 |
| AC-21 | T17.2 | AC-45 | T10.13 | AC-69 | T4.7 (the circuit-breaker alternative) |
| AC-22 | T17.7 | AC-46 | T5.5 | AC-70 | T9.4, T14.3 |
| AC-23 | T15.2 | AC-47 | T4.8 | AC-71 | T12.1, T13.2, T14.4 |
| AC-24 | T1.5, T1.6, T1.7 | AC-48 | T2.6, T3.8, T10.22 | AC-72 | T13.8 |

### ADR decisions

**ADR 0085**

| Decision | Tasks |
| --- | --- |
| Lock-guarded `OutboxSweeperState` and `OutboxSweeperSnapshot` in core | T2.1–T2.6 |
| Four transitions at fixed points in the sweeper | T3.2–T3.6 |
| The active-sweeper ladder lives only in the record | T2.4, T2.5 |
| `UseOutboxSweeper` registers the singleton with the container's clock | T3.7 |
| `SweeperMeter` owns both attribute-free gauges | T4.1, T4.4, T4.8 |
| The breaker is read on each observation, only while active | T4.4, T4.5 |
| Failures become 0 plus a Warning | T4.7 |
| `SweeperMeter` is created with the meter provider; the factory passes the record and the breaker lookup | T5.1–T5.3 |
| Names in `BrighterSemanticConventions` | T1.4 |
| No reader interface (AC-69 alternative) | T4.7 |
| Concurrency and read cost | T10.21, T10.22 |
| Docs | T18.1, T18.4 |

**ADR 0086**

| Decision | Tasks |
| --- | --- |
| Checks live in Outbox.Hosting with one new package | T1.1, T1.5, T1.7 |
| `RemedyKind` and `RemedyCheckConvention` in core | T6.1 |
| Restart check | T7.1–T7.4 |
| Investigate check, incl. the unreadable breaker logged at Error and the null-options default (T7.2, T8.2) | T8.1–T8.13 |
| The registration call, names, tags, per-run factories using `GetService`, failure status Degraded | T9.1–T9.7 |
| Validation in both the call and the constructor | T7.4, T8.3, T9.2, T9.3 |
| Nested `Data` shape | T7.2, T8.7, T16.2 |
| `brighter-` tag prefix | T6.1 |
| Results across checks | T10.7, T10.12 |
| AC-49 test plus the AspNetCore.Tests references | T1.3, T16.1, T16.2 |
| Endpoint tests | T17.2–T17.6 |
| Docs | T18.2, T18.5–T18.7 |

**ADR 0087**

| Decision | Tasks |
| --- | --- |
| Package reference | T1.2, T1.6 |
| `EndRequested` | T11.3, T11.4 |
| `ShutSubscriptions`, with `Shut` under the lock | T11.5–T11.7 |
| `GetLifecycleSnapshot` and `DispatcherLifecycleSnapshot` | T11.1, T11.2 |
| Take-out-of-service check | T12.1–T12.4 |
| Restart check ladder rows 1–8 | T13.2–T13.9 |
| `consumer_operations_in_flight` | T13.3, T13.8 |
| Registration call | T14.1–T14.4 |
| Agreement | T15.4 |
| Obsolete marking, pragmas and sample | T15.1 |
| AC-5 baseline test | T16.3 |
| Coexistence documented, not enforced | T18.6 |
| Endpoint tests | T17.7 |
| Docs and migration | T18.3, T18.6, T18.7 |

### Uncovered by tests (by design, docs only)

These have no AC in the requirements:
- FR-4A.2: more than one sweeper is undefined.
- FR-6A.4: a breaker registered with a non-singleton lifetime.
- FR-10.2.1 / FR-12.6: coexistence of the existing and remedy consumer checks.
- FR-13.2: a `TrippedTopics` that blocks.

No FR or ADR decision is otherwise uncovered.

### Scope-creep check

No task traces to neither a requirement nor an ADR decision. Borderline items to review:
- **The null-options default** (T7.2, T8.2): it traces to an ADR 0086 sentence, not to an AC. It is an ADR decision, so it is not scope creep.
- **T10.20** advances the clock past the hang threshold, a small addition to AC-25 so that its named mutation can be seen.
- **T2.2** clamps running time at 0 with no dedicated test. ADR 0085 states the invariant. The clamp is defensive and untested, because a `FakeTimeProvider` never goes backwards.
- **T15.2** adds a test of the existing check for the "some consumers missing" state, which is required by AC-23 because the existing tests do not cover it.
- **T1.7** (package-graph diff) and **T18.8** are verification only.


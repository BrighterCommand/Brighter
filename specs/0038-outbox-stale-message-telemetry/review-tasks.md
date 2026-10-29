# Review: tasks — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-29
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> This phase is already approved. Findings are informational — consider whether any warrant re-opening the phase.

> Review history for the task breakdown of this spec. Pass 1 PASS (0 at or above 60); pass 2 PASS (0); pass 3 PASS
> (0). Pass 4 NEEDS WORK (1: T6.4 would be green before its implementation). Pass 5 NEEDS WORK (1: the mutation
> named by T12.1 does not compile). Pass 6 NEEDS WORK (4: T10.5 and T10.6, T8.23, T11.4, and T9.11 and T9.12).
> Pass 7 NEEDS WORK (3: T10.5, T11.5 and T8.23). Pass 8 NEEDS WORK (2, both on T11.5). Pass 9 NEEDS WORK (1: T10.1
> read the stop token synchronously, before the loop could observe the stop). Pass 10 PASS (3 findings, 0 at or
> above 60); its findings were then fixed, and T11.2 and T11.3 were reworked so that their mutation fails the test
> without hanging it. Pass 11 PASS (7 findings, 0 at or above 60). Pass 12, after T10.12 and T10.13 were added,
> NEEDS WORK (4 findings, 1 at or above 60: the gauge check of T10.13 passed without the fix). Pass 13, after the
> findings of pass 12 were fixed, PASS (3 findings, 0 at or above 60). Pass 14, after findings 1 and 2 of pass 13
> were fixed and finding 3 was left as it was, PASS (2 findings, 0 at or above 60). Pass 15, after finding 1 of
> pass 14 was fixed and finding 2 was left as it was, PASS (3 findings, 0 at or above 60). Pass 16, after T6.12
> was added, PASS (3 findings, 0 at or above 60), recorded below.

## Findings

### 1. T6.12 leaves the T15.4 exception out of date, and no task says to update it (Score: 55)

T6.12 exists to remove the exception that T15.4 records, and it cites that record as its reason. T15.4 is ticked, and its record is written in the present tense, so once T6.12 is ticked the record is false. The record also says more than the clock: it says the tests "assert only what start set on the readings", which T6.12 leaves alone. So it is not clear whether the whole bullet goes or only the part about the clock.

**Evidence**:
- tasks.md:1871, the T15.4 bullet on T6.7 and T6.11.
- tasks.md:622: "T15.4 lists these tests as the exception to AC-25."
- T6.12 names no output that touches T15.4.

**Recommendation**: Add an output to T6.12 that updates the T15.4 bullet: the tests run on a `FakeTimeProvider` (T6.12) and still read only `OutboxStaleMessageReadings.IsStarted`, `OutboxType` and `MeasuresTopics`, not meter values. Or say that T15.4 is checked again for these two files after T6.12.

---

### 2. The verification does not show that the fake clock reaches the monitor, and names no mutation for T6.11 (Score: 45)

The aim of T6.12 is that the monitors these tests start run on the fake clock and never count or log after the test. The verification runs the T6.7 mutation and the suite. Neither would notice a `FakeTimeProvider` registered where the monitor never gets it, since the tests were green before the change and stay green after it. `BuildMonitor` changes for both T6.11 facts, but no T6.11 mutation is named, so nothing shows those facts still fail when they should.

**Evidence**: tasks.md:623.

**Recommendation**: Add a T6.11 mutation, for example skipping `_readings.Start(...)` (`TimedOutboxStaleMessageMonitor.cs:117`), which should fail both facts. Add one check that the clock reaches the monitor, by reading the diff or with a probe: set `MeasurementInterval` to 1 ms in the changed tests for a while and confirm that the outbox double records no count after a short wait on the real clock.

---

### 3. T6.12 names its own line baseline, and the header does not allow for it (Score: 20)

The header fixes one baseline for line numbers. T6.12 names its own, and no other task does. The lines are right, because nothing under `src` or `tests` changed between 093b69aae and HEAD, but the header at :22 does not say that a task may do this.

**Evidence**: tasks.md:22, "Lines are cited at HEAD 942cb8f56."; tasks.md:620, "Lines in this task are cited at HEAD 093b69aae."

**Recommendation**: Optional. Add half a sentence to :22 saying that a task added later can name its own baseline.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0

## Checked in T6.12, with no finding

Both files exist under `tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/` and create their `ServiceCollection` at the lines T6.12 cites (:63, and :80 in `BuildMonitor`), with no `TimeProvider` and no `Microsoft.Extensions.Time.Testing` using. The pattern T6.12 copies is at `When_the_monitor_starts_should_dispose_its_scope_before_returning.cs:32` and `:53`. The monitor lines it cites (`TimedOutboxStaleMessageMonitor.cs:77` and `:189`, `TimedOutboxStaleMessageMonitorOptions.cs:74`) hold at HEAD. `UseOutboxStaleMessageMonitor` registers the monitor with `AddHostedService` (`HostedServiceCollectionExtensions.cs:106`), its constructor takes an optional `TimeProvider`, and nothing under `src/Paramore.Brighter.Extensions.DependencyInjection` registers one, so the monitor gets the `FakeTimeProvider` the test registers. Only the test arrangement changes, so the task is structural. These two files are the only start tests under `Observability/StaleMessages/` without a `TimeProvider`. The counts at tasks.md:19 match the 151 checkboxes, 5 of them `STRUCTURAL`, and the NFR-6 and AC-25 rows fit requirements.md:461. The three findings of pass 15 were not scored again; T6.12 does not touch them.

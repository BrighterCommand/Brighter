# Review: tasks — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-27
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address these before approving.

> This phase is already approved. Findings are informational — consider whether any warrant re-opening the phase.

> Review history for the task breakdown of this spec. Pass 1 PASS (0 at or above 60); pass 2 PASS (0); pass 3 PASS
> (0). Pass 4 NEEDS WORK (1: T6.4 would be green before its implementation). Pass 5 NEEDS WORK (1: the mutation
> named by T12.1 does not compile). Pass 6 NEEDS WORK (4: T10.5 and T10.6, T8.23, T11.4, and T9.11 and T9.12).
> Pass 7 NEEDS WORK (3: T10.5, T11.5 and T8.23). Pass 8 NEEDS WORK (2, both on T11.5). Pass 9 NEEDS WORK (1: T10.1
> read the stop token synchronously, before the loop could observe the stop). Pass 10 PASS (3 findings, 0 at or
> above 60); its findings were then fixed, and T11.2 and T11.3 were reworked so that their mutation fails the test
> without hanging it. Pass 11 PASS (7 findings, 0 at or above 60). Pass 12, after T10.12 and T10.13 were added,
> NEEDS WORK (4 findings, 1 at or above 60: the gauge check of T10.13 passes without the fix), recorded below.

## Findings

### 1. T10.13's gauge assertion passes without the fix, so "still clears the readings" is never tested (Score: 65)

The title of T10.13 promises that the stop "still clears the readings and cancels the calls". Its only check on the readings is "an export after the stop carries no gauge value", with the stop at 12:01:05, during the first due time's call. No count has completed at that point, and the gauge has no value before the first completion (T3.1). So the export carries no gauge value whether or not `_readings.Stop()` runs. Today `Log.Stopped` throws at `TimedOutboxStaleMessageMonitor.cs:208`, so the code skips both `_readings.Stop()` (:209) and `abandoned.CancelInProgress()` (:210). The token assertion and the Error-entry assertion make the test RED, but a fix that cancels the calls and logs at Error, and still skips `_readings.Stop()`, also goes GREEN. The task gives no RED mutation for that half.

**Evidence**: tasks.md:1367, "`StopAsync`, called at 12:01:05, returns, the call's token is cancelled (determinism rule 2), an export after the stop carries no gauge value"; tasks.md:243, "Started readings with topics report eight zero counter series and no gauge".

**Recommendation**: Give the readings a value before the stop. Complete an earlier due time first, as T10.4 does, throw on the stop's Information entry during the 12:02 call, and check that an export before the stop carries the gauge value and an export after it carries none. Or require a RED mutation that keeps the cancel and the Error log but drops `_readings.Stop()`, and require the test to fail under it.

---

### 2. Phase 11 says it depends on Phase 10, is fully checked, and Phase 10 now has two open production changes (Score: 50)

Phase 11 says "*Depends on Phase 10.*", and T11.1 to T11.5 are all checked. T10.12 and T10.13 are still open, and both change the loop in `TimedOutboxStaleMessageMonitor.cs` (`DecideAsync` at :255, the stop steps at :208-212). Phase 12 also depends on Phase 10. Nothing in the list says the Phase 11 tests are rerun after T10.12 and T10.13, or that Phase 12 waits for them. The full suite runs on each TEST + IMPLEMENT task, so the risk is small; the dependency line is what now says something untrue.

**Evidence**: tasks.md:1374 and tasks.md:1447, "*Depends on Phase 10.*"; T10.12 at tasks.md:1350 and T10.13 at tasks.md:1362 are `[ ]`.

**Recommendation**: Add a line under the Phase 11 header, or in T10.12 and T10.13, saying that they were added after Phase 11 was done, that the Phase 11 tests are part of the suite each of them runs, and that Phase 12 waits for them.

---

### 3. T10.12 does not name its new clock double or say where it lives (Score: 45)

T10.12 asks for "a clock double whose timestamps can be moved ahead without moving `GetUtcNow` or firing a timer (derived from `TimerCountingTimeProvider`...)". It gives no class name and no file in `StaleMessages/TestDoubles/`, and two classes are named `TimerCountingTimeProvider` (one under `StaleMessages/TestDoubles`, one under `Sweeping/TestDoubles`). The task also does not say when `StopAsync` is called relative to the move, though `WaitUntilAsync` (:299-315) and the time left to stop both use elapsed timestamps.

**Evidence**: tasks.md:1355.

**Recommendation**: Name the double (for example `TimestampShiftingTimeProvider` in `StaleMessages/TestDoubles/`, derived from the StaleMessages `TimerCountingTimeProvider`), and add one sentence saying that the move changes only the recorded end, that no timer fires, and that `StopAsync` comes after the Abandoned total reaches 1.

---

### 4. T10.13 does not say which log message carries the Error entry, or what happens when a later step throws (Score: 40)

The implementation line says "run the stop's steps (:208-212) even when an earlier one throws, and log the exception at Error". The existing Error message is `DueTimeFailed` (:637-638), whose text is about a due time. The task does not say whether the stop reuses it or gets its own message, and the test passes either way. The wording also covers a throw from `_readings.Stop()` or `CancelInProgress()`, but the test throws only from `Log.Stopped`.

**Evidence**: tasks.md:1370.

**Recommendation**: Name the message (reuse `DueTimeFailed` with the reason given, or add a `StopFailed` entry at Error), and either narrow the implementation line to the throw from `Log.Stopped` or say that the other steps' throws follow the same path and are left untested on purpose.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 2 |

**Total findings**: 4
**Findings at or above threshold (60)**: 1

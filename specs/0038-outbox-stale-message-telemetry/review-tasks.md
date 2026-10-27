# Review: tasks — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-27
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
> findings of pass 12 were fixed, PASS (3 findings, 0 at or above 60), recorded below.

## Findings

### 1. T10.12 does not say what the test waits for before it moves the timestamps, so a rare interleaving makes the test pass before the fix (Score: 55)

The test arranges for the call to end at the deadline while the deadline wait is still pending. That only works if the loop thread has already reached the await at `TimedOutboxStaleMessageMonitor.cs:311` before the test moves `GetTimestamp` ahead by T.

The order of events is:
- `MeasureAsync` starts the call at :227, and the call's `Task.Run` is at :480.
- Only after that does the loop thread compute the deadline's wait, at :230 and :303-308.
- The only signal the test has is that the counting double recorded the call. The double records it on the pool thread, inside that `Task.Run`.

If the shift lands before the check at :303 or :308, `WaitUntilAsync` returns true at once, so the deadline task completes as true. `DecideAsync` then wakes and finds `EndedBefore` false, whether or not the call has been released. `await deadline` at :255 is true, so the existing code abandons the call: the Abandoned total reaches 1, and Completed and Failed stay 0. The test is GREEN without the fix.

The window is small, because the loop thread runs synchronously from :227 to :308. Rule :27 ("If its test is green on arrival, stop and ask") also catches it when RED is first observed. But the committed test is a weaker regression guard than it looks, and the task gives the implementer no way to close the window. `TimersCreated` cannot be used here: the deadline uses `Alarm.RingAgainAfter`, which calls `_timer.Change` on the existing timer and creates none, so the count does not move.

**Evidence**: tasks.md:1355, "the 12:01 overall call blocks until released; the test moves the timestamps ahead by T and releases the call". That line names no wait before the move. `TimedOutboxStaleMessageMonitor.cs:226-230` and `:303-311`. tasks.md:24-26: determinism rules 1-3 cover waits after `Advance` and before advancing past a call's end, but not a wait before a timestamp shift.

**Recommendation**: Add one sentence giving the test a signal that the deadline wait is armed before it moves the timestamps. For example, `TimestampShiftingTimeProvider` could wrap the timers it creates and count `Change` calls, and the test would wait until the deadline's `Change` is seen, bounded as in rule 1. If that is not wanted, state the window and say that a green on arrival is to be rerun before stopping to ask.

---

### 2. T10.13 says the Error entry "carries the logger's exception", but the entry will carry an `AggregateException` wrapping it (Score: 35)

The capturing provider sits behind MEL's aggregate logger. When one provider's logger throws, the aggregate rethrows an `AggregateException`. T9.18 handles this with `Assert.Same(loggerFailed, error.Exception?.GetBaseException())`. T10.13's wording leads to an `Assert.Same(loggerFailed, error.Exception)`, which fails after a correct fix and sends the implementer looking for a defect that is not there.

**Evidence**: tasks.md:1368, "one Error entry, with the `StopFailed` message, carries the logger's exception". `When_the_loop_code_throws_should_log_it_and_keep_the_schedule.cs:132`, `Assert.Same(loggerFailed, error.Exception?.GetBaseException())`.

**Recommendation**: Say "whose base exception is the logger's exception", or point at T9.18's assertion.

---

### 3. T10.13's `ThrowOnce` match on "the Information entry of the stop" can also catch an Information entry from the start (Score: 25)

The monitor has three Information messages:
- `OverallCountOnlyWithNoCapability` (:640-641)
- `OverallCountOnlyWithNoMeasuredTopic` (:643-644)
- `Stopped` (:646-647)

T10.13 measures only the overall call and does not say whether the setup has a measured topic. If the setup has none and the match is on level alone, the throw fires on a start entry, during start. The stop then logs normally and the test is RED for the wrong reason. `ThrowOnce` drops the throw after it fires (`CapturingLoggerProvider.cs:150`).

**Evidence**: tasks.md:1368; `TimedOutboxStaleMessageMonitor.cs:640-647`; `CapturingLoggerProvider.cs:109-113`, `:142-155`.

**Recommendation**: Say that the match selects the `Stopped` message, for example by its text, or that `ThrowOnce` is set after the 12:01 due time completes.

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

## Pass 12 findings

1. **T10.13's gauge assertion passes without the fix (Score 65): resolved at tasks.md:1367-1368.** The 12:01 due time now completes first, and an export before the stop must carry the overall gauge value (:1367). The stop is now at 12:02:05, while the 12:02 call is blocked (:1368). Without the fix, `Log.Stopped` throws at :208 and the code skips `_readings.Stop()` (:209), so the export after the stop still carries the value and the gauge check fails along with the token and Error checks. After the fix, all three pass.
2. **Phase 11's dependency line no longer true (Score 50): resolved at tasks.md:1379.** The line says T10.12 and T10.13 were added after Phase 11, that each of them runs the full suite including Phase 11's tests, and that Phase 12 waits for both.
3. **T10.12's double unnamed and its location unstated (Score 45): resolved at tasks.md:1355 and :1357.** :1355 names `TimestampShiftingTimeProvider` in `StaleMessages/TestDoubles/`, derived from that folder's `TimerCountingTimeProvider` (not the one in `Sweeping/TestDoubles/`), which is not sealed; it overrides only `GetTimestamp`, `GetUtcNow` stays at 12:01 and no timer fires. :1357 places `StopAsync` after the Abandoned total reaches 1.
4. **T10.13's Error message unnamed, and later-step throws unaddressed (Score 40): resolved at tasks.md:1368, :1372 and :1373.** The test names `StopFailed` (:1368). :1372 adds `StopFailed` at Error with its text, instead of reusing `DueTimeFailed`. :1373 sends throws from `_readings.Stop()` and `CancelInProgress()` down the same path, untested on purpose, and gives the reason: `OutboxStaleMessageReadings` is sealed at `OutboxStaleMessageReadings.cs:39`, and the cancel runs off the loop's thread at `TimedOutboxStaleMessageMonitor.cs:546`.

Also checked, with no finding: the lines T10.12 and T10.13 cite at cf510d832 (:208-212, :249, :255, :546); the task counts at tasks.md:19 (150 tasks, all tag-first); and that after the fix, T10.12's stop returns with Completed and Failed at 0, since the fix lets `MeasureAsync` end through `decided` at :234 and the wait at :211 ends because the abandoned call has ended.

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
> findings of pass 12 were fixed, PASS (3 findings, 0 at or above 60). Pass 14, after findings 1 and 2 of pass 13
> were fixed and finding 3 was left as it was, PASS (2 findings, 0 at or above 60), recorded below.

## Findings

### 1. T10.13's `ThrowOnce` on "the Information entry of the stop" fires at start if it follows T9.18's pattern (Score: 35)

This is finding 3 of pass 13, scored again on its merits. It still applies, and it scores a little higher (35, up from 25), because the nearest example in the suite leads the implementer into it.

T9.18, the test that T10.13 itself cites, arms `ThrowOnce` before it builds the outbox and starts the monitor, and it matches on the level alone: `_logs.ThrowOnce(entry => entry.LogLevel == LogLevel.Debug, loggerFailed)`. A T10.13 written the same way, with `LogLevel.Information`, matches an Information entry that start logs first. Start logs one in two cases:
- with the base `StaleMessageCountingOutbox`, which has no topic-counting capability, it logs `OverallCountOnlyWithNoCapability`;
- with no measured topic, it logs `OverallCountOnlyWithNoMeasuredTopic`.

The setup T10.13 describes measures only the overall count, so one of these two entries is logged at start. The log call runs synchronously during start, at :114, before `_readings.Start` at :117. So the logger's throw comes out of start, the test never reaches the stop, and `ThrowOnce` has already dropped the throw.

The failure is loud and immediate, so it costs a debugging round rather than a wrong test. That is why the score stays low.

**Evidence**:
- tasks.md:1369 ("with `CapturingLoggerProvider.ThrowOnce` on the Information entry of the stop").
- `When_the_loop_code_throws_should_log_it_and_keep_the_schedule.cs:62`: armed before the outbox is built, matched on level only.
- `TimedOutboxStaleMessageMonitor.cs:113-117`: `LogWhenTopicsAreNotMeasured` at :114 runs before `_readings.Start`.
- `TimedOutboxStaleMessageMonitor.cs:383-389` and `:640-647`: three Information messages, `OverallCountOnlyWithNoCapability`, `OverallCountOnlyWithNoMeasuredTopic` and `Stopped`.
- `CapturingLoggerProvider.cs:135-155`: the throw comes out of `Record`, and `_throw = null` at :150 makes it one-shot.

**Recommendation**: In :1369, say that the match selects the `Stopped` message (for example, Information and `entry.Message.Contains("stopped for")`), or that `ThrowOnce` is armed after the 12:01 due time completes.

---

### 2. T10.12's signal rests on two things the task does not state about the wrapper (Score: 20)

:1356 says `TimestampShiftingTimeProvider` "wraps the timers it creates and signals at the first `GetTimestamp` after a `Change`". The design is sound:
- the only `Change` in the monitor is at :599, in `Alarm.RingAgainAfter`, which only the deadline wait uses (:230);
- the next clock read on the loop thread is the re-check at :308;
- the counting doubles record calls with `GetUtcNow`, not `GetTimestamp` (`StaleMessageCountingOutbox.cs:222` and `:234`), so no pool-thread read fires the signal early;
- `_ended = _timeProvider.GetTimestamp();` at :502 runs only after the release, which is after the move.

The task leaves two details unstated:
- **Overriding `CreateTimer` again.** The wrapper's `CreateTimer` overrides `TimerCountingTimeProvider.CreateTimer` a second time. It must call `base.CreateTimer` so that `TimersCreated` keeps counting. The existing row 6c test uses that count for its `WaitUntilTheMonitorWaits` step.
- **When the `Change` is recorded.** The wrapper must record the `Change` when it forwards the call to the inner timer, so that a `GetTimestamp` read inside the timer's own code cannot be counted as the signal. `FakeTimeProvider`'s timer reads its internal `_now` and not the virtual `GetTimestamp`, but the task does not say so.

**Evidence**:
- tasks.md:1355-1356.
- `TimedOutboxStaleMessageMonitor.cs:299-311`, `:589` and `:599`.
- `StaleMessages/TestDoubles/TimerCountingTimeProvider.cs`: not sealed; overrides only `CreateTimer` and returns the base timer unwrapped.
- `StaleMessageCountingOutbox.cs:118` and `:176`: `BlockUntilReleased` and `Release(int)` exist, so the blocking :1355 needs is already available.

**Recommendation**: Optional. Add half a sentence to :1356: the wrapper's `CreateTimer` calls `base.CreateTimer`, and its `Change` marks the timer before it delegates, so the signal comes from the monitor's read at :308.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

## Pass 13 findings

1. **T10.12 named no wait before it moves the timestamps (Score 55): resolved at tasks.md:1356.** Before the move, the test waits, bounded as in determinism rule 1, until the deadline wait has armed its timer and read the clock again. `TimestampShiftingTimeProvider` wraps the timers it creates and signals at the first `GetTimestamp` after a `Change`. The only `Change` in the monitor is in `Alarm.RingAgainAfter` (:599), which only the deadline wait uses (:230). With no per-topic count and the overall call blocked, the next clock read is the check at :308.
2. **T10.13 said the Error entry "carries the logger's exception" (Score 35): resolved at tasks.md:1369.** The `StopFailed` entry now carries an exception whose base exception is the logger's exception, as T9.18 asserts at `When_the_loop_code_throws_should_log_it_and_keep_the_schedule.cs:132`.
3. **T10.13's `ThrowOnce` match on "the Information entry of the stop" (Score 25): left as it was.** Scored again as finding 1 above.

Also checked, with no finding: the monitor is unchanged since cf510d832, so the lines T10.12 and T10.13 cite still hold; T10.12 is RED before the fix (the deadline does not complete before the stop, Abandoned stays 0 and the stop records Completed) and GREEN after it; T10.13's three checks (the token, the gauge export after the stop and the `StopFailed` entry) all fail without the fix and pass after it; the reasons T10.13 gives for not testing throws from `_readings.Stop()` and the cancel still hold; the implementation text matches ADR 0085-outbox-stale-message-telemetry (row 6c, :251-262, :268, :281 and :283-308); the ordering note at tasks.md:1380; and the task counts at tasks.md:19 (150 tasks, all tag-first, 53 TEST + IMPLEMENT, 62 CHARACTERISE, 7 GENERATE, 4 STRUCTURAL, 17 PROJECT, 7 DOC).

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
> were fixed and finding 3 was left as it was, PASS (2 findings, 0 at or above 60). Pass 15, after finding 1 of
> pass 14 was fixed and finding 2 was left as it was, PASS (3 findings, 0 at or above 60), recorded below.

## Findings

### 1. T13.1 and T13.2 cite `InMemoryOutbox.cs` lines that have moved since 942cb8f56 (Score: 35)

T13.1's mutation cites `src/Paramore.Brighter/InMemoryOutbox.cs:633` for the dispatched filter, and T13.2's two mutations cite `:635` for `.Take(maxCount)`. Both are right at 942cb8f56, the commit that unqualified citations refer to.

The file has changed since then. At HEAD, :633 is `var outstandingMessageCount = Requests`, :635 is the dispatched filter and :637 is `.Take(maxCount)`. So an implementer on HEAD who goes to T13.2's :635 finds T13.1's mutation target. The tasks reworked since cf510d832 qualify their citations with "at HEAD cf510d832", and these two do not. The prose names the code, so the right line is easy to find, and that keeps the score low.

**Evidence**:
- tasks.md:1514, :1524 and :1525.
- `git show 942cb8f56:src/Paramore.Brighter/InMemoryOutbox.cs`, lines 633 and 635.
- `src/Paramore.Brighter/InMemoryOutbox.cs` at HEAD, lines 633, 635 and 637.

**Recommendation**: Cite the lines at HEAD with the qualifier ("`:635` at HEAD bd2b16900" for the dispatched filter, "`:637` at HEAD bd2b16900" for `.Take(maxCount)`), or cite by content only.

---

### 2. The ADR note at tasks.md:11 excludes only one of the other ADRs that share its numbers (Score: 25)

The note maps ADR 0085 to `docs/adr/0085-outbox-stale-message-telemetry.md` and ADR 0086 to `docs/adr/0086-outbox-per-topic-stale-count.md`, so every "ADR 0085" and "ADR 0086" in the list points to the right file. What is incomplete is the sentence that follows: it names `docs/adr/0085-gcp-purge-server-boundary.md` as the other ADR numbered 0085.

`docs/adr/` now has two other files with these numbers: `0085-track-outbox-sweeper-state-and-publish-gauges.md` and `0086-sweeper-remedy-health-checks.md`. Both came with 280a6f6b7 (PR #4627), which is not an ancestor of 942cb8f56. Since the positive mappings already settle which file is meant, this is a gap in completeness, not a wrong reference. In the same way, `specs/0038-add-sweeper-health-check` now sits next to `specs/0038-outbox-stale-message-telemetry`, so "spec 0038" with no slug names two folders.

**Evidence**:
- tasks.md:11.
- `docs/adr/`: three files numbered 0085 and two numbered 0086.
- `git merge-base --is-ancestor 280a6f6b7 942cb8f56` exits 1.

**Recommendation**: Optional. Make the sentence name every other file with the same number, or say "any other ADR numbered 0085 or 0086". If wanted, add once that "spec 0038" means `specs/0038-outbox-stale-message-telemetry`.

---

### 3. T10.12's wrapper details are still unstated (Score: 20)

This is finding 2 of pass 14, scored again. It still applies. The design at tasks.md:1356 is sound: the only `Change` in the monitor is at :599, in `Alarm.RingAgainAfter`, which only the deadline wait uses (:230); the next clock read on the loop thread is the re-check at :308; and `_ended` at :502 is set only after the release.

Two details remain unstated:
- the wrapper's `CreateTimer` must call `base.CreateTimer`, so that `TimersCreated` keeps counting for `WaitUntilTheMonitorWaits`;
- the wrapper must record the `Change` before it delegates to the inner timer.

**Evidence**:
- tasks.md:1355-1356.
- `StaleMessages/TestDoubles/TimerCountingTimeProvider.cs`: not sealed; overrides only `CreateTimer`.
- `When_the_loop_reads_an_ended_count_after_the_deadline_should_decide_by_its_recorded_end.cs:113-114`, which relies on `TimersCreated`.

**Recommendation**: Optional. Add half a sentence on these two points to :1356.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 3 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0

## Pass 14 findings

1. **T10.13's `ThrowOnce` on "the Information entry of the stop" would fire at start (Score 35): resolved at tasks.md:1369.** The match is now `entry.LogLevel == LogLevel.Information && entry.Message.Contains("stopped")`, which selects only the `Stopped` message (:646-647) and neither of the two Information messages logged at start.
2. **T10.12's signal rests on two details of the wrapper that the task does not state (Score 20): left as it was.** Scored again as finding 3 above.

Also checked, with no finding: the monitor is unchanged since cf510d832, so the lines T10.12 and T10.13 cite still hold; T10.12 is RED before the fix and GREEN after it; without the fix, the throw from `Log.Stopped` faults the loop before `_readings.Stop()` and the cancel, so all three checks of T10.13 are RED; the implementation text matches rows 6c and 6d of ADR 0085-outbox-stale-message-telemetry (:251-262); the mutation targets of T12.1 and T12.2 exist; the Phase 13 citations in `tools/`, `.github/` and `RelationalDatabaseConfiguration.cs` hold at HEAD; every checkbox lead-in that this pass read is tag-first, each TEST + IMPLEMENT and CHARACTERISE task has a `/test-first` command, and the task counts at tasks.md:19 are unchanged. A fourth finding, that T12.3 names no outbox double, was withdrawn on a second check: T12.3 lists a publication, so the default setup at requirements.md:333-342 applies, and its `InMemoryOutbox` counts by topic (`InMemoryOutbox.cs:83` at HEAD bd2b16900). Phases 14 and 15, the coverage sections and T13.9 onward were not read in full this pass; bd2b16900 does not touch them.

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
> was added, PASS (3 findings, 0 at or above 60). Pass 17, after T9.22 was added, PASS (5 findings, 0 at or above
> 60), recorded below.

## Findings

### 1. Phase 9 is reopened while Phases 10 to 15 are already ticked, and the dependency is not addressed (Score: 50)

T9.22 is added unticked at the end of Phase 9 (tasks.md:1230). Phase 10 says "*Depends on Phase 9.*" (tasks.md:1246), and its tasks, along with later ones such as T15.4 (tasks.md:1880), are already `[x]`. The ticked T15.4 record now uses T9.22 to justify its exception ("so T9.22 reads its last item through the listener", tasks.md:1885), but T9.22 has not been done. Nothing in T9.22 says that no ticked task depends on it, or that it may run after later phases. A reader running `/spec:status` or T15.6 sees an open Phase 9 task behind closed phases, with no stated reason.

**Evidence**: `- [ ] **CHARACTERISE: T9.22 …**` (:1230) followed by `## Phase 10 … *Depends on Phase 9.*` (:1244-1246). T15.4 is `[x]` and cites T9.22 (:1880, :1885).

**Recommendation**: In T9.22, add one line saying it was added after Phase 9 closed, that no task in Phases 10 to 15 depends on it, and that it runs before T15.6. Or make T15.4's acceptance conditional on T9.22 being ticked.

---

### 2. T9.6's ticked "Test should verify" still claims the feature-disabled comparison that T9.22 says it does not make (Score: 45)

T9.22 says, correctly, that "T9.6 checks the last item of AC-9 through `OutboxStaleMessageReadings.GetTotal` and runs no host with the feature disabled" (:1235). This matches the test: it asserts `whileRunning` from `readings.GetTotal` (test :99-102, :135-140) and runs only one host. But T9.6's own bullet, ticked, still reads "which differs from a run with the feature disabled (no series at all)" (:1042). The file now contradicts itself about what T9.6 verified.

**Evidence**: tasks.md:1042 against tasks.md:1235. In `When_a_count_is_still_running_at_the_next_due_time_should_skip_it.cs`, `collected` (:94-99) is only checked for the MESSAGES instrument (:141).

**Recommendation**: Add a note under T9.6 that the feature-disabled comparison and the listener read moved to T9.22.

---

### 3. The first run of T9.22 largely repeats T9.6's scenario (Score: 35)

The first run copies T9.6 step by step. T9.6 already calls `metrics.Observe()` at both collections (test :98). Only the assertion target changes, from readings to listener values. CLAUDE.md says to "stop and ask if a test … duplicates an existing test". The task justifies keeping T9.6 as it is ("It leaves the T9.6 test as it is"), but it does not weigh adding the listener assertions and a second run to T9.6 instead.

**Evidence**: tasks.md:1235, :1237. T9.6 test :94-113.

**Recommendation**: Say in one line why a new test was chosen over extending T9.6, for example that the ticked T9.6 test stays unchanged. Or accept this as is.

---

### 4. The feature-disabled run's "collections at 12:02:20 and 12:03:20" have no timing meaning, and the release rationale is loose (Score: 25)

With `UseOutboxStaleMessageMonitor` absent, no monitor exists. There are no timers, so the T9.6 helper `WaitUntilTheMonitorWaits` cannot be used, and the instants are just `Advance` points. That is feasible, but the wording suggests a mirrored schedule. In the first run, "releases the call … so the monitor can stop" overstates the need. Per T10.1, stop returns within T even while a call is still running. The release mainly avoids leaving a blocked double task behind after the test.

**Evidence**: tasks.md:1237-1239. `When_the_stale_step_is_not_added_should_measure_nothing.cs` :76 uses a single `Advance(10 min)`.

**Recommendation**: Optionally say that the disabled run advances the clock to the same instants with no waits, and that the release avoids a dangling task.

---

### 5. Test file name reads awkwardly (Score: 15)

`When_a_count_is_still_running_should_report_measurements_unlike_the_feature_disabled` ends on "the feature disabled" with no noun ("…unlike_a_host_with_the_feature_disabled"). It still follows `When_..._should_...`.

**Evidence**: tasks.md:1234.

**Recommendation**: Optionally rename, for example `..._should_report_measurements_unlike_a_host_without_the_monitor`.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 4 |

**Total findings**: 5
**Findings at or above threshold (60)**: 0

## Checked in T9.22, with no finding

The counts at tasks.md:19 match the 152 checkboxes: 53 TEST + IMPLEMENT, 63 CHARACTERISE, 7 GENERATE, 5 STRUCTURAL, 17 PROJECT and 7 DOC. The meter lines that T9.22 and T15.4 cite (`ObserveOverall` :84-92, `ObserveTopics` :94-107, `ObserveMeasurements` :109-130) hold at HEAD, since the commit that added T9.22 changes only docs. With no readings, `OutboxStaleMessageMeter` creates no instruments (:59-60), so the run without the monitor receives nothing. Each `StaleMessageMetricsFixture` listens only to its own `IMeterFactory` (:68), so the two runs in one test do not see each other. The RED mutation fails only the first run. The test belongs in `StaleMessageLoggingCollection`, because its first run logs the skip warnings that T9.6 counts. The lead-in, the per-task line baseline (as in T6.12) and the CHARACTERISE gate wording match the rest of the file. The FR-6, FR-8, AC-9 and AC-25 rows trace to requirements.md. The findings of pass 16 were not scored again; T9.22 does not touch them.

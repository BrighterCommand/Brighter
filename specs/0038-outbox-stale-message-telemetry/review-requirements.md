# Review: requirements — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-11
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> Review history: pass 1 NEEDS WORK (21 findings, 9 at or above 60), after which the document was slimmed and
> rewritten; pass 2 NEEDS WORK (15, 3); pass 3 NEEDS WORK (11, 1); pass 4 PASS (10, 0), recorded below. The
> findings of pass 4 were then worked as well, and a narrow contradiction check over those edits found two items
> scored 50 or more (the AC-16 DynamoDB retry outcome and the AC-12 recovery check with an outcome counter), which
> were fixed. The "Status after pass 4" line under each finding records how it was handled. Later, after ADR `0084-outbox-metrics` merged, the line in the Problem Statement about its metrics was corrected: their added and cleared counters do show a backlog, but as a difference with no age that restarts with the process.

## Findings

### 1. NFR-1's on-call exception still conflicts, read literally, with its own "shares no lock, semaphore or flag" sentence (Score: 55)

The exception for a possible Q-2 on-call trigger applied to the first sentence of NFR-1 only, while the third sentence ("Measurement shares no lock, semaphore or flag with the posting path") had no exception, and "takes no lock" forbade most non-blocking signals.

**Recommendation**: Apply the exception to the whole of NFR-1 and narrow "takes no lock" to locks held while doing I/O.

**Status after pass 4**: Applied ("the only exception to this requirement, including the rest of this paragraph … takes no lock that measurement or the outstanding-message check can hold while doing I/O … Apart from the trigger's own signal").

---

### 2. Two of FR-6's three listed mechanisms cannot meet FR-6 as edited (Score: 52)

Before any success, not carrying the value forward and a last-success timestamp both look the same as the feature being disabled, so only an outcome counter met AC-9 on its own.

**Recommendation**: Say that options 1 and 3 need a companion signal, or drop them.

**Status after pass 4**: Applied; FR-6 lists the outcome counter first and says the other two need a companion signal.

---

### 3. AC-8's review-based check names a timer that does not meet FR-7's third bullet (Score: 45)

`PeriodicTimer` catch-up can produce two measurements and leave the grid; the stated reason ("cannot produce a missed due time") overstated the fake-clock limitation.

**Status after pass 4**: Applied; the example is a scheduler that computes the next grid point itself, and the reason is that the result is not deterministic with a fake clock.

---

### 4. FR-6's "no later than T" cannot be met for due times missed during process suspension (Score: 40)

**Status after pass 4**: Applied; for a coalesced due time the bound runs from when the scheduler runs again.

---

### 5. Q-8's election option leaves FR-6 and FR-10 undefined for instances that are not elected (Score: 40)

**Status after pass 4**: Applied; due times left out by an election are not skipped, not shown under FR-6 and not logged under FR-10.

---

### 6. An on-call trigger, if Q-2 adds one, has no bound on how often it queries (Score: 35)

**Status after pass 4**: Applied in Q-2 (at most one triggered measurement per I; counts as completed, failed or abandoned for FR-6; FR-7 and the Proposed Solution are updated if Q-2 is answered that way).

---

### 7. AC-16's DynamoDB retry rule "until it stops changing" can stop early (Score: 30)

**Status after pass 4**: Applied; retry until the expected value or 10 seconds, and a Meets column fails as usual (fixed again after the contradiction check).

---

### 8. It is unclear how many coalesced due times count as skipped (Score: 25)

**Status after pass 4**: Applied; N−1 count as skipped, logged as one entry carrying the number.

---

### 9. FR-6 has no converse: the signal is not required to clear (Score: 25)

**Status after pass 4**: Applied in FR-6, with a recovery check in AC-12 (refined after the contradiction check for a counter-based signal).

---

### 10. AC-1 "both become non-zero" is loose (Score: 15)

**Status after pass 4**: Applied ("at least one count call is recorded and at least one value is received").

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 8 |

**Total findings**: 10
**Findings at or above threshold (60)**: 0

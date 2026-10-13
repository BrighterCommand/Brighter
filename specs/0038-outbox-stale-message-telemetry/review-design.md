# Review: design — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-13
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> Review history for the ADRs of this spec. ADR `0085-outbox-stale-message-telemetry`: pass 1 NEEDS WORK (15
> findings, 2 at or above 60); pass 2 NEEDS WORK (15, 3), all three from a timer-callback design, after which the
> mechanism was rewritten around one background loop; pass 3 NEEDS WORK (11, 1); pass 4 PASS (8, 0). The ADR was then
> renumbered from 0084, because ADR `0084-outbox-metrics` merged with PR #4484, and updated for that ADR and for the
> comment rules in CONTRIBUTING.md: pass 5 NEEDS WORK (7, 1); pass 6 NEEDS WORK (7, 2); pass 7 NEEDS WORK (6, 2);
> pass 8 PASS (3, 0). After the maintainer's review on PR #4598 the requirements added an age per publication and a
> count per topic, and the design was revised: pass 9 NEEDS WORK (11, 3: rationale resting on the review instead of
> the requirement, two decisions in one ADR, and a misdescribed precedent), after which the optional outbox
> capability moved to the new ADR `0086-outbox-per-topic-stale-count`; pass 10 PASS (9, 0) over both ADRs, whose
> findings were then fixed; pass 11 PASS (6, 0) over those fixes, recorded below. The mermaid diagrams render on every
> pass, and from pass 6 every table was also checked to render (mermaid-cli 11 and markdown-it 14.1.0).

## Findings

### 1. The step 9 test of a count that returns before the deadline while a blocking logger holds the loop has no stated sync point (Score: 40)

**Status**: Open, for the task breakdown: the test needs a sync point that shows the count's task recorded its end before fake time reaches the deadline.

---

### 2. "Records its end as its last step" does not say it is recorded on every exit path (Score: 40)

**Status**: Open, for the task breakdown: record the end in a `finally`, whether the call returns or throws.

---

### 3. Row 4 makes AC-9, AC-10 and AC-33 depend on an abandoned or released call recording its end before the next due time (Score: 40)

**Status**: Open, for the task breakdown: monitor tests wait for the double's call to have ended before advancing to the next due time.

---

### 4. The `AddMessageWithAgeAsync` contract states an exact age that a store class cannot meet (Score: 30)

**Status**: Open, for the task breakdown of ADR `0086-outbox-per-topic-stale-count`.

---

### 5. The Terms of ADR `0086-outbox-per-topic-stale-count` define a word the ADR never uses and omit words it uses (Score: 25)

**Status**: Fixed in part: the unused word is gone.

---

### 6. Q-16 is attributed slightly differently in two places of ADR `0086-outbox-per-topic-stale-count` (Score: 20)

**Status**: Fixed.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 6 |

**Total findings**: 6
**Findings at or above threshold (60)**: 0

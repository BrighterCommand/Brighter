# Review: design — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-17
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
> findings were then fixed; pass 11 PASS (6, 0) over those fixes. Its findings 1 to 3, on the step 9 tests, stay open
> for the task breakdown, and so does its finding 4, on the exact age in ADR `0086-outbox-per-topic-stale-count`.
> After the maintainer's reminder on PR #4598 that the box tests are generated from Liquid templates, the age,
> ceiling and cancellation tests of the overall count became generated tests: pass 12 NEEDS WORK (12, 2: rationale
> resting on the review instead of durable reasons, and a second decision that the scope left out); pass 13 PASS
> (8, 0) over the fixes. Pass 13's findings 5 and 8, on the observation facts and the collection test, stay open
> for the task breakdown. The Firestore facts of step 13 then moved to the Firestore emulator, because the `gcp-ci`
> job does not run for a pull request from a fork: pass 14 PASS (5, 0), recorded below. The mermaid diagrams render
> on every pass, and from pass 6 every table was also checked to render (mermaid-cli 11 and markdown-it 14.1).

## Findings

### 1. No alternative of ADR `0085-outbox-stale-message-telemetry` considers running the Spanner and Firestore facts in the `gcp-emulator-ci` job (Score: 52)

**Status**: Not changed: running the Spanner and Firestore facts in CI is a build change that the requirements do not ask for.

---

### 2. ADR `0085-outbox-stale-message-telemetry` does not say how the Firestore emulator is started or which variables the run needs (Score: 42)

**Status**: Open, for the task breakdown: the task for step 13 is to name the Firestore emulator and the variables `FIRESTORE_EMULATOR_HOST` and `GOOGLE_CLOUD_PROJECT`.

---

### 3. Neither the table under *Where each type is touched* nor step 13 mentions the emulator switch of the Firestore provider (Score: 35)

**Status**: Not changed: one row of the table covers each outbox's provider class, and step 13 points to the subsection that describes the `Configure` action.

---

### 4. The sentence on an unset `FIRESTORE_EMULATOR_HOST` blurs the scope of the emulator switch (Score: 28)

**Status**: Fixed: the sentence now says that the Firestore facts still connect to a Google Cloud project.

---

### 5. The sentence on why the emulator shows each Firestore gap also covers column (d), which no run shows (Score: 18)

**Status**: Fixed: the sentence now names columns (a) to (c).

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

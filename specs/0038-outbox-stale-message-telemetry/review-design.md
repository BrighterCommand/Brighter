# Review: design — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-16
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
> (8, 0) over the fixes, recorded below. The mermaid diagrams render on every pass, and from pass 6 every table was
> also checked to render (mermaid-cli 11 and markdown-it 14.1).

## Findings

### 1. Alternative 34 of ADR `0085-outbox-stale-message-telemetry` takes the shape that ADR `0067-conformance-rollout-and-deferral-governance` rejects in its alternative 4, without answering its reasons (Score: 52)

**Status**: Fixed: alternative 34 now names that alternative and answers it with the FR-14 table and the order of step 13.

---

### 2. The `SupportsTopicCounting` setting of ADR `0086-outbox-per-topic-stale-count` is the not-rendered shape that alternative 32 of ADR `0085-outbox-stale-message-telemetry` rejects (Score: 48)

**Status**: Fixed: ADR `0086-outbox-per-topic-stale-count` now says why the topic-counting suite uses no gap mark, and where each column (d) gap still shows.

---

### 3. The last sentence on the `gcp-ci` job states a consequence that does not follow as written (Score: 40)

**Status**: Fixed: the sentence now says that step 13 sets the Firestore marks before the merge.

---

### 4. The base classes of ADR `0085-outbox-stale-message-telemetry` and the base test of ADR `0086-outbox-per-topic-stale-count` differ in shape and hook contracts, with no reason given (Score: 38)

**Status**: Not changed: the members table already says that the cases call the asynchronous count, as the monitor does.

---

### 5. The C of 1 and C of 2 observation facts assert nothing and stay green for good (Score: 35)

**Status**: Open, for the task breakdown: AC-16 asks for both counts with no expected value, so the facts stay.

---

### 6. The paragraph after the members table credits both base classes with hooks that only the AC-16 base has (Score: 30)

**Status**: Fixed.

---

### 7. The reason given for restricting the audits' scan is only half true (Score: 28)

**Status**: Fixed: the text now names the rule in the summary of `GatewaySkipConventionAudit`.

---

### 8. The collection test requires exactly one `[Collection(` line per generated file (Score: 20)

**Status**: Open, for the task breakdown: each template renders one test class per file.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 7 |

**Total findings**: 8
**Findings at or above threshold (60)**: 0

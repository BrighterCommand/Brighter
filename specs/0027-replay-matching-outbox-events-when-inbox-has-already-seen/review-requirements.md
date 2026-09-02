# Review: requirements — 0027-replay-matching-outbox-events-when-inbox-has-already-seen

**Date**: 2026-06-17
**Threshold**: 60
**Verdict**: NEEDS WORK

This phase is already APPROVED; these findings are informational only (BoxProvisioning re-review). The BoxProvisioning factual claims all verify against the codebase, but one new wording inconsistency around the CausationId index crosses the threshold.

## Findings

### 1. AC9 / Performance NFR are silent on the CausationId index that the ADR and replay rationale require (Score: 72)

The Performance NFR says "The outbox store *may* need an index on `CausationId`" — non-committal. AC9, introduced as the *verification* criterion for the schema change, mentions adding the `CausationId` *column* and `SupportsCausationTracking()` but says nothing about the index. Yet the ADR ships an index as part of the same schema work, and the replay mechanism (clearing `DispatchedAt` by `CausationId`) depends on it. AC9 therefore leaves the index's delivery untestable.

**Evidence**: AC9: "A fresh install and a migrated upgrade both end with the `CausationId` column present and `SupportsCausationTracking()` returning `true`." NFR: "The outbox store may need an index on `CausationId`." ADR §Schema Evolution: "the outbox stores add a secondary index on `CausationId`."

**Recommendation**: Add an explicit, testable index clause to AC9, or make the NFR definite and reference it from AC9 so its delivery is verifiable.

---

### 2. AC9 over-specifies internal test mechanics, making it brittle and partly non-testable as a requirement (Score: 64)

AC9 names a specific internal artifact — "the existing builder/migration drift parity test" — as the consistency mechanism. That is design/implementation detail (the ADR owns it with concrete test-name templates), not user-verifiable acceptance, and it will silently rot if the test is renamed.

**Evidence**: AC9: "...the matching live-builder DDL, kept consistent by the existing builder/migration drift parity test."

**Recommendation**: Keep the observable end-state assertions in AC9; reword the mechanism as an outcome ("builder DDL and migration produce identical column sets") rather than naming a specific test.

---

### 3. "Catalog-based" vs "provisioner-based" taxonomy in AC9 is asserted but never defined in requirements.md (Score: 55)

The new constraint and AC9 both lean on a three-way store taxonomy but requirements.md never defines what distinguishes "catalog-based" from "provisioner" delivery. The facts are accurate (verified) — this is a self-containedness/clarity issue.

**Evidence**: Constraint: "for the catalog-based stores ... and through the provisioner for Spanner"; AC9 mirrors it.

**Recommendation**: Add a one-line gloss clarifying that "catalog-based" stores carry versioned migration catalogs while Spanner provisions directly.

---

### 4. Schema-evolution vs data-backfill distinction is clear and non-contradictory (Score: 20 — informational, no action)

The Out-of-Scope rework cleanly separates in-scope schema evolution from out-of-scope data migration, consistent with the constraint and the nullable-column rationale. No stale "separate PR" language survives. No change needed.

**Recommendation**: None.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 1 |

**Total findings**: 4
**Findings at or above threshold (60)**: 2

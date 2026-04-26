# Review: tasks — 0027-box-schema-versioning-and-migrations

**Date**: 2026-04-26
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Review history

This is the **fifth-round** verdict on tasks.md. Prior rounds (in commit `5c7754517` and the working tree of the current branch) returned NEEDS WORK and produced corrective edits to both tasks.md and ADR 0057. The current document is consistent across all cross-references.

| Round | Verdict | Findings ≥60 | Resolution |
|-------|---------|--------------|------------|
| 1 | NEEDS WORK | 8 | All addressed in commit `5c7754517` |
| 2 | NEEDS WORK | 6 (F1–F6) | All addressed in working-tree edits |
| 3 | NEEDS WORK | 2 (F12 detection ownership, F13 AC mapping) + 3 below-threshold | All addressed in working-tree edits |
| 4 | NEEDS WORK | 2 (F17 ADR Key Components, F18 Task 1.4↔1.5 forward dep) + 1 below-threshold | All addressed in working-tree edits |
| 5 | **PASS** | **0** | — |

## Findings (current round)

### 1. Tasks 2.4 / 3.4 implementation text silent on helper invocation and pairing (Score: 45)

The "Pairing constraint" at Task 1.4 cross-references 2.4↔2.5, 3.4↔3.5, 4.4↔4.5, but is not restated locally inside Tasks 2.4 or 3.4. Task 2.4's "Implementation should" (lines 407–409) says only "Modify ... to three-path structure per ADR §3"; Task 3.4's (lines 538–542) similarly omits any mention of `PostgreSqlBoxDetectionHelpers.DetectCurrentVersionAsync` or `MySqlBoxDetectionHelpers.DetectCurrentVersionAsync`. Task 4.4 line 674 does explicitly call out `SqliteBoxDetectionHelpers.DetectCurrentVersionAsync`. A developer reading only Task 2.4 or 3.4 (without scrolling back to Task 1.4) might not immediately see the runner-needs-helper dependency. Below the 60 threshold — every reader has to consult ADR §3 anyway, and Tasks 2.5 / 3.5 spell out the helper move clearly.

**Evidence**: tasks.md lines 407–409 (Task 2.4 implementation), 538–542 (Task 3.4 implementation), vs. line 674 (Task 4.4 implementation correctly mentions the helper).

**Recommendation**: optional — add a one-line "calls `*BoxDetectionHelpers.DetectCurrentVersionAsync` from Task N.5; Tasks N.4 + N.5 land in the same commit (per Pairing constraint at Task 1.4)" bullet to Tasks 2.4 and 3.4 implementation lists.

---

### 2. ADR Key Components "DetectCurrentVersionAsync to each" includes Spanner imprecisely (Score: 30)

Line 361's wording "Spec 0027 adds a new `DetectCurrentVersionAsync` ... static method to each" lists all five helpers including `SpannerBoxDetectionHelpers`. Spanner has no migration list (per §6 / Task 5.x) and never invokes `DetectCurrentVersionAsync`. The §3 "Detection helper ownership" paragraph (line 352) is more precise — it scopes the addition to "Tasks 1.5 / 2.5 / 3.5 / 4.5" (omitting Spanner). Pre-existing wording carried through F17, not introduced by it. Below threshold; reader can reconcile via §3 / Phase 5.

**Evidence**: ADR 0057 line 361 vs. line 352; tasks.md lacks any Phase-5 task adding `DetectCurrentVersionAsync` to `SpannerBoxDetectionHelpers`.

**Recommendation**: optional — change "to each" on line 361 to "to each of `MsSqlBoxDetectionHelpers`, `PostgreSqlBoxDetectionHelpers`, `MySqlBoxDetectionHelpers`, `SqliteBoxDetectionHelpers`" to keep the two ADR statements aligned.

---

## Closure verification of fourth-round findings

- **F17 (ADR Key Components residual)**: **closed**. Lines 360–361 now describe provisioners as "simplified" (delegate to helper, V1Columns deleted) and `*BoxDetectionHelpers` as "extended" with `DetectCurrentVersionAsync` as the single source of detection truth.
- **F18 (Task 1.4 forward dependency on Task 1.5 helper)**: **closed**. Task 1.4 test narrowed to TOCTOU symptom only — seed V_latest-shape table, assert no `CREATE TABLE` exception + ≥1 history row + data preserved. New "Pairing constraint" sub-bullet at line 213 mandates Tasks 1.4 + 1.5 land in the same commit (with cross-reference to 2.4↔2.5, 3.4↔3.5, 4.4↔4.5).
- **F19 (ADR §2 pseudocode signature mismatch)**: **closed**. Pseudocode now uses static signature with all parameters flowed in, matching Task 1.5's specified method signature.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

## Next step

`/spec:approve tasks` — tasks.md is ready to begin Phase 0 implementation.

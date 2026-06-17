# Review: design — 0027-replay-matching-outbox-events-when-inbox-has-already-seen (ADR 0057)

**Date**: 2026-06-17
**Threshold**: 60
**Verdict**: PASS

This phase is already approved; findings are informational (BoxProvisioning re-review). Every concrete codebase reference in the new "Schema Evolution via BoxProvisioning" subsection was verified against the actual code, **with one correction noted below** (the inbox-version claim is wrong for PostgreSQL). The findings below are sub-threshold completeness/coherence observations.

> **Cross-review correction (added by main agent)**: This reviewer asserted "Inbox catalogs at V2 — confirmed for MsSql, MySql, Postgres, Sqlite." That is **wrong for PostgreSQL**, whose inbox catalog is V1-only (its V1 already carries `ContextKey`). The tasks reviewer caught this and the main agent verified it directly (`PostgreSqlInboxMigrationCatalog.cs` has a single `Version: 1`). The ADR text "the MsSql/MySql/Postgres/Sqlite inbox catalogs at V2" must be corrected. See review-tasks.md finding #2.

## Findings

### 1. `SupportsCausationTracking()` runtime-check story is coherent for catalog stores but underspecified for Spanner (Score: 52)

The subsection says for catalog stores `SupportsCausationTracking()` "reflects whether the `CausationId`-adding migration version has been applied to the live schema." For NoSQL it returns `true`. But the Spanner paragraph never states what `SupportsCausationTracking()` returns or how it is computed — Spanner has no versioned migration catalog, so the catalog-version check cannot apply. The Negative consequence and validation section both lean on this method as the un-migrated-user gate.

**Evidence**: ADR §Schema Evolution scopes the version-check claim to catalog stores; the Spanner paragraph omits the runtime-check semantics. `SpannerOutboxProvisioner`/`SpannerInboxProvisioner` exist with no catalog.

**Recommendation**: Add one sentence to the Spanner paragraph stating how `SupportsCausationTracking()` is evaluated for Spanner (e.g., a live column-existence probe).

---

### 2. "AddColumn helper" reference is slightly imprecise (Score: 30)

Catalogs use `AddColumns` (plural) for the migration `UpScript`s; `AddColumn` (singular) is the internal primitive. The ADR body correctly cites the *guard* (`IF COL_LENGTH(...) IS NULL`) rather than a helper name, so the ADR is fine — nit only.

**Evidence**: `MsSqlOutboxMigrationCatalog.cs:204` `AddColumns(...)`, `:207` `AddColumn(...)`.

**Recommendation**: None required for the ADR.

---

### 3. New outbox-builder index claim is prospective, not an existing pattern (Score: 35)

The subsection says "The outbox builders additionally create an index on `CausationId`," phrased as if extending existing behavior. None of the four catalog outbox builders currently create any index, so "additionally" could mislead. Also: the drift parity tests assert column sets, not indexes, so index parity is **not** covered by the cited drift gate.

**Evidence**: `grep -i index` is empty in all four `*OutboxBuilder.cs`.

**Recommendation**: Clarify the index is a newly introduced statement, and note index parity is unverified by the drift test.

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

**Grounding verification (all PASS except the PostgreSQL inbox-version claim corrected above)**: catalog classes, outbox-catalogs-at-V7, idempotent guard, `LogicalColumns`/`Cumulative()`/`s_vNAddedColumns`, live builders, drift parity tests, Spanner provisioners (no catalog), and the absence of NoSQL BoxProvisioning assemblies were all confirmed in the codebase. No residual "separate PR" language remains.

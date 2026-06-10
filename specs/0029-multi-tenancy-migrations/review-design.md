# Review: design (ADR 0060) — 0029-multi-tenancy-migrations (round 2)

**Date**: 2026-05-27
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. D5 seed-gate mechanism ("per-schema table absent before this run") is attributed to a detection step that does not establish that signal (Score: 52)

D5 states the one-time seed is "gated on 'per-schema table absent before this run', established by the existing detection step under the lock." Verified: the only under-lock detection is `RedetectStateAsync` (`SqlBoxMigrationRunner.cs:381-390`), which runs *after* `EnsureHistoryTableAsync` (`:208`) and checks the **box** table existence and `DoesHistoryExistAsync` (history **rows for the box table**), not whether the per-schema history **table** physically pre-existed. D5 places the seed inside `EnsureHistoryTableAsync` "after creating the per-schema table" — but the existing impls use `CREATE TABLE IF NOT EXISTS` (`MsSqlBoxMigrationRunner.cs:135-149`, `PostgreSqlBoxMigrationRunner.cs:130-137`) and return no created-vs-already-existed signal. So the table-level gate has no existing producer at the point D5 says it is available; the implementation must add a probe. The design is still sound (the `NOT EXISTS` row guard makes the seed idempotent regardless), but the narrative over-claims that the signal already exists.

**Evidence**: ADR D5: "established by the existing detection step under the lock." `EnsureHistoryTableAsync` (`SqlBoxMigrationRunner.cs:339`) is `void`-returning with `IF NOT EXISTS` impls; `RedetectStateAsync` (`:381`) checks box-table + history-row presence, runs after EnsureHistory, exposes no per-schema-history-table-existence boolean.

**Recommendation**: State that `EnsureHistoryTableAsync` gains a pre-create probe of the per-schema history table, or that the seed relies solely on the `NOT EXISTS` row guard for idempotency, rather than attributing the gate to an "existing detection step."

---

### 2. D4 PostgreSQL read-side enumerates the existence-literal but not the COUNT/MAX table qualifier (Score: 40)

D4 itemizes the PG read-side as "`PgIdentifier.Normalize` for the existence-check parameter" and "`PgIdentifier.Quote` for the CREATE/INSERT table qualifier." But `PostgreSqlBoxDetectionHelper.DoesHistoryExistAsync` has two hardcoded `public` references on the read side: the existence literal `TABLE_SCHEMA = 'public'` (`:122`) **and** the COUNT-query qualifier `"public"."__BrighterMigrationHistory"` (`:132`); `GetMaxVersionAsync` has its own qualifier too. The read-side table qualifiers are covered only by D4's general "Replace every hardcoded use of `HISTORY_TABLE_SCHEMA`" preamble and the architecture overview, not the explicit PG enumeration. Acceptable for an ADR but slightly under-inclusive.

**Evidence**: `PostgreSqlBoxDetectionHelper.cs:122` (literal), `:132` (qualifier). ADR D4 PG bullet names only "existence-check parameter" + "CREATE/INSERT table qualifier."

**Recommendation**: Add the read-side COUNT/MAX table qualifiers to the PG enumeration, or note "all read-side `public`-qualified table references route through Quote."

---

### 3. "Three call sites" / discarded-hint story is coherent and matches code (Score: 0 — confirmation)

Round-1 #1 is genuinely closed. Forces and D4 name the three sites; `SqlBoxProvisioner` holds only `IAmABoxMigrationRunner` (`:57`), calls the three read methods at `:151/:158/:166`, discard comment at `:155-157`. `MigrateAsync` branches off `RedetectStateAsync` (`:210-241`); the `tableState` parameter is genuinely unused for branch decisions, so the runner's under-lock read is authoritative (FR3 holds). The two-nulls risk is not real: `historySchema=null` ("default schema") and box `schemaName=null` ("SQLite no schema") are distinct parameters consumed independently (`MsSqlBoxDetectionHelper.cs:61,96`; `PostgreSqlBoxDetectionHelper.cs:90,138`).

---

**Round-1 closure summary:**
- **#1 (was 74)** — CLOSED. Three call sites named; provisioner passes null; runner authoritative; `tableState` confirmed unused; no two-nulls collision.
- **#2 (was 70)** — CLOSED. D5 seed lists exactly the 5 CREATE-DDL columns (verified `MsSqlBoxMigrationRunner.cs:141-148`, `PostgreSqlBoxMigrationRunner.cs:130-137`); `NOT EXISTS` on composite PK correct; the `SchemaName DEFAULT 'dbo'` concern is unfounded because the runner always stamps the explicit box schema (`MsSqlBoxMigrationRunner.cs:284`).
- **#3 (was 55)** — CLOSED (modulo minor under-enumeration, finding 2). PG folds both sides; `Quote`/`Normalize` both lowercase (`PgIdentifier.cs:57,77`).
- **#4 (was 25)** — CLOSED. `ConfigurationException(string)` ctor at `:45`; detection interfaces are generic `<TConnection,TTransaction>`.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0

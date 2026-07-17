# Review: requirements — 0029-multi-tenancy-migrations (round 4)

**Date**: 2026-05-27
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### Closed prior-round findings (confirmed)

- **Round-3 #1 (was High, 78)** — CLOSED. FR1a is now scoped: "On the backends where `PerSchema` performs placement (**MSSQL and PostgreSQL**), selecting `PerSchema` with a null `SchemaName` must be rejected ... On backends where the scope has no placement effect (MySQL, SQLite, Spanner), `PerSchema` is a no-op and this guard does not apply." AC1b confirms the MySQL cell positively: "no `ConfigurationException` is thrown." The earlier unconditional-reject vs. MySQL-behaves-as-Global contradiction is gone.
- **Round-3 #2 (was Medium, 61)** — CLOSED. AC4 now anchored: "On MSSQL and PostgreSQL, two tenants with distinct `SchemaName` values...", matching FR2/FR6 placement-backend scoping.
- **Round-3 #7 (was Low, 38)** — CLOSED. NF4 now reads "MySQL and the no-schema backends retain today's behaviour under any scope", consistent with the now-scoped FR1a.

### Matrix verification (no new contradiction found)

The full `scope × SchemaName × backend` matrix is internally consistent and exhaustively covered, with no cell double-specified with conflicting outcomes:

- MSSQL/PG — Global+null → FR4/AC2 (default); Global+non-null → FR4/AC2a (default, not inferred); PerSchema+null → FR1a/AC1a (reject); PerSchema+non-null → FR2/FR3/AC1 (placement). All four cells single-valued and AC-backed.
- MySQL — Global (any SchemaName) → FR4 / Out of Scope (`DATABASE()`); PerSchema (any incl null) → FR1a no-op + AC1b no-throw (`DATABASE()`). Consistent.
- SQLite/Spanner — any scope → no-op (Out of Scope). Consistent.

Cross-references all resolve. Every FR maps to ≥1 AC: FR1→AC2a, FR1a→AC1a+AC1b, FR2→AC1, FR3→AC1, FR4→AC2+AC2a, FR5→AC5, FR6→AC4.

Codebase spot-checks all hold: `HISTORY_TABLE_SCHEMA = "dbo"` const + "always lives in [dbo]" comment (MsSqlBoxMigrationRunner.cs:48–49); detection hardcodes `SELECT COUNT(1) FROM [dbo].[__BrighterMigrationHistory]` (MsSqlBoxDetectionHelper.cs:93); composite PK `(SchemaName, BoxTableName, MigrationVersion)` on MSSQL/PG/MySQL; Postgres `"public"` history comment (PostgreSqlBoxMigrationRunner.cs:48–52); MySQL unqualified history table (MySqlBoxMigrationRunner.cs:137) and SchemaName-stamped insert (245–246); MySqlOutboxMigrationCatalog treats configured schema as a separate database (~232); SQLite/Spanner schema-ignored docs (SqliteBoxDetectionHelper.cs:42–44, SpannerBoxDetectionHelper.cs:42–45); `IAmARelationalDatabaseConfiguration.SchemaName` nullable (61); `BoxProvisioningOptions` exposes only `MigrationLockTimeout`. Minor caveat: the issue's cited line numbers (e.g. "MsSqlBoxMigrationRunner.cs:140") are approximate (line 140 is the CREATE TABLE region, not an exact hit), but the cited facts are correct. Not a substantive defect.

### 1. SQLite/Spanner + PerSchema + null has no positive AC (Score: 34)

FR1a states the null-SchemaName guard "does not apply" on SQLite/Spanner, and Out of Scope says "scope has no effect" there. But unlike MySQL — which gets AC1b explicitly asserting *no ConfigurationException is thrown* — there is no AC asserting the no-throw / no-op outcome for SQLite or Spanner under `PerSchema`. Coverage is implied by the Out-of-Scope text but not test-pinned.

**Evidence**: AC1b covers only MySQL; ACs contain no SQLite/Spanner+PerSchema+null assertion. FR1a names SQLite/Spanner as guard-exempt.

**Recommendation**: Optionally broaden AC1b (or add a sibling AC) to assert the same no-throw/no-op outcome on SQLite and Spanner under `PerSchema`. Non-blocking since these backends are explicitly out of scope.

### 2. NF4 has no directly-mapped AC (Score: 22)

NF4 ("consistency across in-scope backends; MySQL/no-schema retain today's behaviour under any scope") is asserted indirectly via AC1/AC1b/AC2 but has no AC that names it the way AC3→NF2, AC6→NF3, AC7→NF5 do. Minor traceability nit; the behaviour is otherwise covered.

**Evidence**: NF4; no AC tags NF4.

**Recommendation**: Optionally add "(exercises NF4)" to AC1 or AC4. Cosmetic.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

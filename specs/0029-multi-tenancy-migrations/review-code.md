# Review: code — 0029-multi-tenancy-migrations

**Date**: 2026-05-31
**Threshold**: 60
**Verdict**: PASS
**HEAD at review**: `287d6b387`
**Round**: 2 (re-review after tidy of round-1 findings 1/2/3)

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Sample-app SQLite WAL/SHM artefacts surface as untracked (Score: 15)

`samples/WebAPI/WebAPI_Dapper/GreetingsWeb/Greetings.db-shm` and `Greetings.db-wal` appear in the working tree as untracked. They are SQLite runtime artefacts from the sample app, unrelated to spec-0029, and were already flagged in round 1 (Finding 4) as a sample-app concern deferred to a separate change. The current `.gitignore` already excludes `*.sqlite` and `*.db`; extending the pattern to `*.db-shm` / `*.db-wal` is the obvious fix but explicitly out of scope for this spec.

**Evidence**:
- `git status` shows both files as the only untracked entries.
- `.gitignore` excludes `*.db` and `*.sqlite` but not the SQLite WAL/SHM sidecar extensions used by the sample.

**Recommendation**: Track in a separate non-spec-0029 change (e.g. add `*.db-shm` / `*.db-wal` to `.gitignore`, or scope the addition under `samples/WebAPI/WebAPI_Dapper/GreetingsWeb/`). No action required for this PR.

---

## Tidy commit (`287d6b387`) verification

All three round-1 findings are correctly and completely addressed by the tidy. Confirmed by direct inspection of the diff and a follow-up grep of the resulting source:

### Finding 1 (was Score 30) — verified fixed
- `grep -rn "ResolveHistorySchema()" src/Paramore.Brighter.BoxProvisioning*` shows zero remaining `?? HISTORY_TABLE_SCHEMA` fallback sites in production code. Three placement-backend sites correctly use `!`:
  - `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:434` — `var historySchema = ResolveHistorySchema()!;` inside `ResolveHistoryTableSchema()`.
  - `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs:138` — `var schema = ResolveHistorySchema()!;` inside `QuotedHistorySchema()`.
  - `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs:154` — `var resolvedHistorySchema = ResolveHistorySchema()!;` inside `EnsureHistoryTableAsync`.
- The `!` is sound: MSSQL `DefaultHistorySchema => HISTORY_TABLE_SCHEMA` ("dbo" const) and PG `DefaultHistorySchema => HISTORY_TABLE_SCHEMA` ("public" const) — both at line 98 of their respective files. Both override `SupportsPerSchemaHistory => true` (line 101). The D3 guard at `SqlBoxMigrationRunner.cs:201–205` rejects `PerSchema && SupportsPerSchemaHistory && Configuration.SchemaName is null` before `MigrateAsync` proceeds. So `ResolveHistorySchema()` is provably non-null on placement backends at every `!` site.
- The remaining non-bang `ResolveHistorySchema()` call sites correctly keep the `string?` shape:
  - `SqlBoxMigrationRunner.cs:236` (per-run log) uses `?? "<backend default>"` — required because MySQL/SQLite have `DefaultHistorySchema => null`.
  - `SqlBoxMigrationRunner.cs:453`, `MsSqlBoxMigrationRunner.cs:397`, `MySqlBoxMigrationRunner.cs:223`, `PostgreSqlBoxMigrationRunner.cs:432`, `SqliteBoxMigrationRunner.cs:267` all pass the (nullable) result through to detection-helper signatures whose `historySchema` parameter is `string?` — correct.
- MySQL/SQLite are unaffected: `ResolveHistoryTableSchema()` / `QuotedHistorySchema()` are private to the MSSQL/PG concrete runners.

### Finding 2 (was Score 25) — verified fixed
- `grep -rn "AssertSafe(legacySchema" src/Paramore.Brighter.BoxProvisioning*` returns zero hits.
- `grep -rn "AssertSafe(boxSchema" src/Paramore.Brighter.BoxProvisioning*` still returns both seed paths (`MsSqlBoxMigrationRunner.cs:268` and `PostgreSqlBoxMigrationRunner.cs:297`) — operator-supplied identifier validation preserved.
- `const string legacySchema = HISTORY_TABLE_SCHEMA;` remains at `MsSqlBoxMigrationRunner.cs:255` and `PostgreSqlBoxMigrationRunner.cs:283`, so the dropped check was indeed validating a compile-time const.
- Updated comments (MSSQL:262–267, PG:291–296) accurately describe the new state, identify the trigger condition under which the check should be restored ("a future refactor turning legacySchema into a non-const derived value"), and retain the rationale for the surviving `AssertSafe(boxSchema)` call. No leftover "defence in depth" claims about `legacySchema` elsewhere in the box-provisioning source.

### Finding 3 (was Score 20) — verified fixed
- `release_notes.md:226` now reads: "The derived `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` interface file itself is unchanged — its implementors inherit the new signature through interface inheritance."
- `git diff master...HEAD -- src/Paramore.Brighter.BoxProvisioning/IAmAVersionDetectingMigrationHelper.cs` is empty — the interface file is in fact unchanged from master, so the reworded paragraph is factually accurate.
- The accompanying claim at line 242 ("`DetectCurrentVersionAsync` is **unchanged**") remains, and direct inspection of the interface file (lines 51–79) confirms `DetectCurrentVersionAsync` has no `historySchema` parameter — consistent with ADR 0060 D4 errata.

### No-behavioural-change verification
- `git diff 2a0674b21..287d6b387 -- src/` shows exactly two production files changed with the four narrow edits described in the commit message (3 fallback replacements + 2 dropped `AssertSafe(legacySchema)` calls + comment rewrites). No other production-code changes leaked in.
- The `!` is not applied to any nullable that could legitimately be null at runtime; it sits only on `ResolveHistorySchema()` results in MSSQL/PG-private helpers where the D3 guard + `DefaultHistorySchema` const make the value provably non-null. `Configuration.SchemaName` is untouched (still nullable, still passes through `ResolveHistorySchema()`'s ternary).
- Tidy validated by author against tests:
  - `BoxProvisioning.Tests` base 65/65 net9 + 65/65 net10
  - MSSQL `BoxProvisioning` subset 84/84 net9 + 84/84 net10 (separate runs per parallel-TFM deadlock convention)
  - PG `BoxProvisioning` subset 75/75 net9 + 75/75 net10 (separate runs per parallel-TFM flake convention)
  - Counts identical to the round-1 VERIFY baseline at HEAD `2a0674b21`.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 1 |

**Total findings**: 1
**Findings at or above threshold (60)**: 0

## Round 1 reference (for the audit trail)

The previous review at HEAD `2a0674b21` (record committed at `ecb4f10d4`) raised four Low findings, all below threshold:

| # | Title | Round-1 Score | Status |
|---|------|---------------|--------|
| 1 | `ResolveHistoryTableSchema()` `??` fallback unreachable | 30 | ✅ Fixed in `287d6b387` |
| 2 | Dead `Identifiers.AssertSafe(legacySchema)` on the compile-time `dbo`/`public` const | 25 | ✅ Fixed in `287d6b387` |
| 3 | Release notes overstate the interface change scope | 20 | ✅ Fixed in `287d6b387` |
| 4 | Sample-app SQLite WAL/SHM untracked | 15 | Carried forward (out of scope) |

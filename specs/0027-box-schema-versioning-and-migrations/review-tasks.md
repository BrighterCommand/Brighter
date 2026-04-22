# Review: tasks — 0027-box-schema-versioning-and-migrations

**Date**: 2026-04-22
**Threshold**: 60
**Verdict**: NEEDS WORK

8 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Spanner `*Migrations.cs` files will not compile after Task 0.2 — omitted from Task 0.3's call-site update list (Score: 90)

Task 0.2 changes the `BoxMigration` record to add `LogicalColumns` as a required positional parameter. Task 0.3 lists 8 files to update (MsSql/PostgreSql/MySql/Sqlite × Outbox/Inbox) but explicitly omits the Spanner equivalents. The Spanner migration files DO exist and DO instantiate `new BoxMigration(...)`:

- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxMigrations.cs:15 — new BoxMigration(...)`
- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerInboxMigrations.cs:15 — new BoxMigration(...)`

Task 5.3 contradictorily says "No `IAmABoxMigration` list is needed — Spanner provisioner holds `V_latest` as a constant". Either these files should be deleted (not mentioned anywhere) or updated with the new parameters (Task 0.3 omits them). As currently written, the codebase will not compile after Task 0.2 lands until Phase 5 is complete — forcing a six-task solid gap of broken `main` between Phase 0 and Phase 5.

**Evidence**: tasks.md:58–73 (Task 0.3 file list has no Spanner entries); tasks.md:656 ("No `IAmABoxMigration` list is needed"); grep shows `new BoxMigration` in both Spanner migration files.

**Recommendation**: Either (a) add a Task 0.3a that deletes `SpannerOutboxMigrations.cs` / `SpannerInboxMigrations.cs` in Phase 0 (and purges any `new BoxMigration` call inside `SpannerBoxMigrationRunner`/Provisioners); or (b) add them to Task 0.3's file list with a trivial `LogicalColumns = empty-set` bridge until Phase 5 reworks them. Today's wording guarantees a broken build between phases.

---

### 2. Existing fresh-install and bootstrap tests hard-code `MigrationVersion = 1` — will fail after Phase 1 and are not mentioned in tasks (Score: 88)

Tasks assume existing tests "continue to pass" (0.3) or "are re-validated" (AC-17 checklist), but several existing tests hard-code V=1 assertions that directly contradict the new "fresh install stamps V_latest" semantics:

- `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_mssql_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs:55-59` — asserts `MigrationVersion = 1`
- `When_mssql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs:50-54` — same hard-coded V=1

Similar hard-codings exist in PostgreSQL/MySQL/Sqlite test directories. Under spec 0027, fresh install stamps V7 (outbox) / V2 (inbox); bootstrap stamps the detected V. These tests will fail. Tasks never say "update the existing hard-coded V=1 assertions to V_latest". Task 1.9 handwaves "no production-code change expected" but doesn't mention updating these tests.

**Evidence**: tasks.md:72 ("No tests — unit tests for these classes already exist and must continue to pass"); actual test files assert `MigrationVersion = 1`.

**Recommendation**: Add an explicit item to Task 0.3 (or a dedicated Task 0.3a) saying: "Existing `When_*_runs_on_fresh_database_*` and `When_*_finds_existing_table_without_history_*` tests across all four backends must be updated from `MigrationVersion = 1` assertions to `MigrationVersion = V_latest` (outbox=7, inbox=2). List each file." Otherwise Phase 1 lands with a broken test suite.

---

### 3. Task 4.6 is a monolithic sweep covering 5 Acceptance Criteria in one test file (Score: 82)

Task 4.6 bundles: outbox bootstrap-at-V_k × 4 (AC-1), inbox bootstrap-at-V1 (AC-2), idempotency (AC-4), concurrency (AC-5/AC-18), AC-6 (spec-0023-era transition). That is five distinct AC categories in a single test file:

> "Bundled into one file because SQLite's single-writer model makes most scenarios trivially fast"

This violates the spec's own test-first convention ("Each test targets a single behavior") and contradicts the asymmetric granularity of Phases 1/2/3 (each has 4 separate tasks: 1.6/1.7/1.8/1.9 etc.). The concurrency arm tests a fundamentally different scenario from the AC-6 transition arm; a single-file failure would not localize cleanly. It also makes the Acceptance checklist dishonest — AC-17 for SQLite maps to "4.6 idempotency arm" which is not a distinct test.

**Evidence**: tasks.md:587–602 (Task 4.6 combines all of AC-1/2/4/5/6/17/18 into one test file).

**Recommendation**: Split Task 4.6 into 4.6 (outbox bootstrap-at-V_k), 4.7 (inbox bootstrap V1→V2), 4.8 (concurrent bootstrap), 4.9 (spec-0023 AC-6 transition), matching the granularity of Phases 1/2/3. "Single-writer model makes scenarios fast" is a runtime argument; it does not justify combining them at the spec level.

---

### 4. Task 0.3's claim that V1 DDL gets "replaced with proper V1-baseline DDL" in phases 1–4 contradicts ADR §3 and Tasks 1.2/2.2/3.2/4.2 (Score: 78)

Task 0.3 says:

> "V1 stays the full `*Builder.GetDDL(...)` string (unchanged) **until phases 1–4 replace it with the proper V1-baseline DDL**"

But Tasks 1.2, 2.2, 3.2, 4.2 all say "V1 UpScript stays the current builder DDL (fresh-install fast path uses V1's UpScript per ADR §3)". ADR §3 "Fresh path" explicitly requires: "Execute V1 UpScript (current builder DDL) — safe because we've verified under lock that the table doesn't exist", and "Why V1 does not need to be idempotent" section says "V1 is the full `CREATE TABLE` from the current builder DDL".

The "V1-baseline DDL" phrasing is misleading: only `V1.LogicalColumns` is the baseline 6-column set (for detection). `V1.UpScript` stays the current 22-column builder DDL forever. A task implementer reading 0.3 would believe they need to rewrite V1 UpScript to the 2015-era 6-column DDL — which would break fresh install.

**Evidence**: tasks.md:69 (Task 0.3 "until phases 1–4 replace it with the proper V1-baseline DDL"); tasks.md:133 / 309 / 426 / 538 (Tasks 1.2/2.2/3.2/4.2 all say "V1 UpScript stays the current builder DDL"); ADR 0057 §3 "Why V1 does not need to be idempotent".

**Recommendation**: Delete "until phases 1–4 replace it with the proper V1-baseline DDL" from Task 0.3. Substitute "V1.UpScript stays the current builder DDL permanently (per ADR §3 fresh-install fast path). Phases 1–4 only add V2..V_latest entries and populate V1.LogicalColumns with the baseline 6-column set used for detection — V1.UpScript itself does not change."

---

### 5. Task 2.1 references non-existent Postgres payload modes and a non-existent parameter name (Score: 74)

Task 2.1 says:

> "Outbox: `DdlColumnExtractor.GetExpectedColumns(PostgreSqlOutboxBuilder.GetDDL(\"outbox_test\", jsonb: false), QuoteStyle.Postgres)`"
> "Run against all payload variants (text / JSON / JSONB / binary) — Postgres supports four"

Both claims are factually wrong:

1. `PostgreSqlOutboxBuilder.GetDDL` has signature `GetDDL(string outboxTableName, bool binaryMessagePayload = false)`. The named argument `jsonb: false` is a compile error (C# named arguments must match parameter name exactly).
2. Postgres outbox builder has exactly TWO payload variants (text / binary) — no JSON, no JSONB. A grep for `jsonb|JSONB|json_payload|hasJsonBody` in `src/Paramore.Brighter.Outbox.PostgreSql/` returns zero matches.

Similarly, Task 1.1 uses `binary: false` for MSSQL — the actual parameter name is `hasBinaryMessagePayload` (same issue).

**Evidence**: `src/Paramore.Brighter.Outbox.PostgreSql/PostgreSqlOutboxBuilder.cs:100` (`GetDDL(string, bool binaryMessagePayload)`); `src/Paramore.Brighter.Outbox.MsSql/SqlOutboxBuilder.cs:113` (`GetDDL(string, bool hasBinaryMessagePayload)`); tasks.md:103 (`binary: false`); tasks.md:287 (`jsonb: false`); tasks.md:290 (four payload variants claim).

**Recommendation**: Fix Task 2.1 to `binaryMessagePayload: false` and remove the "text / JSON / JSONB / binary — Postgres supports four" statement (test only text and binary). Fix Task 1.1 to `hasBinaryMessagePayload: false`. Audit every other `GetDDL(...)` call in tasks.md for correct parameter names.

---

### 6. Tasks 6.3 / 6.4 hedge on existence of Payload validators that already exist (Score: 72)

Tasks 6.3 and 6.4 say "if `SqlitePayloadModeValidator` exists" and "if Spanner exposes binary payload variant… via `SpannerPayloadModeValidator` (if present)". Both validators DO exist:

- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqlitePayloadModeValidator.cs`
- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerPayloadModeValidator.cs`

Spec 0023 review-code.md finding R5 (line 101–114) explicitly names all four validators as existing-but-untested. The tasks.md hedging makes these tasks speculative when they should be concrete. Worse, Task 6.3's "otherwise document as not-applicable and mark test skipped" creates an escape hatch for a test the reviewer actually wants written.

**Evidence**: codebase `ls src/Paramore.Brighter.BoxProvisioning.Sqlite/` and `ls src/Paramore.Brighter.BoxProvisioning.Spanner/` both list the validator files; `specs/0023-box_database_migration/review-code.md:101–114` names them as existing.

**Recommendation**: Rewrite 6.3 and 6.4 to drop the hedging: "SQLite/Spanner payload-mode-mismatch test — the validator exists; write the test following the MSSQL pattern." Remove the "mark skipped" escape.

---

### 7. Task 7.2 delegates release-notes location to "check with maintainer" — the file is `release_notes.md` at repo root (Score: 70)

Task 7.2:

> "File: release notes location (check with maintainer; likely `docs/release-notes/` or top-level `CHANGELOG.md`)"

Both guesses are wrong. The actual file is `release_notes.md` at repo root. `CHANGELOG.md` does not exist; `docs/release-notes/` does not exist. This is discoverable via `find . -iname "release*"` — the tasks.md author could have verified.

"Check with maintainer" is not actionable for an agent executing `/spec:implement`.

**Evidence**: `find` confirms `release_notes.md` at repo root is the only candidate (plus `.github/release-drafter.yml`).

**Recommendation**: Fix Task 7.2 to say: "File: `release_notes.md` at repository root (confirmed by file listing)."

---

### 8. NFR-3 (30s lock timeout) has no test coverage (Score: 62)

Requirements NFR-3: "A full bootstrap from V1 → V_latest on a single outbox table completes within the default MigrationLockTimeout (30s). On typical RDBMS instances the expected time is well under 1s since each migration is a single DDL." Grepping tasks.md for "NFR-3", "30s", "MigrationLockTimeout", "lock timeout" returns zero matches. No task verifies this NFR.

The requirements are explicit enough that at least one backend should have a timing assertion (e.g., "bootstrap at V1 completes in < 30s against the docker-compose MSSQL instance"). Without a test, NFR-3 is aspirational.

**Evidence**: `grep "NFR-3\|30s\|MigrationLockTimeout" tasks.md` → no matches; requirements.md NFR-3 explicit threshold.

**Recommendation**: Add a test task in Phase 1 (MSSQL reference) that instruments `MigrateAsync` duration for a V1→V7 bootstrap and asserts < 30s (or < 5s as a tighter CI-friendly bound). Optionally extend to other backends if cross-backend verification is required.

---

### 9. Task 3.4 proposes a "test-only seam" in production code for mid-chain failure injection (Score: 58)

Task 3.4 says:

> "inject a failure after V5 applied (e.g. by corrupting V6 UpScript via a test-only seam or by a targeted SQL mock)"

A "test-only seam" added to production code is a design smell — interfaces exist specifically so behavior can be substituted without production-code seams. A cleaner approach is a `TestMigration : IAmABoxMigration` implementation in the test project whose `UpScript` is deliberately-broken SQL (e.g., `SELECT 1 FROM this_does_not_exist`). That passes through the normal runner path and exercises the real mid-chain failure semantics ADR §5a describes.

**Evidence**: tasks.md:450.

**Recommendation**: Rewrite 3.4's implementation note: "Inject failure by substituting a deliberately-broken `IAmABoxMigration` as V6 in the test project's migration list — no production seam required. The test swaps in `new BoxMigration(6, \"broken\", \"SELECT 1 FROM non_existent_table\", logicalCols, null, null)` and asserts the runner throws, history has V4/V5 rows, and re-running resumes from V6 (real one)."

---

### 10. Whole-chain rollback (MSSQL/Postgres/SQLite) is never tested (Score: 54)

ADR §5a makes a strong claim: "MSSQL/Postgres/SQLite: history row inserted immediately after each migration's DDL, inside the same transaction. Either both are visible on commit or neither is (on rollback)." And: "Failure rolls back **all** migrations in the batch + all history rows written in the batch."

MySQL's per-migration-commit recovery is tested (Task 3.4). The MSSQL/Postgres/SQLite whole-chain rollback semantics are NOT tested in any task. An implementer could accidentally break transactional wrapping (e.g., inserting history rows outside the transaction) and no test would catch it. Tasks 1.8/2.7's concurrency tests don't exercise failure — they test two successful runners racing.

**Evidence**: `grep "rollback\|Rollback" tasks.md` returns no backend-specific rollback test.

**Recommendation**: Add a Task 1.x / 2.x / 4.x mid-chain failure test analogous to Task 3.4 but asserting whole-chain rollback (after V6 fails, history is empty / MAX(V) = k_seed, not V4/V5).

---

### 11. Inbox concurrent-bootstrap has no explicit coverage beyond SQLite's 4.6 (Score: 52)

AC-18 ("per-backend concurrent-bootstrap test") maps to "1.8, 2.7, 3.7, 4.6". But 1.8, 2.7, 3.7 explicitly test *outbox* ("seed a V3 outbox"). Task 4.6 bundles inbox inside the monolithic sweep. For MSSQL/Postgres/MySQL, no inbox concurrent-bootstrap test exists. If the concurrency mechanics differ for inbox (different table, different discriminator) a bug would not be caught.

**Evidence**: tasks.md:244–254 (1.8: "Seed a V3 outbox" only); 2.7 and 3.7 similar.

**Recommendation**: Either extend 1.8/2.7/3.7 descriptions to include a minimal inbox concurrent-bootstrap assertion, or explicitly document that "the runner is box-type-agnostic, so outbox coverage transitively covers inbox." The spec should be explicit.

---

### 12. Drift-test helper `GetExpectedColumns` needs to cope with SQLite COLLATE NOCASE inside parentheses, not just as trailing clauses (Score: 45)

Task 0.4 specifies the helper must "strips COLLATE NOCASE clauses" (for SQLite). SQLite's actual DDL is `[MessageId] TEXT NOT NULL COLLATE NOCASE,` — the COLLATE clause is inline with the column declaration. A regex that splits on commas and extracts the first bracket-quoted identifier per line will handle this naturally. Task 0.4's test cases (line 82) include "a COLLATE clause" but don't specify location — could be read as trailing.

**Evidence**: SqliteOutboxBuilder.cs:37; tasks.md:82.

**Recommendation**: Clarify Task 0.4's test assertion to include an SQLite-style inline `COLLATE NOCASE` after the type specifier, not just as a table-level clause. Minor.

---

### 13. StubBrighterBuilder does not require updating (non-finding surfaced for completeness) (Score: 40)

`tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/StubBrighterBuilder.cs` is a pure `IBrighterBuilder` stub with no `IAmABoxMigration` references. Tasks correctly omit it. Documenting here because the review prompt asked to verify.

**Evidence**: StubBrighterBuilder.cs:1–30 reviewed; no `BoxMigration` or migration-list references.

**Recommendation**: No change.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 6 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 2 |

**Total findings**: 13
**Findings at or above threshold (60)**: 8

# Review: code — 0029-multi-tenancy-migrations

**Date**: 2026-05-31
**Threshold**: 60
**Verdict**: PASS
**HEAD at review**: `2a0674b21`

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. `ResolveHistoryTableSchema()` `??` fallback is unreachable in production (Score: 30)

`MsSqlBoxMigrationRunner.ResolveHistoryTableSchema()` and the equivalent fold in `PostgreSqlBoxMigrationRunner.EnsureHistoryTableAsync` both apply a `?? HISTORY_TABLE_SCHEMA` fallback to `ResolveHistorySchema()`. On MSSQL and PG, `DefaultHistorySchema` is the non-null literal `"dbo"`/`"public"`, and under PerSchema the D3 guard guarantees `Configuration.SchemaName` is non-null. So the only way the resolver returns null is if a test-only subclass overrides `DefaultHistorySchema` to null — out of band for production. PROMPT.md acknowledges this as a deferred tidy-first.

**Evidence**:
- `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:429`: `var historySchema = ResolveHistorySchema() ?? HISTORY_TABLE_SCHEMA;`
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs:134`, `:147`: same shape.

**Recommendation**: Drop the `?? <const>` fallback in a follow-up tidy-first commit; if defensive against test subclasses it should be a `Debug.Assert`/`throw new InvalidOperationException` not a silent re-coerce. Not blocking.

---

### 2. Defensive `Identifiers.AssertSafe(legacySchema)` on the compile-time `dbo`/`public` constant (Score: 25)

Both seed paths call `Identifiers.AssertSafe(legacySchema, nameof(legacySchema))` on a `const string legacySchema = HISTORY_TABLE_SCHEMA` (literal `"dbo"`/`"public"`). The literal is trivially safe; the validation is dead defence kept on principle. PROMPT.md flagged this. Comment in MSSQL file explicitly labels it "defence in depth".

**Evidence**:
- `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:265`
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs:288`

**Recommendation**: Optional follow-up. Either remove (cleaner) or leave as documented defensive boundary. Not blocking.

---

### 3. Release notes overstate the interface change scope (Score: 20)

`release_notes.md` says: *"`IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` **and the derived `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>`** add a `string? historySchema` parameter"*. The derived interface file `IAmAVersionDetectingMigrationHelper.cs` is **unchanged** in the diff (`git diff master...HEAD -- src/Paramore.Brighter.BoxProvisioning/IAmAVersionDetectingMigrationHelper.cs` returns nothing) — the modified methods are inherited from the base via interface inheritance, so it is technically true that implementors of the derived interface inherit the new signature, but the wording suggests a separate change. ADR D4 Errata is explicit that only the two history-reading methods on the base interface change.

**Evidence**: `release_notes.md` (Source-breaking change section); `src/Paramore.Brighter.BoxProvisioning/IAmAVersionDetectingMigrationHelper.cs` unchanged in the diff.

**Recommendation**: Reword release notes to "implementors of `IAmAVersionDetectingMigrationHelper` therefore inherit the new signature via the base; the derived interface itself is unchanged". Cosmetic.

---

### 4. Two unrelated pre-existing SQLite WAL/SHM files committed in working tree (Score: 15)

`samples/WebAPI/WebAPI_Dapper/GreetingsWeb/Greetings.db-shm` and `Greetings.db-wal` are untracked in the working tree (per `git status`). PROMPT.md context notes they are pre-existing and unrelated. Not spec-0029 collateral; flagged here per the review brief.

**Evidence**: `git status --porcelain` shows only these two `??` entries.

**Recommendation**: Add `*.db-shm`, `*.db-wal` to `.gitignore` if not already covered (a sample-app concern, not spec-0029).

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 4 |

**Total findings**: 4
**Findings at or above threshold (60)**: 0

## Detailed FR/AC ↔ Code Trace (verification artefact)

Verified against the diff, ADR 0060, and tasks.md. Every requirement traces to code and a test.

| Item | Spec | Code Location (HEAD) | Test Location | Status |
|------|------|---------------------|----------------|--------|
| FR1 / D1 | Enum + property, never inferred | `BoxProvisioningOptions.cs:91`, `MigrationHistoryScope.cs:68-91` | implicit (every PerSchema test uses opt-in) | ✓ |
| FR1a / AC1a / D3 | PerSchema + null + placement backend → throw | `SqlBoxMigrationRunner.cs:201-205` (gated on `SupportsPerSchemaHistory`) | `When_mssql_per_schema_scope_is_selected_with_null_schema_name_it_should_throw_configuration_exception.cs`, PG counterpart | ✓ |
| FR2 / AC1 / D2+D4 | PerSchema → history in configured schema | MSSQL: `MsSqlBoxMigrationRunner.cs:132,429`; PG: `PostgreSqlBoxMigrationRunner.cs:147-150` | `When_mssql_per_schema_scope_is_selected_it_should_create_history_table_in_configured_schema.cs` (+ PG) | ✓ |
| FR3 | Read+write same physical schema | Single `ResolveHistorySchema()` flowed both ways (`SqlBoxMigrationRunner.cs:173-176`, helpers); MSSQL existence delegation passes `historySchema` (`MsSqlBoxDetectionHelper.cs:88-89`) | covered transitively by AC1/AC5 tests | ✓ |
| FR4 / NF1 / AC2 / AC2a | Global default byte-for-byte, even with non-null SchemaName | `ResolveHistorySchema()` returns `DefaultHistorySchema` when scope==Global | `When_global_scope_is_used_with_a_non_default_schema_mssql_history_should_remain_in_dbo.cs` (+ PG) | ✓ |
| FR5 / AC5 / D5 | Flip seed preserves history | MSSQL: `MsSqlBoxMigrationRunner.cs:250-327`; PG: `PostgreSqlBoxMigrationRunner.cs:271-354` | `When_mssql_deployment_flips_from_global_to_per_schema_it_should_not_re_run_applied_migrations.cs` (+ PG) | ✓ |
| FR5 (permission) | Legacy-read failure → ConfigurationException | MSSQL `:290-306`, PG `:315-331` (try/catch); also pre-lock hint catch in `SqlBoxProvisioner.cs:174-180/203-209` | `When_per_schema_flip_cannot_read_legacy_history_table_mssql_runner_should_throw_clear_error.cs` (+ PG) | ✓ |
| FR6 / AC4 | Two-tenant independence | resolution keys off `SchemaName` (already true post-T3/T4) | `When_two_mssql_tenants_use_per_schema_scope_each_should_get_independent_history.cs` (+ PG) | ✓ |
| NF2 / AC3 | Idempotent under PerSchema | NOT EXISTS PK guard in seed + detection short-circuit | `When_mssql_per_schema_provisioning_runs_twice_it_should_be_idempotent.cs` (+ PG) | ✓ |
| NF3 / AC6 | Identifier safety preserved | `SqlBoxProvisioner.cs:105-110` validates SchemaName upfront; `Identifiers.AssertSafe` on every new qualified ref; PG via `PgIdentifier.Quote` | `When_per_schema_scope_is_selected_with_an_unsafe_schema_name_mssql_runner_should_throw.cs` (+ PG) | ✓ |
| NF5 / AC7 / D6 | Per-run log + seed structured fields + Activity tag | `SqlBoxMigrationRunner.cs:234-236`; seed log MSSQL `:316-318`, PG `:343-345`; tag `BoxMigrationSeedRowCount` | `When_provisioning_runs_mssql_runner_should_log_resolved_history_schema_and_scope.cs` (two facts including seed) | ✓ |
| AC1b | PerSchema no-op on MySQL/SQLite/Spanner | `MySqlBoxMigrationRunner.cs:100` (DefaultHistorySchema=null, SupportsPerSchemaHistory defaults to false); same SQLite; Spanner doesn't derive | three files (MySQL/SQLite/Spanner) | ✓ |
| ADR Errata (D4) | `DetectCurrentVersionAsync` does **not** gain `historySchema` | `IAmABoxMigrationDetectionHelper.cs:124` adds historySchema; DetectCurrentVersionAsync unchanged (in `IAmAVersionDetectingMigrationHelper`) | covered by `git diff` (interface file untouched) | ✓ |

### TDD ordering verification
- S1 structural is its own commit (`afc5cae3f`) ahead of all behavioural slices. ✓
- T1 has separate RED test commit (`a0d624241`) → GREEN impl (`7938c56ab`) — matches the test-first protocol explicitly. ✓
- T3, T4, T8, T9, T-PERM, AC7 fold test + impl into single `feat` commits. The user-owned approval gate (CLAUDE.md TDD rule) is enforced by the author, not by the commit shape, so this is procedurally acceptable for the review; the tests inside those commits are real-DB integration tests asserting the right behaviour.
- T5, T6, T7, T_TENANTS, NF3/AC6 are pure `test` commits (no prod-code change beyond S1/T3/T4 was needed). ✓

### Cross-backend consistency
- MSSQL and PG seed shapes are parallel: pre-create probe → CREATE → INSERT…SELECT…NOT EXISTS → try/catch → `ConfigurationException` with identical phrase "the first Global → PerSchema run requires read access to the legacy default-schema history table". ✓
- PG additionally needs `PgIdentifier.Quote`/`Normalize` folding on every reference; verified at MSSQL line `:265-266` ↔ PG line `:288-289` for AssertSafe, and PG identifier folding at write `:309-310` ↔ read `:164-165` `:217-218`. ✓
- MySQL/SQLite/Spanner stay `SupportsPerSchemaHistory => false` (inherited default), so D3 guard is gated off and `ResolveHistorySchema()` returns `DefaultHistorySchema` (null for MySQL/SQLite). ✓

### Scope creep check
- `BrighterSemanticConventions.BoxMigrationSeedRowCount` (`:52`) — traces to D6.
- `SpannerInboxProvisioner.cs` / `SpannerOutboxProvisioner.cs` / `SpannerBoxDetectionHelper.cs` — minimal interface-conformance updates required by the S1 seam change. ✓
- No spurious binaries / build artefacts in the diff.

**Net verdict**: Implementation is faithful to spec 0029 and ADR 0060, including the D4 Errata. Tests are real-container integration tests with strong evident-data assertions. No findings at or above threshold.

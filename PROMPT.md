# Resume State — Spec 0030 primitive_obsession

**Last updated:** 2026-06-08
**Branch:** `primitive_obsession`
**Spec dir:** `specs/0030-primitive_obsession/`  ·  `specs/.current-spec` = `0030-primitive_obsession`
**Issue:** #4164  ·  ADR: `docs/adr/0061-box-provisioning-value-types.md`
**HEAD:** `4cb46c41d`

## Where we are in the workflow

Issue → **Requirements ✅** → **Design (ADR 0061) ✅** → **Tasks ✅** → **Tests/Code 🔄** → Code Review

## Phase 1 — COMPLETE ✅  (111/111 tests, net9.0)

All six value types implemented and committed:

| Commit | What |
|---|---|
| `6262cc15d` | fix: resolve CS8604/CS8601 call-site nullability in `Paramore.Brighter` (boy-scout) |
| `56f77029a` | feat(spec-0030): `BoxTableName` round-trip + value equality (4 tests) |
| `056204fc7` | feat(spec-0030): `BoxTableName.IsNullOrEmpty` (3 tests) |
| `30e7ea169` | feat(spec-0030): `SchemaName` round-trip + nullable + `IsNullOrEmpty` (8 tests) |
| `2efa24466` | feat(spec-0030): `MigrationDescription` round-trip + `IsNullOrEmpty` (7 tests) |
| `e8f854397` | feat(spec-0030): `SqlScript` round-trip + nullable + no-validation + `IsNullOrEmpty` (9 tests) |
| `a92ffc70c` | feat(spec-0030): `SourceReference` round-trip + nullable + `IsNullOrEmpty` (8 tests) |
| `4cb46c41d` | feat(spec-0030): `MigrationVersion` round-trip + int arithmetic + `IComparable` (7 tests) |

New type files (all in `src/Paramore.Brighter.BoxProvisioning/`):
- `BoxTableName.cs`, `SchemaName.cs`, `MigrationDescription.cs`
- `SqlScript.cs`, `SourceReference.cs`, `MigrationVersion.cs`

---

## Current phase: Phase 2 — Retype contracts

> ADR 0061 step 2. **Purely structural** — no behaviour changes. Commit separately from any behaviour work.

### Task 2a — Retype `IAmABoxMigration` and `BoxMigration`

Files to edit:
- `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigration.cs`
- `src/Paramore.Brighter.BoxProvisioning/BoxMigration.cs`

Changes:
- `int Version` → `MigrationVersion`
- `string Description` → `MigrationDescription`
- `string UpScript` → `SqlScript`
- `string? SourceReference` → `SourceReference?`
- `string? IdempotencyCheckSql` → `SqlScript?`
- `LogicalColumns` stays `IReadOnlyCollection<string>` — do NOT change

Verify existing `new BoxMigration(1, "Add Source", "ALTER TABLE …", new[] { "Source" })` call sites in the four relational catalog assemblies compile unchanged via implicit conversions (no argument changes needed).

### Task 2b — Retype `IAmABoxMigrationRunner.MigrateAsync` + fix D4 ternary

Files to edit:
- `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationRunner.cs`
- `src/Paramore.Brighter.BoxProvisioning/SqlBoxMigrationRunner.cs`

Changes:
- `MigrateAsync(string tableName, string? schemaName, …)` → `MigrateAsync(BoxTableName tableName, SchemaName? schemaName, …)`
- **D4 ternary** at `SqlBoxMigrationRunner.cs:285`: change to `migrations.Count == 0 ? (MigrationVersion)0 : migrations[migrations.Count - 1].Version` (resolves CS0172 bidirectional-implicit ambiguity)
- `Identifiers.AssertSafe(tableName, …)` and `AssertSafe(schemaName, …)` lines ~191/194 pass `string?` via implicit conversion — resolve any CS8604 with `.Value` or `!`, do NOT revert operator return type
- `SqlBoxProvisioner` call `_migrationRunner.MigrateAsync(BoxTableName, _configuration.SchemaName, …)` compiles via implicit `string? → SchemaName?`

### Task 2c — Retype `IAmABoxProvisioner.BoxTableName`

Files to edit:
- `src/Paramore.Brighter.BoxProvisioning/IAmABoxProvisioner.cs`
- `src/Paramore.Brighter.BoxProvisioning/SqlBoxProvisioner.cs` (line ~105)

Changes:
- `string BoxTableName` property → `BoxTableName`
- `SqlBoxProvisioner` derives property from `_configuration.OutBoxTableName`/`InBoxTableName` (core `string`) — compiles via implicit conversion, no core changes
- `Identifiers.AssertSafe(BoxTableName, …)` at line ~105 still compiles via implicit `→ string?`; resolve CS8604 with `.Value` or `!`

### Task 2d — Update test doubles to new types (mechanical, no behaviour)

Files to edit (exhaustive list):

**`IAmABoxMigration` implementers** (update property declarations only):
- `tests/.../When_relational_box_migration_runner_base_migrate_receives_non_monotonic_migrations_it_should_throw_before_opening_connection.cs` (`StubBoxMigration`)
- `tests/.../When_relational_box_migration_runner_base_migrate_receives_unsafe_identifier_it_should_throw_before_opening_connection.cs` (`StubBoxMigration`)

**`IAmABoxMigrationRunner` direct implementers** (update `MigrateAsync` signature only):
- `tests/.../When_sql_box_provisioner_detect_table_state_inlines_negative_version_clamp.cs` (`VersionCapturingMigrationRunner`)
- `tests/.../When_sql_box_provisioner_effective_schema_name_is_overridden_it_should_propagate_to_detection_and_payload_calls_only.cs` (`SchemaCapturingMigrationRunner`)
- `tests/.../When_sql_box_provisioner_provision_async_receives_unsafe_identifier_it_should_throw_before_opening_connection.cs` (`ThrowingMigrationRunner`)
- `tests/.../When_sql_box_provisioner_provision_async_runs_successfully_it_should_invoke_hooks_in_documented_order.cs` (`RecordingMigrationRunner`)

**`IAmABoxProvisioner` implementer**:
- `tests/.../TestDoubles/StubBoxProvisioner.cs` (`BoxTableName` property type)

Do NOT modify: `StubBoxDetectionHelper.cs` (detection-helper, out of scope).

---

## Phase 3 — Backend compilability (after Phase 2)

Build all four relational backends (MsSql, MySql, PostgreSql, Sqlite) + Spanner against the retyped interfaces. Confirm compilation via implicit conversions — no argument changes expected. Also confirm null-path behaviour: SQLite null `SchemaName` and V2+ null `IdempotencyCheckSql` produce no NRE.

---

## Phase 4 — Cross-cutting verification (after Phase 3)

1. All TFMs compile (`netstandard2.0;net8.0;net9.0;net10.0`) — confirm no `netstandard2.0`-unavailable APIs
2. Identifier-validation regression: `1Outbox` → `ConfigurationException`; SQLite null schema → succeeds
3. Monotonicity regression: `[1,2,3]` passes; `[1,3]` → `ConfigurationException` with `V1 followed by V3 (expected V2)`
4. Full suite parity + telemetry/log string content unchanged
5. Scope guard: no new types leaked outside `Paramore.Brighter.BoxProvisioning`; core assembly unmodified

---

## Key implementation notes

- **No behaviour changes in Phase 2** — only type signatures change; call sites compile via implicit conversions
- **D4 ternary** is the one required explicit cast; it is pre-identified at `SqlBoxMigrationRunner.cs:285`
- **`AssertSafe` stays at call sites** — do NOT move validation into constructors (D3)
- **`LogicalColumns` unchanged** — stays `IReadOnlyCollection<string>` throughout (D5)
- **CS8604 pattern**: all string-backed operators return `string?`; when passing into non-nullable `AssertSafe(string, …)`, resolve with `.Value` or `!` at the call site (established codebase pattern)
- Phase 2 tasks 2a–2c should each be committed separately; 2d (test doubles) committed after 2c

## Test run command

```bash
dotnet test tests/Paramore.Brighter.BoxProvisioning.Tests/ --framework net9.0 --no-build -q
```

## Spec 0030 about

Replace bare `string`/`int` primitives in Box Provisioning public interfaces with six dedicated value-type `record`s modelled on `src/Paramore.Brighter/Id.cs`. Six types: `BoxTableName`, `SchemaName`, `MigrationDescription`, `SqlScript`, `SourceReference`, `MigrationVersion`. Implicit conversions preserve full source compatibility at every existing call site.

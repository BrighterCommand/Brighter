# Resume State — Spec 0030 primitive_obsession

**Last updated:** 2026-06-08
**Branch:** `primitive_obsession`
**Spec dir:** `specs/0030-primitive_obsession/`  ·  `specs/.current-spec` = `0030-primitive_obsession`
**Issue:** #4164  ·  ADR: `docs/adr/0061-box-provisioning-value-types.md`
**HEAD:** `30e7ea169`

## Where we are in the workflow

Issue → **Requirements ✅** → **Design (ADR 0061) ✅** → **Tasks ✅** → **Tests/Code 🔄** → Code Review

## Current phase: Phase 1 — Add value-type files

### Completed this session

| Commit | What |
|---|---|
| `6262cc15d` | fix: resolve CS8604/CS8601 call-site nullability in `Paramore.Brighter` (59 sites, 16 files, boy-scout follow-up to `cb3e4ef47`) |
| `56f77029a` | feat(spec-0030): `BoxTableName` — round-trip + value equality (4 tests) |
| `056204fc7` | feat(spec-0030): `BoxTableName.IsNullOrEmpty` (3 tests) |
| `30e7ea169` | feat(spec-0030): `SchemaName` — round-trip + nullable + `IsNullOrEmpty` (8 tests) |

**Baseline:** 65/65 → now **80/80** on net9.0 and net10.0.

### Next task (Phase 1, task 4)

```
/test-first MigrationDescription value type round-trips a string, ToString, equality, and IsNullOrEmpty
```

Test file: `tests/Paramore.Brighter.BoxProvisioning.Tests/When_a_migration_description_wraps_a_string_it_should_round_trip_the_value.cs`

Test should verify:
- `MigrationDescription d = "Add Source column"; string s = d;` yields `s == "Add Source column"` (FR-5)
- `ToString()` returns `Value`; equality holds for equal strings
- `IsNullOrEmpty` null/empty/non-empty cases (FR-3, AC-2)

Implementation: `src/Paramore.Brighter.BoxProvisioning/MigrationDescription.cs`
- Follow `BoxTableName.cs` template exactly
- **Null-safe operator** (critical): `public static implicit operator string?(MigrationDescription value) => value?.Value;`

### Remaining Phase 1 tasks (after MigrationDescription)

1. **`SqlScript`** — round-trip + nullable (`SqlScript?`) + `IsNullOrEmpty`
   - Test file: `When_a_sql_script_wraps_script_text_it_should_round_trip_the_value.cs`
   - Null-safe operator: `public static implicit operator string?(SqlScript value) => value?.Value;`

2. **`SourceReference`** — round-trip + nullable (`SourceReference?`) + `IsNullOrEmpty`
   - Test file: `When_a_source_reference_wraps_a_string_or_is_null_it_should_round_trip_the_value.cs`
   - Null-safe operator: `public static implicit operator string?(SourceReference value) => value?.Value;`

3. **`MigrationVersion`** — wraps `int`, `IComparable<MigrationVersion>`, arithmetic via implicit `→ int`
   - Test file: `When_a_migration_version_wraps_an_int_it_should_round_trip_and_support_arithmetic.cs`
   - Implementation: `int Value`, no `IsNullOrEmpty`
   - `public static implicit operator int(MigrationVersion v) => v.Value;`
   - `public static implicit operator MigrationVersion(int v) => new(v);`
   - `IComparable<MigrationVersion>` — `CompareTo`

### Phase 2 (after all Phase 1 types are done)

Retype contracts: `IAmABoxMigration`, `BoxMigration`, `IAmABoxMigrationRunner.MigrateAsync`, `IAmABoxProvisioner.BoxTableName`. Fix the one ternary at `SqlBoxMigrationRunner.cs:285` (`(MigrationVersion)0`).

### Phase 3 (after Phase 2)

Backend compilability: MsSql, MySql, PostgreSql, Sqlite, Spanner.

### Phase 4

Cross-cutting verification: all TFMs, identifier validation regression, monotonicity regression, full suite parity.

## Key implementation notes

- All new types live in `src/Paramore.Brighter.BoxProvisioning/` one file per type
- Template: `src/Paramore.Brighter.BoxProvisioning/BoxTableName.cs` (already written) — follow exactly
- MIT licence header, namespace `Paramore.Brighter.BoxProvisioning`, standalone `record` (no shared base)
- **Null-safe operator** for all string-backed types: `operator string?(T value) => value?.Value;`
- `IsNullOrEmpty` uses `[NotNullWhen(false)]` from `System.Diagnostics.CodeAnalysis`
- `.gitignore` quirk: `*.sqlite` matches `*.Sqlite` directories on macOS case-insensitive FS — new files in `src/Paramore.Brighter.BoxProvisioning/` need `git add -f` when adding new files (per CLAUDE.md)

## Test run command

```bash
dotnet test tests/Paramore.Brighter.BoxProvisioning.Tests/ --framework net9.0 --no-build -q
```

## Spec 0030 about

Replace bare `string`/`int` primitives in Box Provisioning public interfaces with six dedicated value-type `record`s modelled on `src/Paramore.Brighter/Id.cs`. Six types: `BoxTableName`, `SchemaName`, `MigrationDescription`, `SqlScript`, `SourceReference`, `MigrationVersion`. Implicit conversions preserve full source compatibility at every existing call site.

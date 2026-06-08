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

**Baseline:** 65/65 → now **111/111** on net9.0.

## Phase 1 COMPLETE ✅

| Commit | What |
|---|---|
| `56f77029a` | feat(spec-0030): `BoxTableName` round-trip + value equality |
| `056204fc7` | feat(spec-0030): `BoxTableName.IsNullOrEmpty` |
| `30e7ea169` | feat(spec-0030): `SchemaName` round-trip + nullable + `IsNullOrEmpty` |
| `2efa24466` | feat(spec-0030): `MigrationDescription` round-trip + `IsNullOrEmpty` |
| `e8f854397` | feat(spec-0030): `SqlScript` round-trip + nullable + no-validation + `IsNullOrEmpty` |
| `a92ffc70c` | feat(spec-0030): `SourceReference` round-trip + nullable + `IsNullOrEmpty` |
| *(pending)* | feat(spec-0030): `MigrationVersion` round-trip + int arithmetic + IComparable |

## Next: Phase 2 — Retype contracts

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

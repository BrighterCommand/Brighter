# 61. Box Provisioning value types

Date: 2026-06-02

## Status

Accepted

## Context

The Box Provisioning subsystem (`Paramore.Brighter.BoxProvisioning` and its backend assemblies: MySql, PostgreSql, MsSql, Sqlite, Spanner) exposes its user-facing contracts through bare `string`/`int` primitives. This is classic primitive obsession: same-typed parameters sit adjacent in signatures with nothing but argument order and parameter names to distinguish them, so the compiler cannot catch a transposition.

**Parent Requirement**: [specs/0030-primitive_obsession/requirements.md](../../specs/0030-primitive_obsession/requirements.md)

**Scope**: This ADR focuses specifically on introducing dedicated value types to replace the user-facing primitives that flow through the Box Provisioning interfaces and their concrete record/implementations. It does not touch the core `Paramore.Brighter` assembly, the internal `LogicalColumns` detection mechanism, or `IAmABoxMigrationDetectionHelper<,>`.

The concrete pain points, verified against the current source:

- `IAmABoxMigrationRunner.MigrateAsync(string tableName, string? schemaName, BoxType boxType, BoxTableState tableState, CancellationToken)` (`src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationRunner.cs`) takes `tableName` and `schemaName` as two adjacent strings. Transposing them produces no compiler error — the bug surfaces only at runtime as a wrong-schema or wrong-table DDL operation.
- `IAmABoxMigration` (`src/Paramore.Brighter.BoxProvisioning/IAmABoxMigration.cs`) exposes `Version` (`int`), `Description` (`string`), `UpScript` (`string`), `SourceReference` (`string?`), and `IdempotencyCheckSql` (`string?`) — five members of which four are strings, undifferentiated at the `new BoxMigration(...)` call site (`src/Paramore.Brighter.BoxProvisioning/BoxMigration.cs`).
- `IAmABoxProvisioner.BoxTableName` is a bare `string` (`src/Paramore.Brighter.BoxProvisioning/IAmABoxProvisioner.cs`).

Brighter already models this idea for identifiers via `src/Paramore.Brighter/Id.cs`: a standalone `record` wrapping a `string`, with a `Value` property, a public constructor, implicit conversions in both directions, an overridden `ToString()`, and a static `IsNullOrEmpty([NotNullWhen(false)] Id?)` helper. The Box Provisioning value types follow this template so the codebase has a single, recognisable convention.

Two constraints shape the design:

1. **Source compatibility.** The four relational backends, Spanner, the catalogs, and the core `IAmARelationalDatabaseConfiguration` (whose `SchemaName` stays `string?` and whose `InBoxTableName`/`OutBoxTableName` stay `string`) must continue to compile and run with no argument changes at any call site.
2. **Validation placement.** `Identifiers.AssertSafe` (`src/Paramore.Brighter.BoxProvisioning/Identifiers.cs`) is a defence-in-depth pattern invoked at approximately thirty call sites across the BoxProvisioning assemblies: the provisioner and runner entry points, all four backend catalogs, all four backend detection helpers, all four per-backend runners, and the Spanner runner. These calls must keep firing exactly where they fire today, not on every intermediate string-to-type conversion inside the infrastructure.

## Decision

Introduce six standalone `record` value types in namespace `Paramore.Brighter.BoxProvisioning`, one file per type, each modelled on `Id.cs`, and retype the user-facing members of `IAmABoxMigration`/`BoxMigration`, `IAmABoxMigrationRunner.MigrateAsync`, and `IAmABoxProvisioner.BoxTableName` to use them. Implicit conversions to and from the underlying primitive preserve source compatibility at every existing call site. `Identifiers.AssertSafe` stays exactly where it is today, operating on the underlying string (via implicit conversion or `.Value`); it is **not** moved into the value-type constructors.

### Architecture Overview

The value types are pure **information holders** in Responsibility-Driven Design terms: they *know* the value they wrap and *know* how to render and compare themselves, but they do **not** *decide* whether a value is a safe SQL identifier. That decision remains the responsibility of the provisioner and runner (the deciders), which already call `Identifiers.AssertSafe` at their public entry points. This separation is the crux of decision (3) below.

```
┌─────────────────────────────────────────────────────────────────┐
│                 Paramore.Brighter.BoxProvisioning               │
│                                                                 │
│  Value types (information holders)                              │
│  ┌─────────────────┐  ┌─────────────────┐  ┌────────────────┐  │
│  │  BoxTableName   │  │   SchemaName    │  │MigrationVersion│  │
│  │  Value: string  │  │  Value: string  │  │  Value: int    │  │
│  │  ↔ string       │  │  ↔ string       │  │  ↔ int         │  │
│  └─────────────────┘  └─────────────────┘  └────────────────┘  │
│  ┌──────────────────────┐  ┌───────────┐  ┌─────────────────┐  │
│  │ MigrationDescription │  │ SqlScript │  │ SourceReference │  │
│  │  Value: string       │  │ Value:str │  │  Value: string  │  │
│  │  ↔ string            │  │ ↔ string  │  │  ↔ string       │  │
│  └──────────────────────┘  └───────────┘  └─────────────────┘  │
│                                                                 │
│  Contracts (retyped)                                            │
│  IAmABoxMigration      IAmABoxMigrationRunner  IAmABoxProvisioner│
│  └── Version:MigrationVersion    └── MigrateAsync(             │
│      Description:MigrationDescription  BoxTableName tableName, │
│      UpScript:SqlScript                SchemaName? schemaName,  │
│      SourceReference:SourceReference?  …)                       │
│      IdempotencyCheckSql:SqlScript?                             │
│      LogicalColumns:IReadOnlyCollection<string> ← unchanged     │
│                                                                 │
│  Deciders (unchanged in validation logic)                       │
│  SqlBoxProvisioner / SqlBoxMigrationRunner                      │
│  └── Identifiers.AssertSafe(tableName.Value / (string)tableName)│
└─────────────────────────────────────────────────────────────────┘
```

Each value type is a thin wrapper that adds compile-time distinctness with zero runtime behaviour change: an implicit conversion is a field read or a `new`, the JIT inlines it, and on hot paths the wrapper carries no allocation beyond what the underlying string already incurred.

### Key Components

Six new files in `src/Paramore.Brighter.BoxProvisioning/`:

| File | Type | Wraps | Nullable in use |
|---|---|---|---|
| `BoxTableName.cs` | `BoxTableName` | `string` | no |
| `SchemaName.cs` | `SchemaName` | `string` | yes (`SchemaName?` — SQLite has no schema) |
| `MigrationVersion.cs` | `MigrationVersion` | `int` | no |
| `MigrationDescription.cs` | `MigrationDescription` | `string` | no |
| `SqlScript.cs` | `SqlScript` | `string` | up-script no; idempotency-check `SqlScript?` |
| `SourceReference.cs` | `SourceReference` | `string` | yes (`SourceReference?`) |

Every type, per FR-13, is a standalone `record` (no shared base record), with: the MIT licence header, a `Value` property, a public constructor accepting the underlying primitive, implicit operators in both directions, an overridden `ToString()` returning `Value` (for the five string-backed types) or `Value.ToString()` (for `MigrationVersion`, whose `Value` is `int`), XML documentation describing what the value represents, and value equality (provided for free by `record`).

The five string-backed types additionally expose `static bool IsNullOrEmpty([NotNullWhen(false)] T?)` (FR-3).

`MigrationVersion` additionally implements `IComparable<MigrationVersion>` and exposes `Value` of type `int` (FR-4, included for future extensibility).

Modified contracts and implementations:

- `IAmABoxMigration` / `BoxMigration` (FR-8): `Version → MigrationVersion`, `Description → MigrationDescription`, `UpScript → SqlScript`, `SourceReference → SourceReference?`, `IdempotencyCheckSql → SqlScript?`. `LogicalColumns` remains `IReadOnlyCollection<string>`.
- `IAmABoxMigrationRunner.MigrateAsync` (FR-9): `tableName → BoxTableName`, `schemaName → SchemaName?`.
- `IAmABoxProvisioner.BoxTableName` (FR-10): retyped to `BoxTableName`. Log interpolation via `ToString()` still renders the underlying string.

Explicitly **unchanged**: `LogicalColumns` (`IReadOnlyCollection<string>`), `IAmABoxMigrationDetectionHelper<,>` parameter types, `Identifiers` itself, and the core `Paramore.Brighter` assembly.

### Technology Choices

**D1 — `record` over `struct` or `class`.**  
Matches `Id.cs` exactly. Gives value equality and a clean deconstruction surface for free. The existing `BoxMigration` is already a `record`, so the convention is established. A `readonly struct` would avoid the reference allocation but gives different nullable semantics (`Nullable<SchemaName>` vs nullable reference, complicating the "`null` means not supplied" model) and provides an allocation benefit irrelevant on the startup-only provisioning path (NFR-4).

**D2 — Implicit operators in both directions.**  
`T → primitive` lets the value flow into `Identifiers.AssertSafe`, `string`-typed config properties, SQL builders, and log interpolation untouched. `primitive → T` lets every existing call site that passes a string/int literal keep doing so (NFR-1, zero argument changes). This is the mechanism that delivers source compatibility.

**D3 — `Identifiers.AssertSafe` stays at call sites, not in constructors.**  
Constructors run on every implicit `string → T` conversion — including internal SQL builders, lock-resource strings, log interpolation, and the legitimate `SchemaName? s = (string?)null` case for SQLite. Moving `AssertSafe` into the constructor would fire outside the user-facing boundary and reject legitimate intermediate conversions. Validation logic stays at all existing `AssertSafe` call sites (defence-in-depth across provisioner, runner entry points, catalogs, detection helpers, and per-backend runners); any future move is a separate, behavioural commit (FR-11, Tidy First).

**D4 — `MigrationVersion ↔ int` bidirectional implicits cover all call sites; one pattern requires mechanical disambiguation.**  
The implicit `MigrationVersion → int` operator handles arithmetic (`prev + 1`, C# applies user-defined implicit conversions when resolving binary operators) and `int`-parameter binding (`RunFreshPathAsync(…, int latestVersion, …)` at `SqlBoxMigrationRunner.cs:418`). However, bidirectional implicits (`int → MigrationVersion` *and* `MigrationVersion → int`) create CS0172 ("Type of conditional expression cannot be determined because 'int' and 'MigrationVersion' implicitly convert to one another") in one pattern in the current code:

- **Ternary expression** at `SqlBoxMigrationRunner.cs`: `var latestVersion = migrations.Count == 0 ? 0 : migrations[migrations.Count - 1].Version;` — branch types are `int` and `MigrationVersion`; with implicits in both directions the compiler cannot pick a common type. Fix: `? (MigrationVersion)0 : migrations[migrations.Count - 1].Version`.

This is the only mechanical edit required — a single cast addition in the structural commit.

All other `MigrationVersion`/`int` interaction patterns compile unchanged. Overload resolution selects the `int`-vs-`int` candidate via the implicit `MigrationVersion → int` in each case, with no surviving competing candidate:

- **Relational comparisons** (`<=`/`<`/`>`/`>=`): `migration.Version <= detected` and `migration.Version <= maxVersion` in all four backend runners (`MsSqlBoxMigrationRunner.cs`, `MySqlBoxMigrationRunner.cs`, `PostgreSqlBoxMigrationRunner.cs`, `SqliteBoxMigrationRunner.cs`) where `detected`/`maxVersion` are `int`. `record` and `IComparable<MigrationVersion>` synthesize no `<=`/`>=` operators, so the predefined `int <= int` is the only candidate.
- **Equality/inequality comparisons** (`==`/`!=`): `migration.Version == brokenVersion` in the four `BrokenMigrationFactory` test doubles, and `curr != prev + 1` in `SqlBoxMigrationRunner.ValidateMigrationsMonotonic`. Although `record` synthesizes a `==` operator, C# overload resolution betterness rules select the predefined `int == int` candidate via the implicit conversion — no cast required.

`IComparable<MigrationVersion>` is included per FR-4, with no current direct call site; its value is that it makes the type correctly ordered-by-contract for future consumers.

**D5 — `LogicalColumns` remains `IReadOnlyCollection<string>`.**  
`HashSet<string>.IsSupersetOf(LogicalColumns)` in every backend detection helper requires `IEnumerable<string>`. No covariant conversion exists for user-defined element wrappers, so changing the element type to `LogicalColumnName` would break compilation in every detection helper. The internal column-name set is not a user-facing identifier; it carries no documentation or distinctness value from wrapping. Excluded from scope.

**D6 — Nullable semantics on the wrapping reference type.**  
`SchemaName?`, `SourceReference?`, `SqlScript?` — the `?` is on the wrapping reference type, so `null` cleanly means "not supplied," matching the existing `string?` semantics. SQLite has no schema; V1 migrations have no source reference.

**D7 — `netstandard2.0` compatibility confirmed.**  
`Paramore.Brighter.BoxProvisioning.csproj` targets `netstandard2.0;net8.0;net9.0;net10.0` with `LangVersion=latest`. The existing `BoxMigration` record in this same assembly already compiles on all four TFMs, demonstrating the `record`-on-netstandard2.0 configuration works before any new code is added. `[NotNullWhen]` is consumed by `Id.cs` and other netstandard2.0 code in the repo today.

### Implementation Approach

Following **Tidy First**, this is a purely structural change (renaming/retyping at the interface boundary) and must not be mixed with any behavioural change in the same commit.

1. **Add the six value-type files** to `src/Paramore.Brighter.BoxProvisioning/`. This commit adds types only; nothing references them, so it compiles in isolation and can be reviewed independently.

2. **Retype the contracts**: `IAmABoxMigration`, `BoxMigration`, `IAmABoxMigrationRunner.MigrateAsync`, `IAmABoxProvisioner.BoxTableName`. The six string-backed types (`BoxTableName`, `SchemaName`, etc.) compile everywhere without argument changes — their implicit operators handle `AssertSafe` calls, DDL interpolation, and config property binding. `MigrationVersion` also compiles in arithmetic, parameter-binding, and comparison positions without change. One call site needs a mechanical cast addition to resolve bidirectional-implicit ambiguity (D4): the ternary initialising `latestVersion` in `SqlBoxMigrationRunner` (`? (MigrationVersion)0 : …`). This is a structural edit in this same commit. Note: `IAmABoxProvisioner.BoxTableName` is a property whose name equals its new type (`public BoxTableName BoxTableName => …`); C# permits this and it compiles correctly, but readers should be aware it is intentional.

3. **Verify, do not modify, the `AssertSafe` call sites.** Confirm each still compiles against the retyped members. No `AssertSafe` call moves, is added, or is removed (D3, FR-11, NFR-3).

## Consequences

### Positive

- **Compiler-enforced parameter distinctness.** `MigrateAsync(BoxTableName, SchemaName?, …)` cannot be called with the table and schema transposed — the two are now different types. The most dangerous current ambiguity is eliminated.
- **Self-documenting contracts.** `new BoxMigration(version, description, upScript, columns, sourceRef, idempotencyCheck)` now has each argument's intent encoded in its type, and each type carries XML doc describing what it represents (NFR-5).
- **Ergonomic emptiness checks.** `BoxTableName.IsNullOrEmpty(t)` replaces `string.IsNullOrEmpty(t.Value)` — behaviour lives on the type (FR-3).
- **Single recognised convention.** The Box Provisioning types mirror `Id.cs`; a contributor who knows one knows all.
- **Zero migration cost for consumers.** Implicit conversions mean existing user code that passes strings/ints keeps compiling (NFR-1); adoption of the typed surface is incremental.

### Negative

- **Six new files, near-duplicate boilerplate.** Mitigated by the strict one-pattern rule (FR-13, D1) — there is no per-type cleverness.
- **Implicit conversions can mask intent.** `string → BoxTableName` is implicit, so a stray string still flows in silently. The types prevent *transposition* between distinct typed parameters, not *accidental construction* from an arbitrary string. This is the deliberate cost of NFR-1.
- **Reference allocation per value.** Each `record` instance is a heap allocation. Accepted because provisioning types live only on the startup path, never in per-row hot paths (NFR-4).
- **One internal `MigrationVersion`/`int` site requires a mechanical cast addition.** Bidirectional implicit operators create CS0172 in the ternary expression initialising `latestVersion` in `SqlBoxMigrationRunner` (`migrations.Count == 0 ? 0 : …`). This is a single-line cast addition with no behaviour change, included in the interface-retyping structural commit (D4). All other `MigrationVersion`/`int` interactions — arithmetic, parameter binding, equality, and relational comparisons — compile without casts.

### Risks and Mitigations

- **Risk: validation fires on every implicit conversion if moved into constructors.** Mitigation: D3 is explicit that `AssertSafe` stays at the existing call sites. Any future move is a separate, behavioural commit.
- **Risk: bidirectional `MigrationVersion ↔ int` implicits create CS0172 in the `latestVersion` ternary.** Only one site is affected: the ternary initialising `latestVersion` in `SqlBoxMigrationRunner`. Mitigation: a single-line cast `(MigrationVersion)0` (structural edit in the retyping commit, D4). All other patterns — arithmetic, `int`-parameter binding, equality/inequality, and relational comparisons — compile without casts; see D4 for the reasoning.
- **Risk: `LogicalColumns` retyping cascades into detection helpers.** Mitigation: `LogicalColumns` is explicitly excluded (D5, FR-8, FR-12, Out of Scope in requirements).
- **Risk: nullability semantics drift.** Mitigation: `?` is on the wrapping reference type, not inside the record, preserving the `null` = "not supplied" semantic (D6).
- **Risk: `netstandard2.0` compile failure.** Mitigation: the existing `BoxMigration` record in the same assembly already proves the configuration works (D7, NFR-2).

## Alternatives Considered

- **`readonly struct` value types.** Rejected: diverges from `Id.cs` (FR-13), gives `Nullable<T>` semantics that complicate the "`null` means not supplied" model, provides allocation benefit irrelevant on the startup path.
- **Shared base `record` (e.g. `StringValue`).** Rejected: couples the types, leaks cross-type conversion ambiguity, defeats the compile-time distinctness goal. Each type is standalone (FR-13, D1).
- **Moving `Identifiers.AssertSafe` into constructors.** Rejected for this ADR: runs on every implicit conversion, including legitimate null-schema conversions for SQLite and internal infrastructure paths (D3, FR-11). May be revisited as a separate, behavioural change.
- **`LogicalColumnName` for `LogicalColumns` elements.** Rejected: breaks `HashSet<string>.IsSupersetOf` in every detection helper for no user-visible benefit (D5). Out of scope per requirements.
- **Explicit (rather than implicit) conversions.** Rejected: would force edits at every existing call site, violating NFR-1.

## References

- Requirements: [specs/0030-primitive_obsession/requirements.md](../../specs/0030-primitive_obsession/requirements.md)
- Linked issue: #4164
- Pattern source: `src/Paramore.Brighter/Id.cs`
- Validation: `src/Paramore.Brighter.BoxProvisioning/Identifiers.cs`
- Related ADRs:
  - `docs/adr/0053-box-database-migration.md`
  - `docs/adr/0057-box-schema-versioning-and-migrations.md`
  - `docs/adr/0058-box-provisioning-rdd-role-interfaces.md`
  - `docs/adr/0059-box-provisioning-abstract-base-naming-symmetry.md`
  - `docs/adr/0060-multi-tenancy-migration-history-scope.md`
- Design principles: `.agent_instructions/design_principles.md` (Responsibility-Driven Design, avoid primitive obsession, Tidy First)

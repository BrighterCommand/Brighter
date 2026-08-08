# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4164

## Problem Statement

As a developer extending or consuming Brighter's Box Provisioning subsystem (`Paramore.Brighter.BoxProvisioning` and its backend assemblies), I would like the table name, schema name, migration metadata, and SQL-script parameters that flow through provisioning interfaces to be expressed as dedicated value types rather than bare `string`/`int` primitives, so that the compiler tells me which argument is which, I cannot silently transpose two same-typed parameters, and the meaning of each parameter is documented on the type itself.

The concrete pain points in the current code:

- `IAmABoxMigrationRunner.MigrateAsync(string tableName, string? schemaName, BoxType boxType, BoxTableState tableState, …)` takes a `tableName` and a `schemaName` that are both strings. Calling code can transpose them with no compiler error; the bug only surfaces at runtime as a wrong-schema or wrong-table DDL operation.
- `IAmABoxMigration` exposes `Version` (int), `Description`, `UpScript`, `SourceReference`, and `IdempotencyCheckSql` all as primitives. There is nothing distinguishing a description string from a SQL script string from a source-reference string at a call site such as `new BoxMigration(...)`.
- `IAmABoxProvisioner.BoxTableName` is a bare `string`.

Brighter's coding style prefers to avoid primitive obsession and already models this idea for identifiers via `src/Paramore.Brighter/Id.cs`.

## Proposed Solution

Introduce a set of value types in the `Paramore.Brighter.BoxProvisioning` namespace/assembly that wrap the user-facing primitives currently used across the Box Provisioning interfaces and their concrete record/implementations. Each value type:

- Wraps a single underlying primitive and exposes it through a `Value` property (matching the `Id.cs` convention).
- Provides a public constructor accepting the underlying primitive.
- Provides implicit conversions to and from the underlying primitive so that existing call sites, backend implementations, and the core `IAmARelationalDatabaseConfiguration` (which keeps `string` table/schema properties) continue to compile and work with minimal edits.
- For string-backed types, exposes a static null-or-empty check helper (mirroring `Id.IsNullOrEmpty`) so callers can test emptiness ergonomically.
- Carries XML documentation describing what the value represents, so the type itself documents the contract.

After this change, the provisioning interface signatures read as typed contracts (for example, `MigrateAsync(BoxTableName tableName, SchemaName? schemaName, …)`), and a developer cannot accidentally pass a schema where a table name is expected without a compiler error, while existing string-passing code keeps working through the implicit conversions.

The value types proposed (final names to be settled in the ADR):

| Wraps | Underlying | Proposed value type | Nullable in use |
|---|---|---|---|
| Box table name | `string` | `BoxTableName` | no |
| Database schema name | `string` | `SchemaName` | yes (SQLite has no schema) |
| Migration version | `int` | `MigrationVersion` | no |
| Migration description | `string` | `MigrationDescription` | no |
| SQL up-script / idempotency-check SQL | `string` | `SqlScript` | up-script no; idempotency-check yes |
| Migration source reference | `string` | `SourceReference` | yes |

`LogicalColumns` (`IReadOnlyCollection<string>`) is intentionally excluded — it is an internal version-detection mechanism consumed by `HashSet<string>` superset matching with backend-specific case comparers, not a user-facing identifier. Changing its element type would break compilation across all detection helpers without user-visible benefit. See Out of Scope.

## Requirements

### Functional Requirements

**FR-1 — `BoxTableName` value type.** A `BoxTableName` value type SHALL exist in namespace `Paramore.Brighter.BoxProvisioning`, wrapping a `string`, with a public constructor accepting that string, a `Value` property returning it, implicit conversions `BoxTableName → string` and `string → BoxTableName`, an overridden `ToString()` returning `Value`, and value equality semantics.
- Example: `BoxTableName t = "Outbox"; string s = t;` compiles; `t.Value == "Outbox"`; `s == "Outbox"`; `t.ToString() == "Outbox"`.
- Example: `new BoxTableName("Outbox") == (BoxTableName)"Outbox"` is `true`.

**FR-2 — `SchemaName` value type.** A `SchemaName` value type SHALL exist in `Paramore.Brighter.BoxProvisioning`, wrapping a `string`, with a public constructor, `Value`, implicit conversions to/from `string`, `ToString()` returning `Value`, and value equality. It SHALL be usable as a nullable (`SchemaName?`) at every call site where the current schema parameter is `string?` (SQLite/Spanner pass no schema).
- Example: `SchemaName? sn = null;` is legal; a SQLite provisioning run passes `null` and is not rejected as malformed.
- Example: `SchemaName sn = "dbo"; string s = sn;` yields `s == "dbo"`.

**FR-3 — String value types expose a null-or-empty helper.** Every string-backed value type introduced by this change (`BoxTableName`, `SchemaName`, `MigrationDescription`, `SqlScript`, `SourceReference`) SHALL expose a static method named `IsNullOrEmpty`, consistent with `Id.IsNullOrEmpty`, that returns `true` when the supplied instance is `null` or wraps a null/empty string and `false` otherwise. The parameter SHALL be annotated `[NotNullWhen(false)]`.
- Example: `BoxTableName.IsNullOrEmpty(null) == true`; `BoxTableName.IsNullOrEmpty((BoxTableName)"") == true`; `BoxTableName.IsNullOrEmpty((BoxTableName)"Outbox") == false`.

**FR-4 — `MigrationVersion` value type.** A `MigrationVersion` value type SHALL exist in `Paramore.Brighter.BoxProvisioning`, wrapping an `int`, with a public constructor, a `Value` property of type `int`, implicit conversions `MigrationVersion → int` and `int → MigrationVersion`, an overridden `ToString()`, and value equality. The implicit `MigrationVersion → int` operator SHALL apply in arithmetic and comparison expressions, so that `var prev = migration.Version; prev + 1` evaluates as `int` arithmetic and `var latestVersion = migrations.Last().Version` round-trips into `int` method parameters without an explicit cast. `MigrationVersion` SHALL implement `IComparable<MigrationVersion>` to support ordering.
- Example: `MigrationVersion v = 3; int i = v;` yields `i == 3`.
- Example: `var prev = (MigrationVersion)1; int next = prev + 1;` yields `next == 2`.
- Example: given migrations with versions `1, 2, 3`, `ValidateMigrationsMonotonic` still passes; given `1, 3`, it still throws `ConfigurationException` whose message contains the substring `V1 followed by V3 (expected V2)`.

**FR-5 — `MigrationDescription` value type.** A `MigrationDescription` value type SHALL exist wrapping a `string`, with a public constructor, `Value`, implicit conversions to/from `string`, `ToString()`, value equality, and the FR-3 null-or-empty helper.
- Example: `MigrationDescription d = "Add Source column"; string s = d;` yields `s == "Add Source column"`.

**FR-6 — `SqlScript` value type.** A `SqlScript` value type SHALL exist wrapping a `string`, with a public constructor, `Value`, implicit conversions to/from `string`, `ToString()`, value equality, and the FR-3 null-or-empty helper. It SHALL be the type of `IAmABoxMigration.UpScript` (non-null) and of `IAmABoxMigration.IdempotencyCheckSql` (nullable, `SqlScript?`). The `SqlScript` type itself does not validate or restrict content; it is a typed wrapper only.
- Example: `SqlScript up = "ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL"; string sql = up;` round-trips the script text unchanged.
- Example: a migration with `IdempotencyCheckSql == null` exposes `SqlScript? IdempotencyCheckSql` equal to `null`, and the runner's existing "guard folded into UpScript" branch behaves identically.

**FR-7 — `SourceReference` value type.** A `SourceReference` value type SHALL exist wrapping a `string`, with a public constructor, `Value`, implicit conversions to/from `string`, `ToString()`, value equality, and the FR-3 null-or-empty helper. It SHALL be usable as nullable (`SourceReference?`) because V1 migrations carry `null`.
- Example: `SourceReference? r = "a1b2c3d / #4039"; string? s = r;` yields `s == "a1b2c3d / #4039"`; a V1 migration's `SourceReference` is `null`.

**FR-8 — Update `IAmABoxMigration` and `BoxMigration` to value types.** The `IAmABoxMigration` interface and the `BoxMigration` record SHALL expose `Version` as `MigrationVersion`, `Description` as `MigrationDescription`, `UpScript` as `SqlScript`, `SourceReference` as `SourceReference?`, and `IdempotencyCheckSql` as `SqlScript?`. `LogicalColumns` SHALL remain `IReadOnlyCollection<string>` unchanged. Existing `new BoxMigration(...)` call sites that pass primitives for all parameters SHALL continue to compile unchanged through the implicit conversions.
- Example: `new BoxMigration(1, "Add Source", "ALTER TABLE …", new[] { "Source" })` compiles with no argument changes; the primitives convert implicitly.
- Example: `new BoxMigration(1, "V1", "CREATE TABLE …", cols, null, null)` compiles with `SourceReference` and `IdempotencyCheckSql` as `null` (nullable value types).

**FR-9 — Update `IAmABoxMigrationRunner.MigrateAsync` signature.** `MigrateAsync` SHALL declare `tableName` as `BoxTableName` and `schemaName` as `SchemaName?`, leaving `boxType`, `tableState`, and `cancellationToken` unchanged. The `SqlBoxProvisioner` call site `_migrationRunner.MigrateAsync(BoxTableName, _configuration.SchemaName, …)` SHALL continue to compile, with `_configuration.SchemaName` (a core `string?`) converting implicitly to `SchemaName?`.
- Example: `runner.MigrateAsync("Outbox", "dbo", BoxType.Outbox, state)` compiles unchanged because string literals convert implicitly.

**FR-10 — Update `IAmABoxProvisioner.BoxTableName`.** The `IAmABoxProvisioner.BoxTableName` property SHALL be typed `BoxTableName`. The `SqlBoxProvisioner` implementation that derives the value from `_configuration.OutBoxTableName` / `_configuration.InBoxTableName` (core `string` properties) SHALL continue to compile via implicit conversion, and `BoxProvisioningHostedService` log statements that interpolate `BoxTableName` SHALL still render the underlying string.
- Example: `logger.LogInformation("... {BoxTable}", provisioner.BoxTableName)` renders `Outbox`, not the type name.

**FR-11 — Preserve existing SQL-identifier validation.** The existing `Identifiers.AssertSafe` validation applied to table and schema names in `SqlBoxProvisioner.ProvisionAsync` and `SqlBoxMigrationRunner.MigrateAsync` SHALL remain in force after the type change, producing the same `ConfigurationException` messages for null/malformed identifiers. Validation MAY be invoked on the `Value` of the new value types; whether validation additionally moves into the value-type constructors is an ADR decision, but the externally observable rejection behaviour SHALL NOT regress.
- Example: provisioning with table name `"1Outbox"` still throws `ConfigurationException` whose message contains the substring `^[A-Za-z][A-Za-z0-9_]*$`.
- Example: provisioning a SQLite box with `schemaName == null` still succeeds (null schema is legitimate, not "missing").

**FR-12 — Backend implementations remain compilable.** All concrete implementers of the changed interfaces — the four relational catalogs/runners/detection helpers (MsSql, MySql, PostgreSql, Sqlite) and the free-standing Spanner provisioners/runner — SHALL continue to compile and behave identically. Internal helpers that still accept primitives (e.g. `LockResourceFor(string?, string)`, detection-helper `string tableName`/`string? schemaName` parameters, DDL string interpolation, `HashSet<string>.IsSupersetOf(LogicalColumns)`) receive values via the implicit `BoxTableName → string` / `SchemaName? → string?` operators at their call sites.
- Example: `SqlBoxMigrationRunner.LockResourceFor(schemaName, tableName)` — where `schemaName` is `SchemaName?` and `tableName` is `BoxTableName` — receives `string?`/`string` values via the implicit operators and produces the same lock-resource string as before.
- Example: `actualColumns.IsSupersetOf(migrations[i].LogicalColumns)` is unaffected because `LogicalColumns` remains `IReadOnlyCollection<string>`.

**FR-13 — Value types follow the `Id.cs` template.** Each new value type SHALL be a `record` (or equivalent value-equality type) following the structural pattern of `src/Paramore.Brighter/Id.cs`: licence header, namespace `Paramore.Brighter.BoxProvisioning`, a public constructor accepting the underlying primitive, `Value` property, implicit operators, `ToString()` override, XML doc on the type and every public member, and (for string types) the FR-3 static `IsNullOrEmpty` helper using `[NotNullWhen(false)]`. Types SHALL NOT share a common base record — each is a standalone type so that equality is type-scoped (a `BoxTableName` can never equal a `SchemaName` even if they wrap the same string).
- Example: `new BoxTableName("dbo") == new SchemaName("dbo")` does not compile (different types).

### Non-functional Requirements

- **NFR-1 — Source compatibility.** The change SHALL be source-compatible for existing call sites passing primitives. No call site SHALL require reordering or retyping `Version`/`Description`/`UpScript`/`SourceReference`/`IdempotencyCheckSql`/`tableName`/`schemaName`/`LogicalColumns` arguments.
- **NFR-2 — Multi-target framework support.** All new types SHALL compile on every target framework the `Paramore.Brighter.BoxProvisioning` project already targets, including `netstandard2.0`; no API used (e.g. `IReadOnlySet<T>`) may be unavailable on `netstandard2.0`.
- **NFR-3 — No runtime behaviour change.** Provisioning, migration path selection (fresh/bootstrap/normal), monotonicity validation, identifier validation, history-schema resolution, and emitted telemetry/log content SHALL be byte-for-byte equivalent to the pre-change behaviour for all existing inputs.
- **NFR-4 — Allocation overhead.** Wrapping primitives in records SHALL NOT introduce per-operation allocations on hot paths beyond the one-time wrapping of values already constructed once per migration/provision run; no value type may be allocated inside a per-row loop where a primitive was previously used directly.
- **NFR-5 — Documentation quality.** Every new public type and member SHALL carry XML documentation that states what the value represents and its nullability contract, matching the documentation density already present on the BoxProvisioning interfaces.
- **NFR-6 — Style consistency.** New files SHALL carry the standard Brighter MIT licence header and follow the established file-per-type and naming conventions of the `Paramore.Brighter.BoxProvisioning` assembly.

### Constraints and Assumptions

- **C-1 — Scope boundary.** New value types live ONLY in `Paramore.Brighter.BoxProvisioning`. The core `Paramore.Brighter` assembly is NOT modified to introduce new value types under this issue. In particular, `IAmARelationalDatabaseConfiguration.OutBoxTableName`, `InBoxTableName`, and `SchemaName` remain `string`/`string?` in core; the new value types must interoperate with them via implicit conversion.
- **C-2 — Implementers in scope.** Concrete backend implementers of the BoxProvisioning interfaces (the MsSql/MySql/PostgreSql/Sqlite catalog/runner/detection-helper/provisioner classes and the Spanner classes), each in their own assembly, are in scope only to the extent required to keep them compiling and behaviourally identical after the interface signatures change.
- **C-3 — Reuse existing validation.** `Identifiers.AssertSafe` already encodes the cross-backend safe-identifier rule; the solution reuses it rather than re-deriving validation, and any constructor-level validation defers to it.
- **C-4 — Spanner exemption preserved.** Spanner's free-standing implementation (no `IAmABoxMigrationCatalog`, no V_k chain per ADR 0057 §6) must remain valid; value-type adoption must not assume the catalog/version-chain shape.
- **A-1 — Assumption.** Implicit `string ↔ value-type` conversions are acceptable in Brighter's style for this subsystem, consistent with `Id`. (If the maintainers later decide implicit conversions are undesirable for SQL safety, that is an ADR-level reversal, not a requirements change.)
- **A-2 — Assumption.** The `int`-backed `MigrationVersion` keeps `int` as its `Value` type; no widening to `long` is required because existing versions are small monotonic integers.

### Out of Scope

- New value types in the core `Paramore.Brighter` assembly, or changing `IAmARelationalDatabaseConfiguration` property types.
- Wrapping `BoxType`, `BoxTableState`, `MigrationHistoryScope`, or `TimeSpan MigrationLockTimeout`, which are already non-primitive or already appropriately typed.
- Wrapping `LogicalColumns` / introducing a `LogicalColumnName` type. `LogicalColumns` is an internal version-detection mechanism consumed via `HashSet<string>.IsSupersetOf(…)` with backend-specific `StringComparer` semantics; retyping it as `IReadOnlyCollection<LogicalColumnName>` would break the superset call in every detection helper and risk silently changing case-comparison behaviour.
- Wrapping connection strings, payload column names, lock-resource strings, or discriminator strings used purely inside backend implementations and not surfaced on the BoxProvisioning public interfaces.
- Changing the parameter types of `IAmABoxMigrationDetectionHelper<,>` or `IAmAProvisioningUnitOfWork<>` methods (they may continue to accept primitives; value types convert implicitly at the call sites).
- Adding `Down`/rollback scripts, new migration metadata fields, or any behavioural change to migration selection or execution.
- Removing or weakening `Identifiers.AssertSafe` validation.
- Migrating other Brighter subsystems away from primitives.

## Acceptance Criteria

**AC-1 (FR-1, FR-2, FR-4, FR-5, FR-6, FR-7, FR-13).** Given each new value type, When a developer assigns a primitive to it and reads `.Value` / casts back / calls `.ToString()`, Then the underlying primitive round-trips unchanged and two instances wrapping equal primitives compare equal.

**AC-2 (FR-3).** Given any string-backed value type, When `IsNullOrEmpty` is called with `null`, an instance wrapping `""`, and an instance wrapping a non-empty string, Then it returns `true`, `true`, and `false` respectively.

**AC-3 (FR-2, FR-6, FR-7, FR-9).** Given the nullable usages (`SchemaName?`, `SqlScript? IdempotencyCheckSql`, `SourceReference?`), When `null` is supplied, Then it is accepted (compiles and runs) and a SQLite provisioning run with a null schema and a SQLite V2+ migration with a null idempotency-check SQL behaves exactly as before.

**AC-4 (FR-8, FR-10, NFR-1).** Given the existing `new BoxMigration(...)` call sites across the four relational catalog assemblies and the test doubles, When the solution builds, Then they compile with no change to any argument — including `LogicalColumns`, which remains `IReadOnlyCollection<string>`.

**AC-5 (FR-9, FR-10, FR-12).** Given `SqlBoxProvisioner.ProvisionAsync` and `SqlBoxMigrationRunner.MigrateAsync`, When the solution builds, Then the call `_migrationRunner.MigrateAsync(BoxTableName, _configuration.SchemaName, BoxType, tableState, cancellationToken)` and the `BoxTableName` property derivation from core `string` config properties compile via implicit conversion.

**AC-6 (FR-11, NFR-3).** Given a table name `"1Outbox"`, When provisioning runs, Then a `ConfigurationException` whose message contains the substring `^[A-Za-z][A-Za-z0-9_]*$` is thrown; and Given a null schema on SQLite, When provisioning runs, Then no "missing identifier" exception is thrown.

**AC-7 (FR-4, NFR-3).** Given migration version lists `[1,2,3]` and `[1,3]`, When `MigrateAsync` runs its monotonicity check, Then the first passes and the second throws `ConfigurationException` whose message contains the substring `V1 followed by V3 (expected V2)`, identical to current behaviour.

**AC-8 (NFR-2).** Given the `Paramore.Brighter.BoxProvisioning` project's full TFM matrix including `netstandard2.0`, When the solution is compiled for every target, Then all new value types compile with no use of APIs unavailable on `netstandard2.0`.

**AC-9 (FR-12, NFR-3).** Given the full BoxProvisioning solution (all four relational backends and Spanner) and the existing BoxProvisioning unit and integration tests, When the suite is run after the change, Then all previously-passing tests still pass with no test logic changes beyond mechanical type adjustments, and emitted telemetry tags and log messages (`BoxTable`, `HistorySchema`, span display name `box.migration {table}`) render the same string content as before.

**AC-10 (Definition of done).** The work is done when: every FR above is implemented; AC-1 through AC-9 are demonstrated by tests; the solution builds clean on all TFMs; no new public types appear outside `Paramore.Brighter.BoxProvisioning`; and an ADR records the value-type design decisions (conversion strategy, where validation lives, type-isolation rationale for excluding `LogicalColumns`).

## Additional Context

- Reference pattern: `src/Paramore.Brighter/Id.cs` (value type wrapping a string with `Value`, implicit operators, `IsNullOrEmpty`, `ToString`).
- Existing validation to reuse: `src/Paramore.Brighter.BoxProvisioning/Identifiers.cs` (`AssertSafe`, regex `^[A-Za-z][A-Za-z0-9_]*$`, splits null vs malformed diagnostics).
- Primary interfaces to retype: `IAmABoxMigration.cs`, `IAmABoxMigrationRunner.cs`, `IAmABoxProvisioner.cs`, and the `BoxMigration.cs` record.
- Call sites that must keep compiling via implicit conversion: `SqlBoxProvisioner.cs` (`ProvisionAsync`, `BoxTableName` property), `SqlBoxMigrationRunner.cs` (`MigrateAsync`, `LockResourceFor`, `ValidateMigrationsMonotonic`, activity/log tagging), all `*MigrationCatalog.cs` `new BoxMigration(...)` sites in the four relational backend assemblies, the Spanner provisioners/runner, and the `BrokenMigrationFactory` / SQLite contention test doubles under `tests/`.
- Note on the detection helper: `IAmABoxMigrationDetectionHelper<,>` deliberately orders `cancellationToken` before the optional `transaction`; its `string tableName`/`string? schemaName` parameters are intentionally left as primitives (out of scope), with value types converting implicitly at the runner/provisioner call sites.

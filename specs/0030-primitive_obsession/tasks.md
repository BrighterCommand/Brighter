# Tasks: Spec 0030 — Box Provisioning Value Types

> **Tidy First note:** This spec is a **purely structural** change (ADR 0061 "Implementation Approach"). No runtime behaviour changes. New behaviours under test belong to the new value types themselves (their `Value`/`ToString`/equality/`IsNullOrEmpty`/conversion/comparison semantics — AC-1 through AC-7). The contract retyping and the single cast fix (D4) carry zero behaviour change and must be committed separately from any behavioural work.

## Phase 0 — Baseline (risk mitigation)

- [ ] **Establish a green baseline before any change.**
  - *Compile*: build the full BoxProvisioning src/backend solution across all four TFMs (`netstandard2.0;net8.0;net9.0;net10.0`). The test project targets only `net9.0;net10.0` — do not attempt to run tests against netstandard2.0 or net8.0.
  - *Run*: run the existing `tests/Paramore.Brighter.BoxProvisioning.Tests/` suite on `net9.0` and `net10.0`; record the passing set.
  - Purpose: AC-9 / NFR-3 require previously-passing tests still pass with no logic change. This is the reference point for both the src-compile and test-run baselines.
  - References (existing): `tests/Paramore.Brighter.BoxProvisioning.Tests/Paramore.Brighter.BoxProvisioning.Tests.csproj`

---

## Phase 1 — Add value-type files (purely additive structural; nothing references them yet)

> These are pure structural additions per ADR 0061 Implementation Approach step 1 — the commit adds types only and compiles in isolation. Each value type's behaviour (round-trip, equality, conversions, `IsNullOrEmpty`, comparison) is a real new behaviour, so each is written **test-first** using the TDD template. All six files live in `src/Paramore.Brighter.BoxProvisioning/`, one file per type, modelled exactly on `Id.cs` (D1, FR-13).

- [ ] **TEST + IMPLEMENT: `BoxTableName` wraps a string and round-trips through Value, conversions, ToString, and equality.**
  - **USE COMMAND**: `/test-first BoxTableName value type round-trips a string through Value, implicit conversions both ways, ToString, and value equality`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests/`
  - Test file: `When_a_box_table_name_wraps_a_string_it_should_round_trip_the_value.cs`
  - Test should verify:
    - `BoxTableName t = "Outbox"; string s = t;` compiles, `t.Value == "Outbox"`, `s == "Outbox"`, `t.ToString() == "Outbox"` (FR-1)
    - `new BoxTableName("Outbox") == (BoxTableName)"Outbox"` is `true` (FR-1, value equality)
    - `new BoxTableName("dbo") == new SchemaName("dbo")` does **not** compile — note as an expectation in the test, not an executable assertion (FR-13 type isolation, D1)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `src/Paramore.Brighter.BoxProvisioning/BoxTableName.cs`: MIT licence header, namespace `Paramore.Brighter.BoxProvisioning`, `public record BoxTableName`, `string Value` property, public ctor `BoxTableName(string value)`, implicit `BoxTableName → string?` and `string → BoxTableName`, `ToString()` override returning `Value`, XML docs on type and every member (FR-1, FR-13, NFR-5, NFR-6)
    - **Null-safe operator** (critical): the implicit-to-string operator MUST return `string?` and use `?.Value` — `public static implicit operator string?(BoxTableName value) => value?.Value;` — matching the `Id.cs` template exactly (and all nine boy-scout-corrected types in the codebase). The operator is defined on the non-nullable type; the `?.Value` guard handles the case where the variable is typed as `BoxTableName?`. Call sites passing the result into the non-nullable `Identifiers.AssertSafe(string identifier, …)` parameter receive a `string?` — this is the established codebase pattern (e.g. `RoutingKey`, `Id`); CS8604 may surface under `<Nullable>enable</Nullable>` and should be resolved with `.Value` or `!` at the call site, not by making the operator return `string`.
    - Follow `src/Paramore.Brighter/Id.cs` structure exactly; standalone record with no shared base (FR-13)

- [ ] **TEST + IMPLEMENT: `BoxTableName.IsNullOrEmpty` reports null/empty/non-empty correctly.**
  - **USE COMMAND**: `/test-first BoxTableName.IsNullOrEmpty returns true for null and empty and false for non-empty with NotNullWhen(false)`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests/`
  - Test file: `When_box_table_name_is_null_or_empty_is_called_it_should_report_emptiness.cs`
  - Test should verify:
    - `BoxTableName.IsNullOrEmpty(null) == true` (FR-3, AC-2)
    - `BoxTableName.IsNullOrEmpty((BoxTableName)"") == true` (FR-3, AC-2)
    - `BoxTableName.IsNullOrEmpty((BoxTableName)"Outbox") == false` (FR-3, AC-2)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add static `public static bool IsNullOrEmpty([NotNullWhen(false)] BoxTableName? value)` to `BoxTableName.cs`, mirroring `Id.IsNullOrEmpty` (FR-3); `using System.Diagnostics.CodeAnalysis;`

- [ ] **TEST + IMPLEMENT: `SchemaName` round-trips a string and is usable as nullable.**
  - **USE COMMAND**: `/test-first SchemaName value type round-trips a string, supports SchemaName? null, and exposes IsNullOrEmpty`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests/`
  - Test file: `When_a_schema_name_wraps_a_string_or_is_null_it_should_round_trip_the_value.cs`
  - Test should verify:
    - `SchemaName sn = "dbo"; string s = sn;` yields `s == "dbo"` (FR-2)
    - `SchemaName? sn = null;` compiles and is legal (FR-2, AC-3 SQLite null-schema)
    - equality, `ToString()` returns `Value`
    - `SchemaName.IsNullOrEmpty(null) == true`, `IsNullOrEmpty((SchemaName)"") == true`, `IsNullOrEmpty((SchemaName)"dbo") == false` (FR-3, AC-2)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `src/Paramore.Brighter.BoxProvisioning/SchemaName.cs` following the `BoxTableName.cs`/`Id.cs` template, with `IsNullOrEmpty([NotNullWhen(false)] SchemaName?)` (FR-2, FR-3, D6 nullable-on-reference-type)
    - **Null-safe operator** (critical): the implicit-to-string operator MUST return `string?` and use `?.Value` — `public static implicit operator string?(SchemaName value) => value?.Value;` — matching the `ChannelName.cs` pattern already established in the codebase. Without this, `(string?)(SchemaName?)null` throws NRE at runtime (AC-3, FR-12 SQLite null-schema path)

- [ ] **TEST + IMPLEMENT: `MigrationDescription` round-trips a string and exposes IsNullOrEmpty.**
  - **USE COMMAND**: `/test-first MigrationDescription value type round-trips a string, ToString, equality, and IsNullOrEmpty`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests/`
  - Test file: `When_a_migration_description_wraps_a_string_it_should_round_trip_the_value.cs`
  - Test should verify:
    - `MigrationDescription d = "Add Source column"; string s = d;` yields `s == "Add Source column"` (FR-5)
    - `ToString()` returns `Value`; equality holds for equal strings
    - `IsNullOrEmpty` null/empty/non-empty cases (FR-3, AC-2)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `src/Paramore.Brighter.BoxProvisioning/MigrationDescription.cs` following the template with `IsNullOrEmpty` (FR-5, FR-3)
    - **Null-safe operator** (critical): the implicit-to-string operator MUST return `string?` and use `?.Value` — `public static implicit operator string?(MigrationDescription value) => value?.Value;` — matching the `Id.cs` template and all other string-backed types in this spec

- [ ] **TEST + IMPLEMENT: `SqlScript` round-trips script text unchanged, supports nullable, exposes IsNullOrEmpty.**
  - **USE COMMAND**: `/test-first SqlScript value type round-trips SQL text unchanged, supports SqlScript? null, and exposes IsNullOrEmpty without validating content`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests/`
  - Test file: `When_a_sql_script_wraps_script_text_it_should_round_trip_the_value.cs`
  - Test should verify:
    - `SqlScript up = "ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL"; string sql = up;` round-trips unchanged (FR-6)
    - `SqlScript? guard = null;` compiles and equals `null` (FR-6, AC-3 null idempotency check)
    - the type does not validate or reject content (no exception for arbitrary text) (FR-6)
    - `IsNullOrEmpty` null/empty/non-empty cases (FR-3, AC-2)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `src/Paramore.Brighter.BoxProvisioning/SqlScript.cs` following the template with `IsNullOrEmpty`; no content validation in the type (FR-6, FR-3)
    - **Null-safe operator** (critical): `public static implicit operator string?(SqlScript value) => value?.Value;` — needed for the `SqlScript? IdempotencyCheckSql` nullable usage (AC-3)

- [ ] **TEST + IMPLEMENT: `SourceReference` round-trips a string, supports nullable for V1, exposes IsNullOrEmpty.**
  - **USE COMMAND**: `/test-first SourceReference value type round-trips a string, supports SourceReference? null, and exposes IsNullOrEmpty`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests/`
  - Test file: `When_a_source_reference_wraps_a_string_or_is_null_it_should_round_trip_the_value.cs`
  - Test should verify:
    - `SourceReference? r = "a1b2c3d / #4039"; string? s = r;` yields `s == "a1b2c3d / #4039"` (FR-7)
    - `SourceReference? r = null;` compiles (V1 carries null) (FR-7, D6)
    - `ToString()`, equality, `IsNullOrEmpty` cases (FR-3, AC-2)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `src/Paramore.Brighter.BoxProvisioning/SourceReference.cs` following the template with `IsNullOrEmpty` (FR-7, FR-3)
    - **Null-safe operator** (critical): `public static implicit operator string?(SourceReference value) => value?.Value;` — needed for the `SourceReference?` nullable usage in V1 migrations (FR-7, AC-3)

- [ ] **TEST + IMPLEMENT: `MigrationVersion` round-trips an int and participates in int arithmetic, comparison, and ordering.**
  - **USE COMMAND**: `/test-first MigrationVersion value type round-trips an int through Value and implicit conversions, supports int arithmetic and comparison, and implements IComparable`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests/`
  - Test file: `When_a_migration_version_wraps_an_int_it_should_round_trip_and_support_arithmetic.cs`
  - Test should verify:
    - `MigrationVersion v = 3; int i = v;` yields `i == 3` (FR-4, AC-1)
    - `var prev = (MigrationVersion)1; int next = prev + 1;` yields `next == 2` (FR-4 arithmetic via implicit `→ int`)
    - comparison: `(MigrationVersion)1 < (MigrationVersion)2` and `IComparable<MigrationVersion>.CompareTo` orders correctly (FR-4 ordering)
    - `ToString()` renders the number; value equality holds (FR-4)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `src/Paramore.Brighter.BoxProvisioning/MigrationVersion.cs`: `public record MigrationVersion : IComparable<MigrationVersion>`, `int Value`, public ctor `MigrationVersion(int value)`, implicit `MigrationVersion → int` and `int → MigrationVersion`, `CompareTo`, `ToString()` override, XML docs (FR-4, FR-13, A-2 keeps `int`)
    - No `IsNullOrEmpty` (int-backed, not a string type) (FR-3 scope)

---

## Phase 2 — Retype contracts and fix the one cast (structural; D4)

> ADR 0061 Implementation Approach step 2. These edits change member/parameter **types** only; all existing call sites continue to compile via implicit conversions (NFR-1, AC-4, AC-5). The only non-mechanical-conversion edit is the single ternary cast at `SqlBoxMigrationRunner.cs:285` (D4). No behaviour changes — commit this separately from any behavioural change.

- [x] **Retype `IAmABoxMigration` and `BoxMigration` to value types; keep all primitive call sites compiling.**
  - Edit `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigration.cs`: `Version → MigrationVersion`, `Description → MigrationDescription`, `UpScript → SqlScript`, `SourceReference → SourceReference?`, `IdempotencyCheckSql → SqlScript?`. `LogicalColumns` stays `IReadOnlyCollection<string>` (FR-8, D5). Preserve existing XML docs and nullability contract notes (NFR-5).
  - Edit `src/Paramore.Brighter.BoxProvisioning/BoxMigration.cs` record parameters to the same value types, `LogicalColumns` unchanged (FR-8).
  - Verify (do not change arguments) that existing `new BoxMigration(1, "Add Source", "ALTER TABLE …", new[] { "Source" })` and `new BoxMigration(1, "V1", "CREATE TABLE …", cols, null, null)` call sites across the four relational catalog assemblies compile unchanged via implicit conversions (FR-8, NFR-1, AC-4, D2).
  - References (existing): `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigration.cs`, `src/Paramore.Brighter.BoxProvisioning/BoxMigration.cs`, `src/Paramore.Brighter/Id.cs`
  - Depends on: all Phase 1 value-type tasks.

- [x] **Retype `IAmABoxMigrationRunner.MigrateAsync` signature; fix the D4 ternary cast in `SqlBoxMigrationRunner`.**
  - Edit `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationRunner.cs`: `tableName → BoxTableName`, `schemaName → SchemaName?`; leave `boxType`, `tableState`, `cancellationToken` unchanged (FR-9). Preserve XML docs.
  - Edit `src/Paramore.Brighter.BoxProvisioning/SqlBoxMigrationRunner.cs` line ~285 ternary to `migrations.Count == 0 ? (MigrationVersion)0 : migrations[migrations.Count - 1].Version` to resolve the bidirectional-implicit ambiguity (CS0172) (D4). This is the **only** required cast.
  - Verify the `Identifiers.AssertSafe(tableName, …)` (line ~191) and `AssertSafe(schemaName, …)` (line ~194) calls still compile against the retyped parameters via implicit `→ string?`, unchanged — do NOT move validation into constructors (FR-11, D3, C-3). Note: because all string-backed operators return `string?` (matching `Id.cs`), these calls pass a `string?` into the non-nullable `AssertSafe(string identifier, …)` parameter — the established codebase pattern. CS8604 nullable warnings may surface; resolve with `.Value` or `!` at each call site, not by reverting the operator return type.
  - Verify the `SqlBoxProvisioner` call `_migrationRunner.MigrateAsync(BoxTableName, _configuration.SchemaName, …)` compiles with core `string?` `SchemaName` converting implicitly to `SchemaName?` (FR-9, AC-5, C-1, D2).
  - References (existing): `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationRunner.cs`, `src/Paramore.Brighter.BoxProvisioning/SqlBoxMigrationRunner.cs` (lines 285, 191, 194), `src/Paramore.Brighter.BoxProvisioning/SqlBoxProvisioner.cs`
  - Depends on: Phase 1 (`BoxTableName`, `SchemaName`, `MigrationVersion`).

- [x] **Retype `IAmABoxProvisioner.BoxTableName` to `BoxTableName`; keep config-derived and log call sites working.**
  - Edit `src/Paramore.Brighter.BoxProvisioning/IAmABoxProvisioner.cs`: `BoxTableName` property `string → BoxTableName` (FR-10). Preserve XML docs.
  - Verify `SqlBoxProvisioner` deriving the property from `_configuration.OutBoxTableName` / `InBoxTableName` (core `string`) compiles via implicit conversion, with no core changes (FR-10, C-1, D2).
  - Verify `BoxProvisioningHostedService` log interpolation of `BoxTableName` still renders the underlying string via `ToString()`, not the type name (FR-10, AC-9).
  - Verify `Identifiers.AssertSafe(BoxTableName, …)` at `SqlBoxProvisioner.cs:105` still compiles via implicit `→ string?` (FR-11, D3). Note: the `→ string?` operator passes a `string?` into the non-nullable `AssertSafe(string, …)` parameter — resolve any CS8604 warning with `.Value` or `!`, consistent with the runner call sites.
  - References (existing): `src/Paramore.Brighter.BoxProvisioning/IAmABoxProvisioner.cs`, `src/Paramore.Brighter.BoxProvisioning/SqlBoxProvisioner.cs` (line 105)
  - Depends on: Phase 1 (`BoxTableName`).

- [x] **Mechanically update existing test doubles' property types and method signatures to value types (no behaviour change).**
  - This is a purely mechanical type adjustment driven by the contract retyping — NOT a new behaviour test. It splits into two categories:
  - **`IAmABoxMigration` implementers** — update property declarations: `int Version → MigrationVersion`, `string Description → MigrationDescription`, `string UpScript → SqlScript`, `string? SourceReference → SourceReference?`, `string? IdempotencyCheckSql → SqlScript?`. `LogicalColumns` stays `IReadOnlyCollection<string>`. Note: the test project contains no `new BoxMigration(...)` call sites — migrations in tests are built via `StubBoxMigration` property declarations; `new BoxMigration(...)` source-compat is a production-catalog concern covered by Phase 3.
  - **`IAmABoxMigrationRunner` direct implementers** — update `MigrateAsync` signature: `string tableName → BoxTableName`, `string? schemaName → SchemaName?`. Four such stubs exist in the test project, each a private class in a provisioner test file (they implement `IAmABoxMigrationRunner` directly, not `SqlBoxMigrationRunner`).
  - The verifiable result for this task is: the test assembly compiles cleanly on `net9.0` and `net10.0` with no argument-list changes at any call site.
  - References (existing — exhaustive list of files containing types to update):
    - `IAmABoxMigration` implementers:
      - `tests/Paramore.Brighter.BoxProvisioning.Tests/When_relational_box_migration_runner_base_migrate_receives_non_monotonic_migrations_it_should_throw_before_opening_connection.cs` (`StubBoxMigration`)
      - `tests/Paramore.Brighter.BoxProvisioning.Tests/When_relational_box_migration_runner_base_migrate_receives_unsafe_identifier_it_should_throw_before_opening_connection.cs` (`StubBoxMigration`)
    - `IAmABoxMigrationRunner` direct implementers (update `MigrateAsync` signature only):
      - `tests/Paramore.Brighter.BoxProvisioning.Tests/When_sql_box_provisioner_detect_table_state_inlines_negative_version_clamp.cs` (`VersionCapturingMigrationRunner`)
      - `tests/Paramore.Brighter.BoxProvisioning.Tests/When_sql_box_provisioner_effective_schema_name_is_overridden_it_should_propagate_to_detection_and_payload_calls_only.cs` (`SchemaCapturingMigrationRunner`)
      - `tests/Paramore.Brighter.BoxProvisioning.Tests/When_sql_box_provisioner_provision_async_receives_unsafe_identifier_it_should_throw_before_opening_connection.cs` (`ThrowingMigrationRunner`)
      - `tests/Paramore.Brighter.BoxProvisioning.Tests/When_sql_box_provisioner_provision_async_runs_successfully_it_should_invoke_hooks_in_documented_order.cs` (`RecordingMigrationRunner`)
    - `IAmABoxProvisioner` implementer:
      - `tests/Paramore.Brighter.BoxProvisioning.Tests/TestDoubles/StubBoxProvisioner.cs` (`BoxTableName` property)
    - Note: `tests/Paramore.Brighter.BoxProvisioning.Tests/TestDoubles/StubBoxDetectionHelper.cs` implements `IAmAVersionDetectingMigrationHelper` (detection-helper, explicitly out of scope) — do NOT modify it.
    - Note: `SqlBoxMigrationRunner` subclasses in test files (e.g. `OrderProbeTestRunner`) inherit the retyped `MigrateAsync` from the base class and require no changes — only the four direct `IAmABoxMigrationRunner` implementers above need signature updates.
  - Depends on: the three contract-retyping tasks above.

---

## Phase 3 — Backend compilability (structural; recompile-only, no argument changes)

> C-2: backend implementers are in scope only to keep them compiling and behaviourally identical. AC-4/AC-5/AC-6/NFR-1: implicit operators absorb the type change at every call site — **no argument reordering or retyping** is expected.

- [x] **Verify the four relational backends recompile unchanged through implicit conversions.**
  - Build the MsSql, MySql, PostgreSql, and Sqlite BoxProvisioning catalog/runner/detection-helper/provisioner assemblies against the retyped interfaces.
  - Confirm internal helpers still receiving primitives — `LockResourceFor(string?, string)`, detection-helper `string tableName`/`string? schemaName`, DDL string interpolation, `HashSet<string>.IsSupersetOf(LogicalColumns)` — receive values via implicit `BoxTableName → string` / `SchemaName? → string?` at the call sites, with no signature changes to `IAmABoxMigrationDetectionHelper<,>` (FR-12, C-2, D5, Out-of-Scope detection helpers).
  - Confirm `actualColumns.IsSupersetOf(migrations[i].LogicalColumns)` is unaffected (`LogicalColumns` unchanged) (FR-12, D5).
  - **Null-path behavioural check**: compilation alone is not sufficient for the nullable conversions. Also confirm at runtime that the SQLite runner path (null `SchemaName`) and a V2+ migration with null `IdempotencyCheckSql` produce the same outcome as before — no NRE, no "missing identifier" exception. The Phase 4 identifier-validation task provides the null-schema test; ensure it is run before marking this Phase 3 task done.
  - Depends on: Phase 2.

- [x] **Verify the free-standing Spanner provisioners/runner recompile and stay catalog-free.**
  - Build the Spanner provisioner/runner against the retyped interfaces; confirm it compiles via implicit conversions without assuming the `IAmABoxMigrationCatalog` / V_k version-chain shape (FR-12, C-4 Spanner exemption).
  - Depends on: Phase 2.

---

## Phase 4 — Cross-cutting verification (risk mitigation; preserves behaviour)

- [x] **Verify all TFMs compile, including `netstandard2.0` with no unavailable APIs.**
  - Build `Paramore.Brighter.BoxProvisioning` and all backends across `netstandard2.0;net8.0;net9.0;net10.0`. Confirm new value types use no `netstandard2.0`-unavailable API (e.g. no `IReadOnlySet<T>`); `[NotNullWhen]` and the `Id`-style record pattern are already proven on this TFM matrix (NFR-2, AC-8, D7).
  - Depends on: Phases 1–3.

- [x] **Verify identifier-validation behaviour is unchanged (no regression).**
  - Confirm the existing identifier-validation tests still pass: provisioning with table name `"1Outbox"` throws `ConfigurationException` whose message contains `^[A-Za-z][A-Za-z0-9_]*$`; SQLite provisioning with `schemaName == null` succeeds with no "missing identifier" error (FR-11, NFR-3, AC-6).
  - References (existing): `tests/Paramore.Brighter.BoxProvisioning.Tests/When_sql_box_provisioner_provision_async_receives_unsafe_identifier_it_should_throw_before_opening_connection.cs`, `tests/Paramore.Brighter.BoxProvisioning.Tests/When_relational_box_migration_runner_base_migrate_receives_unsafe_identifier_it_should_throw_before_opening_connection.cs`, `tests/Paramore.Brighter.BoxProvisioning.Tests/When_assert_safe_identifier_is_called_with_known_unsafe_inputs_it_should_throw.cs`
  - Depends on: Phase 2.

- [x] **Verify monotonicity validation behaviour is unchanged.**
  - Confirm versions `[1,2,3]` pass and `[1,3]` throws `ConfigurationException` whose message contains `V1 followed by V3 (expected V2)` — identical to current, with `MigrationVersion` participating via implicit `→ int` arithmetic in the `$"...expected V{prev + 1}"` interpolation at `SqlBoxMigrationRunner.cs:479` (FR-4, NFR-3, AC-7).
  - References (existing): `tests/Paramore.Brighter.BoxProvisioning.Tests/When_relational_box_migration_runner_base_migrate_receives_non_monotonic_migrations_it_should_throw_before_opening_connection.cs`, `src/Paramore.Brighter.BoxProvisioning/SqlBoxMigrationRunner.cs` (lines 467–479)
  - Depends on: Phase 1 (`MigrationVersion`), Phase 2.

- [x] **Verify full suite parity and telemetry/log string content.**
  - Run the entire BoxProvisioning unit suite (and integration tests where runnable); confirm all previously-passing tests pass with only mechanical type adjustments, and that emitted telemetry tags / log messages render the same string content — `BoxTable` renders `Outbox`, `HistorySchema` unchanged, span display name `box.migration {table}` unchanged (NFR-3, AC-9, FR-10).
  - References (existing): `tests/Paramore.Brighter.BoxProvisioning.Tests/When_box_provisioning_hosted_service_logs_progress_it_should_include_the_box_table_name.cs`, `tests/Paramore.Brighter.BoxProvisioning.Tests/When_relational_box_migration_runner_base_migrate_runs_with_a_tracer_it_should_emit_a_migration_span.cs`
  - Depends on: Phases 1–4.

- [x] **Confirm no new public types leaked outside `Paramore.Brighter.BoxProvisioning` (scope guard).**
  - Confirm the core `Paramore.Brighter` assembly is unmodified — `IAmARelationalDatabaseConfiguration.OutBoxTableName`/`InBoxTableName`/`SchemaName` remain `string`/`string?`; all six new types live only in `src/Paramore.Brighter.BoxProvisioning/` (C-1, AC-10 definition of done).
  - References (existing): `src/Paramore.Brighter.BoxProvisioning/`
  - Depends on: Phases 1–3.

---

## Risk-mitigation summary

- **Baseline first** (Phase 0): pin the green reference set before edits, so AC-9 parity is measurable.
- **D4 ambiguity** is pre-identified and isolated to the single ternary at `SqlBoxMigrationRunner.cs:285`; the retype task calls it out explicitly so it is not discovered late.
- **`netstandard2.0` API risk** has a dedicated verification task (Phase 4 / AC-8).
- **Behaviour-parity risk** is split into three focused verification tasks (identifier validation, monotonicity, telemetry/log strings) so a regression is localized.
- **Scope-creep guard** (Phase 4 final task) asserts no core-assembly or `LogicalColumns` drift.

---

## Coverage cross-reference

### Functional Requirements → tasks
| FR | Covered by |
|---|---|
| FR-1 `BoxTableName` | Phase 1 `BoxTableName` round-trip TEST+IMPLEMENT |
| FR-2 `SchemaName` (nullable) | Phase 1 `SchemaName` TEST+IMPLEMENT |
| FR-3 `IsNullOrEmpty` (all string types) | `BoxTableName.IsNullOrEmpty` task + `IsNullOrEmpty` assertions in `SchemaName`/`MigrationDescription`/`SqlScript`/`SourceReference` tasks |
| FR-4 `MigrationVersion` (arith/compare/IComparable) | Phase 1 `MigrationVersion` task; Phase 4 monotonicity task |
| FR-5 `MigrationDescription` | Phase 1 `MigrationDescription` task |
| FR-6 `SqlScript` (nullable, no validation) | Phase 1 `SqlScript` task |
| FR-7 `SourceReference` (nullable) | Phase 1 `SourceReference` task |
| FR-8 retype `IAmABoxMigration`/`BoxMigration` | Phase 2 migration-contract retype task |
| FR-9 retype `MigrateAsync` | Phase 2 runner-contract retype + D4 cast task |
| FR-10 retype `IAmABoxProvisioner.BoxTableName` | Phase 2 provisioner-contract retype task; Phase 4 telemetry task |
| FR-11 preserve `AssertSafe` validation | Phase 2 runner + provisioner retype tasks (verify-only); Phase 4 identifier-validation task |
| FR-12 backends compilable | Phase 3 relational + Spanner verification tasks |
| FR-13 `Id.cs` template, standalone records | every Phase 1 implement step; type-isolation note in `BoxTableName` task |

### ADR decisions → tasks
| D | Covered by |
|---|---|
| D1 `record` over struct/class | every Phase 1 implement step (modelled on `Id.cs`) |
| D2 bidirectional implicit operators | Phase 1 implement steps; verified at Phase 2 retype tasks and Phase 3 backends |
| D3 `AssertSafe` stays at call sites | Phase 2 runner/provisioner retype tasks (verify-only, explicit "do NOT move into constructors") |
| D4 single ternary cast | Phase 2 runner-contract retype + D4 cast task (`SqlBoxMigrationRunner.cs:285`) |
| D5 `LogicalColumns` unchanged | Phase 2 migration retype task; Phase 3 relational backends task |
| D6 nullable on wrapping reference type | `SchemaName`, `SqlScript`, `SourceReference` Phase 1 tasks |
| D7 `netstandard2.0` compatibility | Phase 4 all-TFM compile task |

### Acceptance Criteria → tasks
| AC | Covered by |
|---|---|
| AC-1 round-trip/equality (all types) | all Phase 1 round-trip tasks |
| AC-2 `IsNullOrEmpty` true/true/false | `IsNullOrEmpty` assertions across the five string-type Phase 1 tasks |
| AC-3 nullable usages accepted | `SchemaName`, `SqlScript`, `SourceReference` Phase 1 tasks |
| AC-4 existing `BoxMigration` call sites compile unchanged | Phase 2 migration retype task; Phase 2 test-doubles task; Phase 3 relational task |
| AC-5 `MigrateAsync`/`BoxTableName` derivation compile via implicit | Phase 2 runner + provisioner retype tasks |
| AC-6 `1Outbox` throws; SQLite null schema OK | Phase 4 identifier-validation task |
| AC-7 monotonicity `[1,2,3]`/`[1,3]` | Phase 4 monotonicity task |
| AC-8 all TFMs incl. `netstandard2.0` | Phase 4 all-TFM compile task |
| AC-9 suite parity + telemetry/log strings | Phase 4 full-suite parity task; Phase 0 baseline |
| AC-10 definition of done (no leaked types, ADR recorded) | Phase 4 scope-guard task; ADR 0061 already exists (`docs/adr/0061-box-provisioning-value-types.md`) |

### Scope-creep check
No task introduces behaviour beyond the value types' own semantics. No task moves `AssertSafe` (D3), retypes `LogicalColumns` (D5), changes `IAmABoxMigrationDetectionHelper<,>` parameters (Out of Scope), or touches the core `Paramore.Brighter` assembly (C-1). AC-10's ADR requirement is already satisfied by the existing `docs/adr/0061-box-provisioning-value-types.md`, so no ADR-authoring task is included.

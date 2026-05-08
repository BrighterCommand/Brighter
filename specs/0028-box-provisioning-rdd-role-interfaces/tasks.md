# Tasks — Spec 0028 Box Provisioning RDD Role Interfaces

> **Source design**: [ADR 0058](../../docs/adr/0058-box-provisioning-rdd-role-interfaces.md) (Accepted, 2026-05-07).
>
> **Workflow reminder**: Tasks marked **TEST + IMPLEMENT** invoke `/test-first` per CLAUDE.md TDD mandate; STOP for user IDE approval after writing the test, before implementing. Tasks marked **TIDY FIRST** are structural-only refactors per Beck's "Tidy First" — committed as structural commits with the existing spec 0027 test suite passing before AND after; no `/test-first` because no new behaviour is introduced.
>
> **Validation gate** between phases: every backend's BoxProvisioning test filter must pass against live containers (or in-process for SQLite, emulator for Spanner). Pre-refactor counts (per NF2 at `edfa9fc99`): MSSQL 54/54, Postgres 46/46, MySQL 50/50 (net9.0 only), SQLite 40/40, Spanner 26/26, Core BoxProvisioning.Tests 23/23, Core BoxProvisioning 5/5. Phase 0 captures the actual baseline at the spec 0028 starting HEAD (`2f6d20bb9` or later) into a tracked `baseline.md`; if it has drifted upward by additive tests, the new floor is recorded there and the requirements NF2 enumeration is updated in the same commit.
>
> **Phase gate audit trail**: each phase ends with a "gate" task. The gate task's closing commit message MUST quote (a) the test-count delta vs `baseline.md` per backend per TFM, and (b) any verification command output relevant to the gate (e.g. "dotnet build all-TFMs clean" for Phase 1). This makes phase completion auditable from `git log` rather than relying on developer recollection.
>
> **Unreleased-branch licence (per NF1 / requirements C1)**: spec 0027's BoxProvisioning surface has not yet shipped — the entire family is on `database_migration` awaiting PR #4039 merge. Spec 0028 has explicit licence to source-break ANY spec 0027 surface introduced on this branch. Renames (`{Backend}BoxDetectionHelpers` → `{Backend}BoxDetectionHelper`), parameter additions (the new `string? schemaName` slot on SQLite/Spanner), parameter widenings (`string` → `string?` on MSSQL/PG/MySQL), ctor cascades on the ten provisioners — none of these are "behavioural changes" in the released-API sense. They are licensed by NF1 and enumerated in `release_notes.md`. The TIDY-FIRST/TEST+IMPLEMENT distinction in this file applies to *behaviour observable to a user of the role interfaces or the runner base after spec 0028 ships* (e.g. UoW lifecycle, schemaName null-substitution, harmonised rollback contract) — NOT to the in-flight spec 0027 surface as it transitions through this branch.
>
> **Naming conventions used in this file**:
> - "the relational four" = MSSQL, Postgres, MySQL, SQLite (Spanner exempt per ADR 0057 §6 unless explicitly listed).
> - `Add{Backend}{Box}` = the four DI extension methods `AddMsSql{Outbox,Inbox}` / `AddPostgreSql{Outbox,Inbox}` / `AddMySql{Outbox,Inbox}` / `AddSqlite{Outbox,Inbox}`.
> - "the Spanner pair" = `SpannerOutboxProvisioner` + `SpannerInboxProvisioner`.

## Dependency overview

```
Phase 1 (interfaces) ──┬─→ Phase 2 (detection helpers)   ─┐
                       ├─→ Phase 3 (catalogues)           ├─→ Phase 8 (provisioner ctor cascade) ─→ Phase 9 (DI) ─→ Phase 12 (verify)
                       ├─→ Phase 4 (payload validators)   ┘
                       └─→ Phase 5 (UoWs) ─→ Phase 6 (runner base) ─→ Phase 7 (refactor each runner) ─→ Phase 10 (lifecycle tests)
                                                                                                       │
                                                                                                       └─→ Phase 11 (release notes / PR)
```

Phases 2/3/4/5 can proceed in parallel after Phase 1. Phase 6 requires Phase 5 (UoW interface). Phase 7.1–7.4 (relational runners) require Phase 6 AND Phases 2 (detection helpers — `DetectionHelper` is injected into the base) and 5 (UoW — runner constructs UoWs). Phase 7.5 (Spanner runner rewire — does NOT derive from the base per ADR 0057 §6) requires only Phase 2.5 (Spanner detection helper conversion). Phase 8 requires Phases 2/3/4 (provisioners depend on the new role-typed parameters). Phase 9 requires Phases 2/3/4/8 (DI registers the role-impls and supplies them to the provisioner; Phase 9.5 also wires the Spanner detection helper into the runner from Phase 7.5). Phase 10 requires Phase 7 (UoW lifecycle observed via the runner). Phase 11 can be authored incrementally; finalise at end. Phase 12 is the global green run.

## Risk mitigations baked in

- **PR diff explosion** (per ADR Risks): each phase commits separately; each phase keeps existing tests passing. No "big bang" refactor commit.
- **Silent behavioural regression**: every new behaviour (UoW lifecycle, schemaName null-substitution, harmonised rollback contract) gets explicit `/test-first` coverage; pure refactors validated by the existing 198+ spec 0027 tests.
- **TFM compatibility break**: Phase 1 builds with `dotnet build` against the full TFM matrix BEFORE any backend conversion begins. Any static-virtual / `IReadOnlySet<T>` slip is caught early.
- **MSSQL `RELEASE_LOCK`-style diagnostic loss**: Phase 5 includes a per-backend test that exercises the diagnostic logging path (e.g. MySQL `1`/`0`/`NULL` tri-state) inside the new UoW class.

---

## Phase 0 — Preliminaries

- [x] **Capture spec 0027 baseline as a tracked file**
  - Run `dotnet test --filter FullyQualifiedName~BoxProvisioning` against each of the six BoxProvisioning test directories.
  - Write the captured counts (per backend, per TFM) to `specs/0028-box-provisioning-rdd-role-interfaces/baseline.md`. This file is the spec 0028 NF2 floor — every subsequent phase gate compares against it.
  - Compare the captured counts against `requirements.md` NF2 (currently anchored at `edfa9fc99`). If counts have drifted upward (additive tests landed on `database_migration` since that commit), update the requirements NF2 enumeration in the same commit so the recorded floor matches the spec 0028 starting HEAD.
  - Commit: `docs: spec 0028 Phase 0 — capture baseline test counts`.
  - Exit early if any baseline test is red — investigate before continuing.

- [x] **Verify TFM matrix builds cleanly and record the result**
  - `dotnet build src/Paramore.Brighter.BoxProvisioning/Paramore.Brighter.BoxProvisioning.csproj` succeeds on `netstandard2.0;net8.0;net9.0;net10.0`.
  - `dotnet build src/Paramore.Brighter.BoxProvisioning.MsSql/Paramore.Brighter.BoxProvisioning.MsSql.csproj` succeeds on `net462;net8.0;net9.0;net10.0`.
  - Record the TFM-matrix outcome inside the Phase 0 baseline-capture commit message (or in `baseline.md` itself). Confirms the matrix that Phase 1's role interfaces must respect (per C7).

---

## Phase 1 — New role interfaces in the shared assembly (Tidy First scaffolding)

These tasks add the role interface declarations to `src/Paramore.Brighter.BoxProvisioning/`. No backend implementations yet; the shared assembly compiles standalone.

- [x] **TIDY FIRST: Add `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` interface declaration**
  - File: `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationDetectionHelper.cs`
  - Five methods per ADR 0058 §A.1 (`DoesTableExistAsync`, `DoesHistoryExistAsync`, `GetMaxVersionAsync`, `GetTableColumnsAsync`, `DiscriminatorFor`).
  - Generic constraints `where TConnection : DbConnection where TTransaction : DbTransaction`.
  - XML-doc per ADR §A.1 — including the "schemaName null is substituted with backend default by each implementation" note on every method that takes `schemaName`.
  - Validation: `dotnet build` succeeds on the full shared-assembly TFM matrix.

- [x] **TIDY FIRST: Add `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` interface declaration**
  - File: `src/Paramore.Brighter.BoxProvisioning/IAmAVersionDetectingMigrationHelper.cs`
  - Inherits `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>`.
  - Adds `DetectCurrentVersionAsync` per ADR §A.1.
  - XML-doc explaining the relational-four exemption shape (Spanner does NOT implement this interface — see ADR 0057 §6).

- [x] **TIDY FIRST: Add `IAmABoxMigrationCatalog` interface declaration**
  - File: `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationCatalog.cs`
  - Single method `IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)` per ADR §A.2.
  - XML-doc explaining the "catalogue per (backend, box-type)" shape and the Spanner exemption (no catalogue per ADR 0057 §6).

- [x] **TIDY FIRST: Add `IAmABoxPayloadModeValidator<TConnection>` interface declaration**
  - File: `src/Paramore.Brighter.BoxProvisioning/IAmABoxPayloadModeValidator.cs`
  - Single method `ValidateAsync` with `(TConnection, string tableName, string? schemaName, string columnName, bool binaryMessagePayload, CancellationToken)` per ADR §A.3.
  - Generic constraint `where TConnection : DbConnection`.
  - XML-doc — including the schemaName null-substitution note matching §A.1.

- [x] **TIDY FIRST: Add `IAmAProvisioningUnitOfWork<TTransaction>` interface declaration**
  - File: `src/Paramore.Brighter.BoxProvisioning/IAmAProvisioningUnitOfWork.cs`
  - Inherits `IAsyncDisposable`.
  - Property `TTransaction? Transaction { get; }`.
  - Methods `BeginAsync`, `CommitAsync`, `RollbackAsync` per ADR §B.1.
  - Generic constraint `where TTransaction : DbTransaction`.
  - XML-doc per ADR §B.1 — including the "BeginAsync may throw → DisposeAsync still called → MUST tolerate dispose-after-failed-begin" note, and the "RollbackAsync MUST NOT throw" / "CommitAsync throws → RollbackAsync runs best-effort" notes from §B.3.

- [x] **Phase 1 gate: Full TFM-matrix build with the five new interface files**
  - `dotnet build` for `Paramore.Brighter.BoxProvisioning.csproj` clean on `netstandard2.0;net8.0;net9.0;net10.0`.
  - No `static virtual` / `IReadOnlySet<T>` slipped in (per C7).
  - **Commit** as a single Tidy First structural commit: `refactor: spec 0028 Phase 1 — add role interfaces (no implementations yet)`.

---

## Phase 2 — Detection helper conversions (per backend)

Each detection helper static class becomes a public instance class implementing the appropriate role interface. The conversion is mostly Tidy First — same SQL, same return values when `schemaName` is non-null. The new behaviour is the schemaName **null substitution** (each impl substitutes its backend default), which gets `/test-first` coverage.

### 2.1 MSSQL

- [x] **TIDY FIRST: Convert `MsSqlBoxDetectionHelpers` static class to `MsSqlBoxDetectionHelper` instance class implementing `IAmAVersionDetectingMigrationHelper<SqlConnection, SqlTransaction>`**
  - File: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxDetectionHelpers.cs` → renamed `MsSqlBoxDetectionHelper.cs` (singular).
  - Class `static class MsSqlBoxDetectionHelpers` → `public class MsSqlBoxDetectionHelper`.
  - All static methods become instance methods with identical bodies; `string schemaName` widens to `string? schemaName`.
  - Existing positional layout `(connection, tableName, schemaName, cancellationToken, transaction)` preserved (per ADR §A.1 source-break note: MSSQL call-sites do NOT need re-ordering).
  - Existing call-sites (`MsSqlOutboxProvisioner`, `MsSqlInboxProvisioner`, `MsSqlBoxMigrationRunner`) — leave unchanged for now; Phase 8 rewires them.
  - Validation: existing MSSQL BoxProvisioning tests stay green (the static call-sites still compile because the rename happens in this commit but Phase 8 removes them; for now keep BOTH the static facade and the new instance class so the tests pass between phases — see "Bridging shim" note below).
  - **Bridging shim**: keep a `static class MsSqlBoxDetectionHelpers` thin facade that delegates to a private singleton `MsSqlBoxDetectionHelper` instance during Phases 2–7. Removed in Phase 8 when call-sites rewire to instance dispatch. The shim has no logic of its own — pure delegation.
  - Commit message: `refactor: spec 0028 Phase 2.1 — MSSQL detection helper as instance class implementing role interface`.

- [x] **TEST + IMPLEMENT: MSSQL detection helper substitutes "dbo" when schemaName is null**
  - **USE COMMAND**: `/test-first when MsSqlBoxDetectionHelper receives null schemaName it should substitute "dbo" as the default schema for SQL parameter binding`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_detection_helper_receives_null_schema_name_it_should_substitute_dbo.cs`
  - Test should verify:
    - Calling `DoesTableExistAsync(connection, "outbox", schemaName: null, ...)` looks up `dbo.outbox` (i.e. behaviourally equivalent to passing `"dbo"`).
    - Same null-substitution applied to `DoesHistoryExistAsync`, `GetMaxVersionAsync`, `GetTableColumnsAsync`.
    - Pre-existing call with explicit `"dbo"` still works (no regression).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In each method on `MsSqlBoxDetectionHelper`, replace any direct `@SchemaName` binding of the parameter with `(schemaName ?? "dbo")`.
    - Reference per ADR §A.1 "Null-handling for schemaName".
    - Document the substitution rule in XML-doc on each public method.

### 2.2 Postgres

- [x] **TIDY FIRST: Convert `PostgreSqlBoxDetectionHelpers` static class to `PostgreSqlBoxDetectionHelper` instance class implementing `IAmAVersionDetectingMigrationHelper<NpgsqlConnection, NpgsqlTransaction>`**
  - File rename + static → instance per the MSSQL recipe.
  - Bridging shim retained until Phase 8.
  - Validation: existing Postgres BoxProvisioning tests stay green.

- [x] **TEST + IMPLEMENT: Postgres detection helper substitutes "public" when schemaName is null**
  - **USE COMMAND**: `/test-first when PostgreSqlBoxDetectionHelper receives null schemaName it should substitute "public" as the default schema for SQL parameter binding`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_detection_helper_receives_null_schema_name_it_should_substitute_public.cs`
  - Test should verify:
    - Calling `DoesTableExistAsync(connection, "outbox", schemaName: null, ...)` looks up `public.outbox`.
    - Same null-substitution applied to all four schema-bearing methods.
    - Pre-existing call with explicit `"public"` still works.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Replace direct `@SchemaName` binding with `(schemaName ?? "public")`.
    - Document the substitution rule in XML-doc.

### 2.3 MySQL

- [x] **TIDY FIRST: Convert `MySqlBoxDetectionHelpers` static class to `MySqlBoxDetectionHelper` instance class implementing `IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction>`**
  - File rename + static → instance per the recipe.
  - The transaction parameter is accepted but ignored (XML-doc states this); MySQL DDL auto-commits per ADR 0057 §5a.
  - Bridging shim retained until Phase 8.
  - Validation: existing MySQL BoxProvisioning tests stay green (net9.0 only).

- [x] **TEST + IMPLEMENT: MySQL detection helper substitutes connection.Database when schemaName is null**
  - **USE COMMAND**: `/test-first when MySqlBoxDetectionHelper receives null schemaName it should substitute connection.Database as the default schema for SQL parameter binding`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning`
  - Test file: `When_mysql_detection_helper_receives_null_schema_name_it_should_substitute_connection_database.cs`
  - Test should verify:
    - Calling `DoesTableExistAsync(connection, "outbox", schemaName: null, ...)` looks up `{connection.Database}.outbox`.
    - Same null-substitution applied to all four schema-bearing methods.
    - Pre-existing call with explicit schema still works.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Replace direct schema-binding with `(schemaName ?? connection.Database)`.
    - Document in XML-doc.

### 2.4 SQLite

- [x] **TIDY FIRST: Convert `SqliteBoxDetectionHelpers` static class to `SqliteBoxDetectionHelper` instance class implementing `IAmAVersionDetectingMigrationHelper<SqliteConnection, SqliteTransaction>`**
  - File rename + static → instance.
  - **New parameter slot**: methods previously `(connection, tableName, cancellationToken, transaction)` become `(connection, tableName, string? schemaName, cancellationToken, transaction)`. The `schemaName` parameter is accepted and ignored (SQLite has no schema concept).
  - XML-doc explicitly states "the schemaName parameter is accepted and ignored — SQLite has no schema concept".
  - **Bridging shim**: the static facade `SqliteBoxDetectionHelpers` keeps its existing four-arg signature (without `schemaName`) and delegates to the instance method passing `null`. This isolates Phase 2.4 from existing call-sites until Phase 8.
  - Validation: existing SQLite BoxProvisioning tests stay green.

### 2.5 Spanner

- [x] **TIDY FIRST: Convert `SpannerBoxDetectionHelpers` static class to `SpannerBoxDetectionHelper` (PUBLIC) instance class implementing `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` (BASE INTERFACE ONLY, NO `DetectCurrentVersionAsync`)**
  - File rename + static → instance + visibility widened from `internal` to `public` (per ADR §A.1 source-break: Spanner becomes public).
  - **New parameter slot**: methods previously without `schemaName` gain `string? schemaName` accepted-and-ignored.
  - The new class implements ONLY the base interface `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` — NOT `IAmAVersionDetectingMigrationHelper` (Spanner is degenerate per ADR 0057 §6).
  - Bridging shim retained until Phase 8.
  - Validation: existing Spanner BoxProvisioning tests stay green.

### 2.6 Phase 2 gate

- [x] **Phase 2 gate: All five detection helpers converted; all existing tests green; no regressions per NF2**
  - Run all six BoxProvisioning test filters; confirm pre-Phase-2 counts hold per backend per TFM.
  - Confirm bridging shims (per backend) compile and pass tests.
  - **No commit** — already committed per sub-task.

---

## Phase 3 — Migration catalogue conversions (Tidy First; per backend)

Each `{Backend}{Box}Migrations` static class becomes a public instance class `{Backend}{Box}MigrationCatalog` implementing `IAmABoxMigrationCatalog`. Pure structural — `All()` body unchanged.

- [x] **TIDY FIRST: Convert `MsSqlOutboxMigrations` to `MsSqlOutboxMigrationCatalog` implementing `IAmABoxMigrationCatalog`**
  - File: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrations.cs` → renamed `MsSqlOutboxMigrationCatalog.cs`.
  - `static class` → `public class`; `static IReadOnlyList<IAmABoxMigration> All(config)` → instance method.
  - Bridging shim: keep `static class MsSqlOutboxMigrations` thin facade delegating to a private singleton instance — same pattern as Phase 2 shims; removed in Phase 8.
  - Validation: existing MSSQL outbox catalogue tests (`When_mssql_outbox_migrations_are_listed_it_should_return_v1_through_v7_*`) stay green.

- [x] **TIDY FIRST: Convert `MsSqlInboxMigrations` to `MsSqlInboxMigrationCatalog`**

- [x] **TIDY FIRST: Convert `PostgreSqlOutboxMigrations` to `PostgreSqlOutboxMigrationCatalog`**

- [x] **TIDY FIRST: Convert `PostgreSqlInboxMigrations` to `PostgreSqlInboxMigrationCatalog`**

- [x] **TIDY FIRST: Convert `MySqlOutboxMigrations` to `MySqlOutboxMigrationCatalog`**

- [ ] **TIDY FIRST: Convert `MySqlInboxMigrations` to `MySqlInboxMigrationCatalog`**

- [ ] **TIDY FIRST: Convert `SqliteOutboxMigrations` to `SqliteOutboxMigrationCatalog`**

- [ ] **TIDY FIRST: Convert `SqliteInboxMigrations` to `SqliteInboxMigrationCatalog`**

- [ ] **Phase 3 gate: All eight catalogues converted; existing tests green; no Spanner change (Spanner exempt per ADR §A.2)**

---

## Phase 4 — Payload-mode validator conversions

Per ADR §A.3. Relational three (MSSQL, Postgres, MySQL) — Tidy First (signature shape preserved aside from `string` → `string?`). SQLite + Spanner — schema parameter is NEW; bridging shim with old signature delegates to new instance method passing null.

### 4.1 MSSQL

- [ ] **TIDY FIRST: Convert `MsSqlPayloadModeValidator` static class to instance class implementing `IAmABoxPayloadModeValidator<SqlConnection>`**
  - File: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlPayloadModeValidator.cs`.
  - `static class` → `public class`; `string schemaName` widens to `string? schemaName`.
  - Bridging shim retained until Phase 8.

- [ ] **TEST + IMPLEMENT: MSSQL payload validator substitutes "dbo" when schemaName is null**
  - **USE COMMAND**: `/test-first when MsSqlPayloadModeValidator receives null schemaName it should substitute "dbo" before querying INFORMATION_SCHEMA`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_payload_validator_receives_null_schema_name_it_should_substitute_dbo.cs`
  - Test should verify:
    - Validating column type for a table in `dbo` with `schemaName: null` succeeds (no spurious error).
    - Validating against an explicit `"dbo"` produces identical result.
    - Mismatch detection still throws `InvalidOperationException` per spec 0027 behaviour.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Substitute `(schemaName ?? "dbo")` before binding.
    - Match the §A.1 contract.

### 4.2 Postgres

- [ ] **TIDY FIRST: Convert `PostgreSqlPayloadModeValidator` static → instance implementing `IAmABoxPayloadModeValidator<NpgsqlConnection>`**

- [ ] **TEST + IMPLEMENT: Postgres payload validator substitutes "public" when schemaName is null**
  - **USE COMMAND**: `/test-first when PostgreSqlPayloadModeValidator receives null schemaName it should substitute "public" before querying information_schema.columns`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_payload_validator_receives_null_schema_name_it_should_substitute_public.cs`
  - Test should verify same shape as MSSQL — null path equivalent to explicit `"public"`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 4.3 MySQL

- [ ] **TIDY FIRST: Convert `MySqlPayloadModeValidator` static → instance implementing `IAmABoxPayloadModeValidator<MySqlConnection>`**

- [ ] **TEST + IMPLEMENT: MySQL payload validator substitutes connection.Database when schemaName is null**
  - **USE COMMAND**: `/test-first when MySqlPayloadModeValidator receives null schemaName it should substitute connection.Database before querying information_schema.columns`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning`
  - Test file: `When_mysql_payload_validator_receives_null_schema_name_it_should_substitute_connection_database.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 4.4 SQLite

- [ ] **TIDY FIRST: Convert `SqlitePayloadModeValidator` static → instance implementing `IAmABoxPayloadModeValidator<SqliteConnection>` with NEW `string? schemaName` parameter slot (accepted-and-ignored)**
  - Bridging shim: existing static signature delegates to new instance method passing `null`.
  - Validation: existing SQLite payload tests stay green via the shim.

### 4.5 Spanner

- [ ] **TIDY FIRST: Convert `SpannerPayloadModeValidator` static → instance implementing `IAmABoxPayloadModeValidator<SpannerConnection>` with NEW `string? schemaName` parameter slot (accepted-and-ignored)**
  - The existing `STARTS_WITH("BYTES" / "STRING")` validation logic is preserved per ADR §A.3.

- [ ] **Phase 4 gate: All five payload validators converted; existing tests green per backend per TFM**

---

## Phase 5 — Provisioning UoW classes (per relational backend)

Per ADR §B.1. Each backend ships one `{Backend}ProvisioningUnitOfWork` class implementing `IAmAProvisioningUnitOfWork<{Backend}Transaction>`. These are NEW types — `/test-first` for each lifecycle behaviour.

### 5.1 MSSQL UoW

- [ ] **TEST + IMPLEMENT: MsSqlProvisioningUnitOfWork acquires lock AFTER BeginTransaction during BeginAsync**
  - **USE COMMAND**: `/test-first when MsSqlProvisioningUnitOfWork BeginAsync is called it should call BeginTransaction before acquiring the @LockOwner='Transaction' advisory lock`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_provisioning_uow_begin_async_is_called_it_should_acquire_lock_after_begin_transaction.cs`
  - Test should verify:
    - Order of operations on a fake/spy: `connection.BeginTransaction()` → `advisoryLock.Acquire(...)`.
    - `Transaction` property is non-null after `BeginAsync` returns.
    - `Transaction` is the same object passed to `Acquire` (transaction-scoped lock per ADR §B.1).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - File: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlProvisioningUnitOfWork.cs`.
    - Implements `IAmAProvisioningUnitOfWork<SqlTransaction>`.
    - Ctor: `(SqlConnection connection, IMsSqlAdvisoryLock advisoryLock, ILogger logger)`.
    - `BeginAsync` calls `connection.BeginTransaction()` then `advisoryLock.AcquireAsync(...)` passing the transaction.

- [ ] **TEST + IMPLEMENT: MsSqlProvisioningUnitOfWork CommitAsync commits transaction (lock release implicit)**
  - **USE COMMAND**: `/test-first when MsSqlProvisioningUnitOfWork CommitAsync is called it should commit the transaction without explicit lock release because lock is transaction-scoped`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_provisioning_uow_commit_async_is_called_it_should_commit_transaction_without_explicit_lock_release.cs`
  - Test should verify:
    - `tx.CommitAsync()` invoked.
    - No call to `advisoryLock.ReleaseAsync()` (lock released implicitly when transaction commits per ADR §B.1).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: MsSqlProvisioningUnitOfWork RollbackAsync does not throw when transaction is already finalised**
  - **USE COMMAND**: `/test-first when MsSqlProvisioningUnitOfWork RollbackAsync is called after CommitAsync threw it should be best-effort and not throw`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_provisioning_uow_rollback_async_is_called_after_commit_threw_it_should_not_throw.cs`
  - Test should verify:
    - With a spy transaction whose state is `Zombied` (post-failed-commit), `RollbackAsync` returns without throwing.
    - A warning is logged via the injected `ILogger` (capturing logger pattern per ADR §B.3 logger plumbing).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Inspect transaction state before attempting rollback (per §B.3 "best-effort after thrown CommitAsync").
    - Log Warning, never throw.

- [ ] **TEST + IMPLEMENT: MsSqlProvisioningUnitOfWork DisposeAsync tolerates dispose-after-failed-BeginAsync**
  - **USE COMMAND**: `/test-first when MsSqlProvisioningUnitOfWork BeginAsync threw it should be safe to invoke DisposeAsync without throwing`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_provisioning_uow_begin_throws_it_should_dispose_without_throwing.cs`
  - Test should verify:
    - With a fake advisory lock that throws on `AcquireAsync`, `await using var uow = ...; await uow.BeginAsync(...);` propagates the throw.
    - Implicit `DisposeAsync` from the `await using` does NOT throw.
    - No call to `tx.RollbackAsync()` or `tx.CommitAsync()` before disposal (per ADR §B.3 BeginAsync-throws contract).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 5.2 Postgres UoW

- [ ] **TEST + IMPLEMENT: PostgreSqlProvisioningUnitOfWork acquires session-scoped lock BEFORE BeginTransaction**
  - **USE COMMAND**: `/test-first when PostgreSqlProvisioningUnitOfWork BeginAsync is called it should acquire pg_advisory_lock before BeginTransaction because the lock is session-scoped`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_provisioning_uow_begin_async_is_called_it_should_acquire_lock_before_begin_transaction.cs`
  - Test should verify order: `advisoryLock.Acquire()` → `connection.BeginTransaction()`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: PostgreSqlProvisioningUnitOfWork CommitAsync releases lock explicitly via pg_advisory_unlock**
  - **USE COMMAND**: `/test-first when PostgreSqlProvisioningUnitOfWork CommitAsync is called it should commit the transaction and explicitly release the pg_advisory_lock`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_provisioning_uow_commit_async_is_called_it_should_commit_then_release_lock.cs`
  - Test should verify: `tx.CommitAsync()` followed by `advisoryLock.ReleaseAsync()`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: PostgreSqlProvisioningUnitOfWork RollbackAsync rolls back transaction and releases lock without throwing**
  - **USE COMMAND**: `/test-first when PostgreSqlProvisioningUnitOfWork RollbackAsync is called it should roll back transaction and release lock and never throw`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_provisioning_uow_rollback_async_is_called_it_should_release_lock_without_throwing.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: PostgreSqlProvisioningUnitOfWork DisposeAsync tolerates dispose-after-failed-BeginAsync**
  - **USE COMMAND**: `/test-first when PostgreSqlProvisioningUnitOfWork BeginAsync threw before lock was acquired it should dispose without releasing lock and without throwing`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_provisioning_uow_begin_throws_it_should_dispose_without_throwing.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 5.3 MySQL UoW

- [ ] **TEST + IMPLEMENT: MySqlProvisioningUnitOfWork acquires GET_LOCK and never opens a transaction**
  - **USE COMMAND**: `/test-first when MySqlProvisioningUnitOfWork BeginAsync is called it should acquire GET_LOCK and leave Transaction null because MySQL DDL auto-commits`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning`
  - Test file: `When_mysql_provisioning_uow_begin_async_is_called_it_should_acquire_get_lock_with_null_transaction.cs`
  - Test should verify:
    - `advisoryLock.AcquireAsync()` invoked.
    - `connection.BeginTransaction()` NOT invoked.
    - `Transaction` property returns `null` after `BeginAsync`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: MySqlProvisioningUnitOfWork CommitAsync is no-op for transaction and releases GET_LOCK**
  - **USE COMMAND**: `/test-first when MySqlProvisioningUnitOfWork CommitAsync is called it should release GET_LOCK without committing any transaction`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning`
  - Test file: `When_mysql_provisioning_uow_commit_async_is_called_it_should_release_get_lock_without_committing_transaction.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: MySqlProvisioningUnitOfWork RollbackAsync logs RELEASE_LOCK tri-state diagnostic without throwing**
  - **USE COMMAND**: `/test-first when MySqlProvisioningUnitOfWork RollbackAsync sees RELEASE_LOCK return NULL or zero it should log a tri-state diagnostic and not throw`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning`
  - Test file: `When_mysql_provisioning_uow_rollback_observes_release_lock_tri_state_it_should_log_without_throwing.cs`
  - Test should verify (per spec 0027 Item M / ADR 0057 §5b):
    - `RELEASE_LOCK = 1` → no warning, lock cleanly released.
    - `RELEASE_LOCK = 0` → Warning logged ("lock not held by this session").
    - `RELEASE_LOCK = NULL` → Warning logged ("lock did not exist").
    - In all three cases, `RollbackAsync` returns without throwing.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Preserve the spec 0027 Item M tri-state distinction inside `MySqlProvisioningUnitOfWork.RollbackAsync` (and `CommitAsync` — same diagnostic).
    - Reference ADR §B.3 "Diagnostic information preserved" — no information loss.

- [ ] **TEST + IMPLEMENT: MySqlProvisioningUnitOfWork DisposeAsync tolerates dispose-after-failed-BeginAsync**
  - **USE COMMAND**: `/test-first when MySqlProvisioningUnitOfWork BeginAsync threw before lock was acquired it should dispose without throwing`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning`
  - Test file: `When_mysql_provisioning_uow_begin_throws_it_should_dispose_without_throwing.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 5.4 SQLite UoW

- [ ] **TEST + IMPLEMENT: SqliteProvisioningUnitOfWork BeginAsync issues BEGIN IMMEDIATE as the writer-slot lock**
  - **USE COMMAND**: `/test-first when SqliteProvisioningUnitOfWork BeginAsync is called it should issue BEGIN IMMEDIATE which reserves the writer slot as a combined lock-and-transaction`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning`
  - Test file: `When_sqlite_provisioning_uow_begin_async_is_called_it_should_begin_immediate_as_writer_slot_lock.cs`
  - Test should verify:
    - `BEGIN IMMEDIATE` SQL issued (verifiable via SQLite event hook OR via inspecting that a second concurrent `BEGIN IMMEDIATE` fails fast with SQLITE_BUSY).
    - `Transaction` property non-null after BeginAsync.
    - No separate lock primitive is acquired (no `IXxxAdvisoryLock` field; SQLite's UoW ctor takes only `SqliteConnection` + `ILogger`).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: SqliteProvisioningUnitOfWork CommitAsync commits transaction releasing writer slot**
  - **USE COMMAND**: `/test-first when SqliteProvisioningUnitOfWork CommitAsync is called it should commit the BEGIN IMMEDIATE transaction releasing the writer slot`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning`
  - Test file: `When_sqlite_provisioning_uow_commit_async_is_called_it_should_commit_releasing_writer_slot.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: SqliteProvisioningUnitOfWork RollbackAsync rolls back without throwing**
  - **USE COMMAND**: `/test-first when SqliteProvisioningUnitOfWork RollbackAsync is called it should roll back the transaction and never throw`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning`
  - Test file: `When_sqlite_provisioning_uow_rollback_async_is_called_it_should_not_throw.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: SqliteProvisioningUnitOfWork DisposeAsync tolerates dispose-after-failed-BeginAsync**
  - **USE COMMAND**: `/test-first when SqliteProvisioningUnitOfWork BeginAsync threw it should dispose without throwing`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning`
  - Test file: `When_sqlite_provisioning_uow_begin_throws_it_should_dispose_without_throwing.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 5.5 Phase 5 gate

- [ ] **Phase 5 gate: Four UoW classes exist with full lifecycle test coverage; no runner refactor yet**
  - Run all relational BoxProvisioning test filters; confirm pre-Phase-5 counts hold (UoW tests are additive — counts INCREASE from this phase).
  - Confirm each UoW's `MUST NOT throw on RollbackAsync/DisposeAsync` contract is exercised.

---

## Phase 6 — RelationalBoxMigrationRunnerBase abstract class

Per ADR §B.2. Introduce the abstract base class in the shared assembly. NO derived runners yet.

- [ ] **TEST + IMPLEMENT: RelationalBoxMigrationRunnerBase orchestrates the template algorithm in the documented order on the success path**
  - **USE COMMAND**: `/test-first when RelationalBoxMigrationRunnerBase MigrateAsync runs successfully against a fake backend it should invoke the hooks in the order OpenConnection → CreateUnitOfWork → BeginAsync → EnsureHistoryTable → RedetectStateAsync → Run{Fresh,Bootstrap,Normal}PathAsync → CommitAsync`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests`
  - Test file: `When_relational_box_migration_runner_base_migrate_runs_successfully_it_should_invoke_hooks_in_documented_order.cs`
  - Test should verify (against a fake `TestRunner : RelationalBoxMigrationRunnerBase<DbConnection, DbTransaction>` with hook spies):
    - Order on fresh-table path: OpenConnection → CreateUnitOfWork → BeginAsync → EnsureHistoryTable → RedetectStateAsync → RunFreshPathAsync → CommitAsync → DisposeAsync.
    - Order on bootstrap path: same up to RedetectStateAsync, then RunBootstrapPathAsync (not RunFreshPath/RunNormalPath).
    - Order on normal path: same up to RedetectStateAsync, then RunNormalPathAsync.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - File: `src/Paramore.Brighter.BoxProvisioning/RelationalBoxMigrationRunnerBase.cs`.
    - Class: `public abstract class RelationalBoxMigrationRunnerBase<TConnection, TTransaction> : IAmABoxMigrationRunner where TConnection : DbConnection where TTransaction : DbTransaction`.
    - Ctor: `(IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>, IAmARelationalDatabaseConfiguration, TimeSpan lockTimeout, ILogger? logger = null)`.
    - Protected props: `Logger`, `DetectionHelper`, `Configuration` per ADR §B.2.
    - Public sealed `MigrateAsync` body per ADR §B.2 listing.
    - Abstract hooks: `OpenConnectionAsync`, `CreateUnitOfWorkAsync`, `LockResourceFor`, `EnsureHistoryTableAsync`, `RunFreshPathAsync`, `RunBootstrapPathAsync`, `RunNormalPathAsync`.
    - Virtual hook: `RedetectStateAsync` (default calls `_detectionHelper.DoesTableExistAsync` + `DoesHistoryExistAsync`).
    - Protected `ValidateMigrationsMonotonic` helper lifted from spec 0027 Items H/I/Q (move from existing per-backend runners or keep duplicated until Phase 7).

- [ ] **TEST + IMPLEMENT: RelationalBoxMigrationRunnerBase calls RollbackAsync(CancellationToken.None) on exception from any hook between BeginAsync and CommitAsync**
  - **USE COMMAND**: `/test-first when RelationalBoxMigrationRunnerBase a hook between BeginAsync and CommitAsync throws it should call uow.RollbackAsync with CancellationToken.None and rethrow`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests`
  - Test file: `When_relational_box_migration_runner_base_hook_throws_it_should_rollback_with_cancellation_token_none_and_rethrow.cs`
  - Test should verify:
    - When `EnsureHistoryTableAsync` throws → `RollbackAsync` invoked with `CancellationToken.None` exactly (NOT the caller's token).
    - When `RunFreshPathAsync` throws → same.
    - When `RunBootstrapPathAsync` throws → same.
    - When `RunNormalPathAsync` throws → same.
    - The original exception is rethrown to the caller.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: RelationalBoxMigrationRunnerBase does NOT call CommitAsync or RollbackAsync when BeginAsync throws**
  - **USE COMMAND**: `/test-first when RelationalBoxMigrationRunnerBase BeginAsync throws it should not call CommitAsync or RollbackAsync and should still dispose the UoW via await using`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests`
  - Test file: `When_relational_box_migration_runner_base_begin_async_throws_it_should_skip_commit_and_rollback_and_still_dispose.cs`
  - Test should verify:
    - With a UoW spy whose `BeginAsync` throws — `CommitAsync` is NEVER called, `RollbackAsync` is NEVER called, `DisposeAsync` IS called.
    - The exception propagates to the caller of `MigrateAsync`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: RelationalBoxMigrationRunnerBase RedetectStateAsync default implementation calls DoesTableExistAsync then DoesHistoryExistAsync (and short-circuits when table missing)**
  - **USE COMMAND**: `/test-first when RelationalBoxMigrationRunnerBase RedetectStateAsync default runs it should call DoesTableExistAsync and only call DoesHistoryExistAsync when the table exists`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests`
  - Test file: `When_relational_box_migration_runner_base_redetect_state_default_runs_it_should_short_circuit_history_check_when_table_missing.cs`
  - Test should verify:
    - `DoesTableExistAsync = false` → `DoesHistoryExistAsync` NOT called → returns `(false, false)`.
    - `DoesTableExistAsync = true, DoesHistoryExistAsync = true` → returns `(true, true)`.
    - `DoesTableExistAsync = true, DoesHistoryExistAsync = false` → returns `(true, false)`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: RelationalBoxMigrationRunnerBase derived class can override RedetectStateAsync without overriding any other hook**
  - **USE COMMAND**: `/test-first when a derived runner overrides RedetectStateAsync to return constant true it should be invoked instead of the base default and the base algorithm should still run unchanged`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests`
  - Test file: `When_relational_box_migration_runner_base_redetect_state_is_overridden_it_should_use_the_override.cs`
  - Test should verify the virtual hook contract per ADR §B.2 (the third escape hatch — override `RedetectStateAsync` for non-standard detection model).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: RelationalBoxMigrationRunnerBase ValidateMigrationsMonotonic throws on non-monotonic migration list**
  - **USE COMMAND**: `/test-first when RelationalBoxMigrationRunnerBase MigrateAsync receives a non-monotonic migration list it should throw before opening any connection`
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests`
  - Test file: `When_relational_box_migration_runner_base_migrate_receives_non_monotonic_migrations_it_should_throw_before_opening_connection.cs`
  - Test should verify the monotonicity check from spec 0027 Items H/I/Q is preserved at the base level.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **Phase 6 gate: Abstract base class compiles on full shared-assembly TFM matrix; all base-level tests green**
  - `dotnet build src/Paramore.Brighter.BoxProvisioning` clean on `netstandard2.0;net8.0;net9.0;net10.0`.
  - All Phase-6 `/test-first` tests pass against the fake `TestRunner` derivative.
  - **No backend runner change yet** — Phase 7 picks up.

---

## Phase 7 — Refactor migration runners (per backend)

The four relational runners refactor to derive from `RelationalBoxMigrationRunnerBase`. The Spanner runner does NOT derive from the base (degenerate per ADR 0057 §6) but DOES rewire its detection-helper static calls to instance dispatch — same shape as the provisioner cascade in Phase 8. Without the Spanner rewire, Phase 8.6's grep gate is unsatisfiable.

Each relational backend's runner refactor is broken into three structural sub-steps (a/b/c) per Beck's Tidy First — each sub-step a separate commit, each compiles, each runs the existing per-backend BoxProvisioning test filter green before the next sub-step starts. This avoids a 400-line "TIDY FIRST" task that bundles ctor rewrite + 7 hook overrides + delete-old-orchestration in one go.

### 7.1 MSSQL runner

- [ ] **TIDY FIRST: 7.1a Introduce `MsSqlBoxMigrationRunner` derived shell that delegates to existing private methods (legacy delegates)**
  - File: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs`.
  - Change class declaration to `: RelationalBoxMigrationRunnerBase<SqlConnection, SqlTransaction>`.
  - Add new ctor `(MsSqlBoxDetectionHelper detectionHelper, IAmARelationalDatabaseConfiguration configuration, IMsSqlAdvisoryLock? advisoryLock = null, ILogger? logger = null, TimeSpan? lockTimeout = null)` — forwards `(detectionHelper, configuration, lockTimeout ?? default, logger)` to base ctor; stores `IMsSqlAdvisoryLock?` private field.
  - Implement each abstract hook by delegating to the existing private method body — rename the existing internal helpers to `*Legacy` for the duration so the override `OpenConnectionAsync` simply calls `OpenConnectionLegacyAsync(...)`, etc.
  - Rename the existing public `MigrateAsync` to `private MigrateLegacyAsync` (no longer overrides anything); the public surface is now the inherited `RelationalBoxMigrationRunnerBase.MigrateAsync` from the base, but the legacy orchestration is preserved internally for now.
  - Validation: existing 54/54 MSSQL tests stay green. Both the new base orchestration AND the legacy delegates coexist for one commit.
  - Commit: `refactor: spec 0028 Phase 7.1a — MsSqlBoxMigrationRunner derives from base via legacy-delegate hooks`.

- [ ] **TIDY FIRST: 7.1b Replace each legacy-delegate hook body with the cleaned override (one hook per commit)**
  - For each hook in turn (`OpenConnectionAsync`, `CreateUnitOfWorkAsync`, `LockResourceFor`, `EnsureHistoryTableAsync`, `RunFreshPathAsync`, `RunBootstrapPathAsync`, `RunNormalPathAsync`):
    - Move the corresponding logic from the `*Legacy` helper into the override body itself.
    - Adjust signatures to use base-supplied protected properties (`Configuration.ConnectionString`, `DetectionHelper.DetectCurrentVersionAsync(...)`) instead of locally-stored fields.
    - Delete the now-unused `*Legacy` helper.
    - Run MSSQL test filter — must stay 54/54 green.
    - Commit per hook: `refactor: spec 0028 Phase 7.1b — MsSqlBoxMigrationRunner override {HookName}` (seven commits).
  - Default `RedetectStateAsync` (do NOT override).

- [ ] **TIDY FIRST: 7.1c Delete the legacy `MigrateLegacyAsync` orchestration**
  - Remove `MigrateLegacyAsync` and any remaining try/catch/finally + lock + transaction lifecycle scaffolding now owned by the base.
  - Run MSSQL test filter — must stay 54/54 green.
  - Commit: `refactor: spec 0028 Phase 7.1c — MsSqlBoxMigrationRunner remove legacy orchestration`.

### 7.2 Postgres runner — apply the 7.1a/b/c recipe

- [ ] **TIDY FIRST: 7.2a Introduce `PostgreSqlBoxMigrationRunner` derived shell with `IPostgreSqlAdvisoryLock?` field; legacy delegates. Validation: 46/46 per TFM.**

- [ ] **TIDY FIRST: 7.2b Replace each legacy-delegate hook with the cleaned override (one commit per hook). Validation: 46/46 each commit.**

- [ ] **TIDY FIRST: 7.2c Delete the legacy `MigrateLegacyAsync` orchestration. Validation: 46/46.**

### 7.3 MySQL runner — apply the recipe

- [ ] **TIDY FIRST: 7.3a Introduce `MySqlBoxMigrationRunner` derived shell with `IMySqlAdvisoryLock?` field. MySQL UoW takes the lock primitive; transaction stays null per Phase 5.3. Validation: 50/50 net9.0-only.**

- [ ] **TIDY FIRST: 7.3b Replace each legacy-delegate hook (one commit per hook). Validation: 50/50 net9.0-only each commit.**

- [ ] **TIDY FIRST: 7.3c Delete the legacy `MigrateLegacyAsync` orchestration. Validation: 50/50 net9.0-only.**

### 7.4 SQLite runner — apply the recipe

- [ ] **TIDY FIRST: 7.4a Introduce `SqliteBoxMigrationRunner` derived shell. SQLite has no advisory-lock primitive — the derived ctor does NOT take `I*AdvisoryLock`. UoW ctor takes only connection + logger per Phase 5.4. Validation: 40/40 per TFM.**

- [ ] **TIDY FIRST: 7.4b Replace each legacy-delegate hook (one commit per hook). Validation: 40/40 each commit.**

- [ ] **TIDY FIRST: 7.4c Delete the legacy `MigrateLegacyAsync` orchestration. Validation: 40/40.**

### 7.5 Spanner runner — static-to-instance rewire (NOT derived from base)

Spanner is degenerate per ADR 0057 §6 and stays free-standing as `IAmABoxMigrationRunner`. It does NOT derive from `RelationalBoxMigrationRunnerBase`. But it DOES call `SpannerBoxDetectionHelpers` directly (`SpannerBoxMigrationRunner.cs:134, 137`); after Phase 2.5 the helper is an instance class, and after Phase 8.6 the static facade is deleted. Without rewiring the Spanner runner, Phase 8.6's grep gate becomes unsatisfiable.

- [ ] **TIDY FIRST: Refactor `SpannerBoxMigrationRunner` to take an injected detection helper and call instance methods on it**
  - File: `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs`.
  - New ctor parameter: `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction> detectionHelper` (BASE interface — Spanner is degenerate; no version inference per ADR 0057 §6).
  - Body changes: `SpannerBoxDetectionHelpers.{Method}(...)` → `_detectionHelper.{Method}(...)` with explicit `null` for the new `string? schemaName` parameter slot at every call-site.
  - Update `AddSpannerOutbox` / `AddSpannerInbox` extensions in `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxProvisioningExtensions.cs` to pass the detection helper into the runner ctor (the helper is also registered for the provisioners by Phase 9.5 — same singleton).
  - Validation: existing Spanner BoxProvisioning tests stay green per NF2 (26/26 per TFM).
  - Commit: `refactor: spec 0028 Phase 7.5 — SpannerBoxMigrationRunner takes injected detection helper`.

### 7.6 Phase 7 gate

- [ ] **Phase 7 gate: All four relational runners derive from base; Spanner runner rewires to instance helper; pre-Phase-7 counts preserved per NF2**
  - Run all six BoxProvisioning test filters; confirm counts ≥ `baseline.md` per backend per TFM (counts INCREASE due to new Phase-5/6/10 tests; Phase 7 itself adds no new tests).
  - Quote count delta vs `baseline.md` in the closing Phase 7 commit message.

---

## Phase 8 — Provisioner ctor cascade (per backend, per box-type)

Per ADR §A.1 source-break "Provisioner ctor cascade" + §A.4 step 6. Each provisioner's ctor gains three new parameters (relational eight) or two (Spanner pair — no catalogue), and its body switches from static-method calls to instance dispatch on the injected fields. Bridging shims from Phases 2/3/4 are removed in this phase.

### 8.1 MSSQL provisioners

- [ ] **TIDY FIRST: `MsSqlOutboxProvisioner` ctor gains three new typed parameters; body rewires static calls to instance dispatch**
  - File: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxProvisioner.cs`.
  - New ctor parameters: `IAmAVersionDetectingMigrationHelper<SqlConnection, SqlTransaction> detectionHelper`, `IAmABoxMigrationCatalog catalog`, `IAmABoxPayloadModeValidator<SqlConnection> payloadValidator`.
  - Body changes:
    - `MsSqlBoxDetectionHelpers.{Method}(...)` → `_detectionHelper.{Method}(...)`.
    - `MsSqlOutboxMigrations.All(config)` → `_catalog.All(config)`.
    - `MsSqlPayloadModeValidator.ValidateAsync(...)` → `_payloadValidator.ValidateAsync(...)`.
  - Validation: existing MSSQL outbox provisioner tests stay green.

- [ ] **TIDY FIRST: `MsSqlInboxProvisioner` ctor cascade — same recipe as MSSQL outbox, with `MsSqlInboxMigrationCatalog` injected as the `IAmABoxMigrationCatalog`**
  - File: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlInboxProvisioner.cs`.
  - Validation: existing MSSQL inbox provisioner tests stay green.

### 8.2 Postgres provisioners

- [ ] **TIDY FIRST: `PostgreSqlOutboxProvisioner` ctor cascade — three new typed params; rewire static→instance**
  - File: `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxProvisioner.cs`.
  - New ctor parameters: `IAmAVersionDetectingMigrationHelper<NpgsqlConnection, NpgsqlTransaction> detectionHelper`, `IAmABoxMigrationCatalog catalog` (Outbox catalogue), `IAmABoxPayloadModeValidator<NpgsqlConnection> payloadValidator`.
  - Body: `PostgreSqlBoxDetectionHelpers.{Method}(...)` → `_detectionHelper.{Method}(...)`; `PostgreSqlOutboxMigrations.All(config)` → `_catalog.All(config)`; `PostgreSqlPayloadModeValidator.ValidateAsync(...)` → `_payloadValidator.ValidateAsync(...)`.
  - Validation: existing Postgres outbox provisioner tests stay green.

- [ ] **TIDY FIRST: `PostgreSqlInboxProvisioner` ctor cascade — same recipe, with `PostgreSqlInboxMigrationCatalog` injected**
  - File: `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlInboxProvisioner.cs`.
  - Validation: existing Postgres inbox provisioner tests stay green.

### 8.3 MySQL provisioners

- [ ] **TIDY FIRST: `MySqlOutboxProvisioner` ctor cascade — three new typed params; rewire static→instance**
  - File: `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxProvisioner.cs`.
  - New ctor parameters: `IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction> detectionHelper`, `IAmABoxMigrationCatalog catalog` (Outbox), `IAmABoxPayloadModeValidator<MySqlConnection> payloadValidator`.
  - Body: same static→instance rewire pattern as Postgres above.
  - Validation: existing MySQL outbox provisioner tests stay green (net9.0-only).

- [ ] **TIDY FIRST: `MySqlInboxProvisioner` ctor cascade — same recipe with `MySqlInboxMigrationCatalog`**
  - File: `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlInboxProvisioner.cs`.
  - Validation: existing MySQL inbox provisioner tests stay green (net9.0-only).

### 8.4 SQLite provisioners

- [ ] **TIDY FIRST: `SqliteOutboxProvisioner` ctor cascade — three new typed params; rewire static→instance; payload validator now takes `string? schemaName` (passed as `null`)**
  - File: `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxProvisioner.cs`.
  - New ctor parameters: `IAmAVersionDetectingMigrationHelper<SqliteConnection, SqliteTransaction> detectionHelper`, `IAmABoxMigrationCatalog catalog` (Outbox), `IAmABoxPayloadModeValidator<SqliteConnection> payloadValidator`.
  - Body: rewire to instance; SQLite's payload validator signature now takes `string? schemaName` — always pass `null` (SQLite has no schema concept). Same `null`-pass convention applies everywhere SQLite calls the detection helper's schema-bearing methods.
  - Validation: existing SQLite outbox provisioner tests stay green.

- [ ] **TIDY FIRST: `SqliteInboxProvisioner` ctor cascade — same recipe with `SqliteInboxMigrationCatalog`**
  - File: `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteInboxProvisioner.cs`.
  - Validation: existing SQLite inbox provisioner tests stay green.

### 8.5 Spanner provisioners (catalogue OMITTED per ADR 0057 §6)

- [ ] **TIDY FIRST: `SpannerOutboxProvisioner` ctor gains TWO new parameters (detection helper + payload validator); NO catalogue**
  - File: `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxProvisioner.cs`.
  - New ctor parameters: `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction> detectionHelper` (BASE interface, NOT the version-detecting variant — Spanner is degenerate per ADR 0057 §6), `IAmABoxPayloadModeValidator<SpannerConnection> payloadValidator`.
  - Body changes: rewire `SpannerBoxDetectionHelpers.{Method}(...)` and `SpannerPayloadModeValidator.ValidateAsync(...)` to instance dispatch. Schema parameter passed as `null` everywhere.
  - Validation: existing Spanner outbox provisioner tests stay green.

- [ ] **TIDY FIRST: `SpannerInboxProvisioner` ctor cascade — same recipe (TWO new params, no catalogue)**
  - File: `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerInboxProvisioner.cs`.
  - Validation: existing Spanner inbox provisioner tests stay green.

### 8.6 Remove bridging shims

- [ ] **TIDY FIRST: Remove all bridging shims (`static class {Backend}BoxDetectionHelpers`, `static class {Backend}{Box}Migrations`, `static class {Backend}PayloadModeValidator`)**
  - All call-sites have rewired to instance dispatch via Phase 8 ctor cascade (and Phase 7 runner refactor — runner overrides call helpers via injected fields, not statics).
  - Verify with `grep -r "BoxDetectionHelpers\." src/` returns zero hits in the BoxProvisioning packages.
  - Verify with `grep -r "OutboxMigrations\.\|InboxMigrations\." src/` returns zero hits.
  - Verify with `grep -r "PayloadModeValidator\." src/` returns zero hits (against the static-class form).

### 8.7 Phase 8 gate

- [ ] **Phase 8 gate: All provisioner ctors updated; all bridging shims removed; existing tests green per NF2**
  - Run all six BoxProvisioning test filters — counts ≥ pre-Phase-0 baseline (now boosted by Phase 5/6/10 additions).

---

## Phase 9 — DI extension updates (per backend)

Per ADR §A.4 step 6 final paragraph + §A.4 the Alternatives section "instance interfaces with DI-registered default implementations". Each `Add{Backend}{Box}` registers the four role-impls as singletons (one detection helper + two catalogues per backend covers both Outbox and Inbox extensions; one payload validator) and supplies them to the provisioner ctor.

### 9.1 MSSQL

- [ ] **TIDY FIRST: `AddMsSqlOutbox` / `AddMsSqlInbox` register `MsSqlBoxDetectionHelper`, `MsSqlOutboxMigrationCatalog` (Outbox-only), `MsSqlInboxMigrationCatalog` (Inbox-only), `MsSqlPayloadModeValidator` as singletons; supply them to provisioner ctor**
  - File: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxProvisioningExtensions.cs`.
  - Detection helper + payload validator registered ONCE per backend (covered by both extensions; idempotent registration if both extensions called).
  - Catalogues registered per box-type (`MsSqlOutboxMigrationCatalog` only by `AddMsSqlOutbox`; `MsSqlInboxMigrationCatalog` only by `AddMsSqlInbox`).
  - Total: 4 DI registrations per backend per ADR Alternatives.
  - Same updates for `AddMsSqlOutbox(string connectionName)` / `AddMsSqlInbox(string connectionName)` overloads — confirm spec 0027 Phase 11 Items U-mssql-outbox / U-mssql-inbox connection-name overloads stay covered by their existing tests.
  - Validation: existing MSSQL connection-name tests + DI-registration tests green.

### 9.2 Postgres

- [ ] **TIDY FIRST: `AddPostgreSqlOutbox` / `AddPostgreSqlInbox` DI updates**

### 9.3 MySQL

- [ ] **TIDY FIRST: `AddMySqlOutbox` / `AddMySqlInbox` DI updates**

### 9.4 SQLite

- [ ] **TIDY FIRST: `AddSqliteOutbox` / `AddSqliteInbox` DI updates**

### 9.5 Spanner (NO catalogue registration)

- [ ] **TIDY FIRST: `AddSpannerOutbox` / `AddSpannerInbox` register `SpannerBoxDetectionHelper` and `SpannerPayloadModeValidator` as singletons (NO catalogue per ADR 0057 §6); supply them to Spanner provisioners AND to the Spanner runner**
  - Total: 2 DI registrations for Spanner (vs 4 for relational backends).
  - Note: the same `SpannerBoxDetectionHelper` singleton is consumed by both the provisioner pair (Phase 8.5) AND `SpannerBoxMigrationRunner` (Phase 7.5).

### 9.6 Phase 9 gate

- [ ] **Phase 9 gate: DI registers all role-impls as singletons; provisioners construct with role-typed dependencies; existing tests green per NF2**
  - Run all six BoxProvisioning test filters; confirm counts ≥ pre-Phase-0 baseline.
  - Confirm spec 0027 Phase 11 connection-name overload tests (Items U-*) stay green for all four relational backends.

---

## Phase 10 — Harmonised UoW lifecycle and cancellation contract tests (cross-backend)

Per ADR §B.3. These tests verify the harmonised contract is upheld uniformly across the four relational backends. Each backend gets its own copy of the contract test suite (Brighter convention for backend-specific tests). The tests run end-to-end via `RelationalBoxMigrationRunnerBase.MigrateAsync` to observe the contract through the runner.

### 10.1 Cancellation token discipline

- [ ] **TEST + IMPLEMENT: MSSQL — Caller's cancellation during a migration path triggers RollbackAsync(CancellationToken.None) preserving lock release**
  - **USE COMMAND**: `/test-first when MSSQL migration is cancelled mid-flight RollbackAsync should run with CancellationToken.None to ensure lock release completes`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_migration_is_cancelled_mid_flight_it_should_rollback_with_cancellation_token_none.cs`
  - Test should verify:
    - With a fake migration whose `ApplyAsync` is interrupted by token cancellation — `RollbackAsync` IS still invoked.
    - The `CancellationToken` passed to `RollbackAsync` is `CancellationToken.None` (NOT the cancelled token).
    - The MSSQL transaction-scoped lock IS released (verified by a subsequent acquisition succeeding).
    - The original `OperationCanceledException` is rethrown to the caller.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - The runner base's catch block already passes `CancellationToken.None` per Phase 6 implementation. This test exercises the contract end-to-end via MSSQL.
    - If test fails because rollback throws or lock leaks, fix in `MsSqlProvisioningUnitOfWork.RollbackAsync`.

- [ ] **TEST + IMPLEMENT: Postgres — Caller's cancellation mid-flight triggers RollbackAsync(CancellationToken.None) and pg_advisory_unlock**
  - **USE COMMAND**: `/test-first when Postgres migration is cancelled mid-flight RollbackAsync should run with CancellationToken.None and pg_advisory_unlock should release the lock`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_migration_is_cancelled_mid_flight_it_should_rollback_and_release_session_lock.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: MySQL — Caller's cancellation mid-flight triggers RELEASE_LOCK with CancellationToken.None**
  - **USE COMMAND**: `/test-first when MySQL migration is cancelled mid-flight RollbackAsync should run with CancellationToken.None and RELEASE_LOCK should free the GET_LOCK`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning`
  - Test file: `When_mysql_migration_is_cancelled_mid_flight_it_should_release_get_lock.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

- [ ] **TEST + IMPLEMENT: SQLite — Caller's cancellation mid-flight rolls back BEGIN IMMEDIATE releasing writer slot**
  - **USE COMMAND**: `/test-first when SQLite migration is cancelled mid-flight RollbackAsync should run with CancellationToken.None and the writer slot should be released`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning`
  - Test file: `When_sqlite_migration_is_cancelled_mid_flight_it_should_rollback_releasing_writer_slot.cs`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 10.2 BeginAsync-throws contract

- [ ] **TEST + IMPLEMENT: MSSQL — BeginAsync throwing during lock acquire surfaces MigrationLockDeadlockException without invoking commit/rollback**
  - **USE COMMAND**: `/test-first when MSSQL advisory lock acquire throws MigrationLockDeadlockException during BeginAsync it should propagate without the runner calling CommitAsync or RollbackAsync`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_advisory_lock_acquire_throws_during_begin_async_runner_should_not_call_commit_or_rollback.cs`
  - Test should verify:
    - With a fake `IMsSqlAdvisoryLock` whose `AcquireAsync` throws `MigrationLockDeadlockException` per spec 0027 Item N — the exception propagates from `MigrateAsync`.
    - `CommitAsync` and `RollbackAsync` are NEVER called on the UoW.
    - `DisposeAsync` IS called via `await using`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 10.3 Best-effort RollbackAsync after thrown CommitAsync

- [ ] **TEST + IMPLEMENT: MSSQL — When CommitAsync throws, RollbackAsync runs best-effort against the zombied transaction without throwing**
  - **USE COMMAND**: `/test-first when MSSQL CommitAsync throws RollbackAsync should be best-effort against the already finalised zombied transaction and not throw`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_commit_throws_rollback_should_be_best_effort_without_throwing.cs`
  - Test should verify:
    - With a spy that simulates `CommitAsync` throwing `InvalidOperationException` (transaction already finalised) — `RollbackAsync` runs without throwing.
    - The original commit exception propagates to the caller.
    - A Warning is logged (capturing logger).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**

### 10.4 Phase 10 gate

- [ ] **Phase 10 gate: Cross-backend lifecycle contract uniformly verified; all backend-specific lifecycle tests green**
  - Run all four relational backend BoxProvisioning test filters; verify the lifecycle/cancellation contract tests pass.
  - Confirm Spanner BoxProvisioning is unchanged (degenerate per ADR 0057 §6 — no UoW lifecycle to contract-check).

---

## Phase 11 — Documentation, release notes, PR description

- [ ] **Update `release_notes.md` with the spec 0028 source-breaks and additive surface (per AC7)**
  - File: `release_notes.md` (existing PR #4039 file).
  - Under the existing "Breaking Changes" section, enumerate per ADR §A.1 / §A.2 / §A.3 source-break bullets:
    - Detection helpers: `static class {Backend}BoxDetectionHelpers` → `public class {Backend}BoxDetectionHelper` (singular). All five backends. SpannerBoxDetectionHelpers visibility widened from `internal` to `public`.
    - MSSQL/PG/MySQL detection methods: `string schemaName` → `string? schemaName`.
    - SQLite/Spanner detection methods: gain `string? schemaName` parameter (positional re-order at call-sites).
    - All five backends: `GetTableColumnsAsync` return type `HashSet<string>` → `IReadOnlyCollection<string>`.
    - Migration catalogues: `static class {Backend}{Box}Migrations` → `public class {Backend}{Box}MigrationCatalog`. Eight classes (Spanner exempt).
    - Payload validators: `static class {Backend}PayloadModeValidator` → `public class {Backend}PayloadModeValidator`. SQLite/Spanner gain `string? schemaName` parameter.
    - Provisioner ctors: 10 provisioners gain new typed parameters (3 for relational eight; 2 for Spanner pair).
    - Runner ctors: 4 relational runners derive from `RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` and gain `IAmAVersionDetectingMigrationHelper`, `IAmARelationalDatabaseConfiguration`, `TimeSpan lockTimeout`, `ILogger?` ctor params (forwarded to base).
  - Under "Additive" section, enumerate the new public types: 5 role interfaces + 1 abstract base class + 4 UoW classes + 5 detection-helper classes (instance) + 8 catalogue classes + 5 payload-validator classes (instance).

- [ ] **Update PR #4039 description to enumerate spec 0028 work (per AC11)**
  - Add a new section "Spec 0028 Box Provisioning RDD Role Interfaces" with:
    - Link to ADR 0058.
    - Link to `specs/0028-box-provisioning-rdd-role-interfaces/`.
    - Note that spec 0028 is a fourth-pass review response on the same PR (per requirements C1).

- [ ] **Verify "Adding a new BoxProvisioning backend" section in ADR 0058 reflects shipped surface (per AC5)**
  - Locate the section using `grep -n "## Adding a new BoxProvisioning backend" docs/adr/0058-box-provisioning-rdd-role-interfaces.md` (line numbers drift as the ADR is edited; the section title is stable).
  - For each class name listed in the section (`{Backend}BoxDetectionHelper`, `{Backend}{Box}MigrationCatalog`, `{Backend}PayloadModeValidator`, `{Backend}ProvisioningUnitOfWork`, `{Backend}BoxMigrationRunner`, plus the abstract base `RelationalBoxMigrationRunnerBase`), confirm via `grep -rn "class {ClassName} " src/Paramore.Brighter.BoxProvisioning*` that each class exists with the documented name.
  - If any name drift is found, update the ADR section AND the implementation in the same commit so the doc and surface stay aligned.

---

## Phase 12 — Final verification and acceptance criteria sign-off

- [ ] **Run the full BoxProvisioning test matrix and confirm NF2 counts (per AC6)**
  - Six test filters: MSSQL 54+/54+ per TFM, Postgres 46+/46+ per TFM, MySQL 50+/50 net9.0-only, SQLite 40+/40 per TFM, Spanner 26/26 per TFM (unchanged), Core BoxProvisioning.Tests 23+/23 per TFM, Core BoxProvisioning 5/5.
  - Counts EXCEED baseline due to additive Phase 5/6/10 tests; record the new counts.
  - Spanner counts EQUAL baseline (Spanner unchanged per ADR 0057 §6).

- [ ] **Confirm no `InternalsVisibleTo` directives added (per AC8 / NF5)**
  - `grep -r "InternalsVisibleTo" src/Paramore.Brighter.BoxProvisioning*` returns no NEW entries beyond pre-Phase-0 baseline.

- [ ] **Confirm no test-only public surface introduced (per AC8 / NF6)**
  - Survey new public types from Phase 1/5/6 — any motivated by testability? If yes, document in ADR 0058 with trade-off (per NF6).
  - Expected outcome: no test-only public types.

- [ ] **Confirm naming convention compliance per AC9 / C4**
  - All five new role interfaces start with `IAmA*` per Brighter convention.
  - No deviation requiring justification (the ADR §A.1/§A.2/§A.3 already justifies naming choices in the Rationale sub-sections).

- [ ] **Confirm TFM matrix is unchanged per C6**
  - `grep -A2 "TargetFrameworks" src/Paramore.Brighter.BoxProvisioning*/Paramore.Brighter.BoxProvisioning.csproj src/Paramore.Brighter.BoxProvisioning*/*.csproj` matches pre-Phase-0 state.

- [ ] **Cross-walk F1..F9 against the shipped surface (per requirements.md functional requirements)**
  - For each functional requirement, list the implementing files / classes / sections in `specs/0028-box-provisioning-rdd-role-interfaces/traceability.md`.
  - F1 — confirm ADR 0058 exists with status Accepted, contains §A and §B sections.
  - F2 — confirm `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` interface present and implemented by 5 backend classes.
  - F3 — confirm `IAmABoxMigrationCatalog` interface present and implemented by 8 backend classes (Spanner exempt per ADR 0057 §6).
  - F4 — confirm `IAmABoxPayloadModeValidator<TConnection>` interface present and implemented by 5 backend classes.
  - F5 — confirm `IAmAProvisioningUnitOfWork<TTransaction>` interface present and implemented by 4 relational backend classes.
  - F6 — confirm `RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` abstract class present and 4 relational runners derive from it.
  - F7 — confirm harmonised UoW lifecycle / cancellation / disposal contract is exercised by at least one test per relational backend (Phase 10).
  - F8 — confirm "Adding a new BoxProvisioning backend" section in ADR 0058 enumerates the role interfaces from F2/F3/F4/F5 and the optional base from F6.
  - F9 — verify AC4 discharge: re-walk ADR §B.4 candidate list against the post-implementation surface; confirm the four "No" decisions still hold; record any new candidate (typically empty) in `specs/0028-box-provisioning-rdd-role-interfaces/sweep-result.md`. If a candidate IS surfaced during implementation, the spec scope expands and a new round of `/spec:review code` is required before approval.
  - Discharge any uncovered requirement before requesting `/spec:approve code`.

- [ ] **Tick spec acceptance criteria AC1..AC11 in `specs/0028-box-provisioning-rdd-role-interfaces/acceptance.md` (per-AC, not bulk)**
  - For each AC, record (a) the verifying artefact (test name / file path / commit sha / ADR section reference) and (b) the tick.
  - AC1 — ADR 0058 reviewed and Accepted (`docs/adr/0058-box-provisioning-rdd-role-interfaces.md` status = Accepted, `.design-approved` exists).
  - AC2 — F2/F3/F4 interfaces named with `IAmA*`, present in `src/Paramore.Brighter.BoxProvisioning/`, each with XML-doc; implementations across backends.
  - AC3 — `RelationalBoxMigrationRunnerBase` exists; `IAmAProvisioningUnitOfWork` exists with 4 backend impls; Spanner runner exemption documented.
  - AC4 — sweep result recorded per F9 task above (`sweep-result.md`).
  - AC5 — "Adding a new BoxProvisioning backend" section verified per Phase 11 task.
  - AC6 — backend test counts ≥ `baseline.md` per backend per TFM.
  - AC7 — `release_notes.md` enumerates source-breaks and additive surface (Phase 11 task).
  - AC8 — no new `InternalsVisibleTo`; no test-only public surface.
  - AC9 — naming convention: every new role interface starts with `IAmA*`.
  - AC10 — every new-behaviour task used `/test-first`; pure structural moves are TIDY FIRST commits with the test filter green before AND after. Verify via `git log {Phase 0 sha}..HEAD --oneline` that commits clearly distinguish.
  - AC11 — PR #4039 description updated with spec 0028 section.
  - Run `/spec:approve code` only after every AC is ticked.

---

## Appendix — Out-of-scope reminders

These items are explicitly NOT in spec 0028 scope (per requirements §Out of Scope and §Removed):

- Feedback item 2 (DI extensions role-based interface) — dropped to simplify.
- Feedback item 4 (provisioner finer-grained sub-role) — already met by `IAmABoxProvisioner` (ADR 0053).
- Adding a sixth backend (e.g. Oracle).
- Changes to spec 0027's logical migration chain or DDL.
- Changes to `IAmABoxProvisioner`, `IAmABoxMigrationRunner`, `IAmABoxMigration`, `IAmARelationalDatabaseConfiguration`, `I*AdvisoryLock`.
- Changes to migration-history table schema.
- New lock primitives or rollback strategies.
- Move of existing test-double types.
- New package / project / assembly creation.

If any of these surfaces during implementation, push back and ask the user before expanding scope.

**AC4 reactive obligation (NOT out of scope)**: per requirements F9 / AC4, if implementation surfaces an open-closed sweep candidate that ADR §B.4 missed, the spec scope EXPANDS to fold it in (or document the deferral with reason). Do NOT silently absorb such a candidate as scope creep — record it in `sweep-result.md` and ask the user whether to fold it into spec 0028 or defer to a follow-up.

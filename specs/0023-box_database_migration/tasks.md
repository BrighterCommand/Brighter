# Tasks: Box Database Migration (Spec 0023)

> **ADR**: [0053-box-database-migration.md](../../docs/adr/0053-box-database-migration.md)
> **Requirements**: [requirements.md](requirements.md)

## Prerequisites

These tasks address interface gaps and builder inconsistencies that must be resolved before the provisioning library can be built.

### Task 0.1: Add SchemaName to IAmARelationalDatabaseConfiguration

- [ ] **IMPLEMENT: Expose SchemaName on the relational database configuration interface**
  - `SchemaName` exists on `RelationalDatabaseConfiguration` but not on `IAmARelationalDatabaseConfiguration`
  - The provisioner code references `_configuration.SchemaName` through the interface
  - Add `string? SchemaName => null;` as a **default interface member** on `IAmARelationalDatabaseConfiguration` in `src/Paramore.Brighter/IAmARelationalDatabaseConfiguration.cs`
  - Using a default interface member avoids breaking any external implementors — existing implementations that do not override `SchemaName` will return `null`, which the provisioner interprets as the backend's default schema (e.g. `dbo` for MSSQL, `public` for PostgreSQL)
  - Update `StubSqlDbConfiguration` in `tests/Paramore.Brighter.Extensions.Tests/TestDifferentSetups.cs` to implement the new member
  - Verify existing code compiles — `RelationalDatabaseConfiguration` already has the property and will satisfy the interface implicitly

### Task 0.2: Fix Spanner Outbox Builder Missing Columns

- [ ] **IMPLEMENT: Add missing DataRef and SpecVersion columns to Spanner outbox builder**
  - `SpannerOutboxBuilder` is missing `DataRef` and `SpecVersion` columns that all other outbox builders include
  - Add `DataRef STRING(MAX)` and `SpecVersion STRING(10)` columns to both text and binary DDL templates in `src/Paramore.Brighter.Outbox.Spanner/SpannerOutboxBuilder.cs`
  - No migration concern — there are no known Spanner users per ADR

---

## Phase 1: Core Abstractions Package

Create `Paramore.Brighter.BoxProvisioning` with interfaces, records, hosted service, and registration extensions.

### Task 1.1: Core Interfaces and Types

- [ ] **IMPLEMENT: Create BoxProvisioning core package with interfaces and types**
  - Create new project `src/Paramore.Brighter.BoxProvisioning/Paramore.Brighter.BoxProvisioning.csproj`
  - Follow existing .csproj patterns (SDK, target frameworks from `$(BrighterCoreTargetFrameworks)`, nullable enable)
  - Reference `Paramore.Brighter` and `Microsoft.Extensions.Hosting.Abstractions`
  - Implement:
    - `BoxType` enum (Inbox, Outbox)
    - `BoxTableState` record (TableExists, HistoryExists, CurrentVersion)
    - `IAmABoxProvisioner` interface (BoxType, ProvisionAsync)
    - `IAmABoxMigrationRunner` interface (MigrateAsync)
    - `IAmABoxMigration` interface (Version, Description, UpScript)
    - `BoxMigration` record implementing `IAmABoxMigration`
    - `BoxProvisioningOptions` class (Add method, MigrationLockTimeout, internal Registrations)
  - Add project to solution file

### Task 1.2: Hosted Service Runs Provisioners at Startup

- [ ] **TEST + IMPLEMENT: BoxProvisioningHostedService runs all registered provisioners on StartAsync**
  - **USE COMMAND**: `/test-first when box provisioning hosted service starts it should run all registered provisioners`
  - Test location: `tests/Paramore.Brighter.Core.Tests/BoxProvisioning/`
  - Test file: `When_box_provisioning_hosted_service_starts_it_should_run_all_registered_provisioners.cs`
  - Test should verify:
    - Given two mock `IAmABoxProvisioner` instances registered
    - When `StartAsync` is called
    - Then both provisioners have `ProvisionAsync` called
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `BoxProvisioningHostedService` implementing `IHostedService`
    - Constructor takes `IEnumerable<IAmABoxProvisioner>` and `ILogger<BoxProvisioningHostedService>`
    - `StartAsync` iterates provisioners and calls `ProvisionAsync` on each
    - `StopAsync` returns `Task.CompletedTask`

### Task 1.3: Hosted Service Orders Outbox Before Inbox

- [ ] **TEST + IMPLEMENT: BoxProvisioningHostedService provisions outbox before inbox**
  - **USE COMMAND**: `/test-first when box provisioning hosted service starts it should provision outbox before inbox`
  - Test location: `tests/Paramore.Brighter.Core.Tests/BoxProvisioning/`
  - Test file: `When_box_provisioning_hosted_service_starts_it_should_provision_outbox_before_inbox.cs`
  - Test should verify:
    - Given an inbox provisioner and an outbox provisioner registered (inbox first in collection)
    - When `StartAsync` is called
    - Then the outbox provisioner is called before the inbox provisioner (track call order)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Order provisioners by `BoxType == BoxType.Outbox ? 0 : 1` before iterating

### Task 1.4: Hosted Service Wraps Failures in ConfigurationException

- [ ] **TEST + IMPLEMENT: BoxProvisioningHostedService wraps provisioner failures in ConfigurationException**
  - **USE COMMAND**: `/test-first when box provisioning fails it should throw configuration exception`
  - Test location: `tests/Paramore.Brighter.Core.Tests/BoxProvisioning/`
  - Test file: `When_box_provisioning_fails_it_should_throw_configuration_exception.cs`
  - Test should verify:
    - Given a provisioner that throws an `InvalidOperationException`
    - When `StartAsync` is called
    - Then a `ConfigurationException` is thrown with the original exception as inner exception
    - And the message includes the box type
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Catch exceptions from `ProvisionAsync`, log at Error level, wrap in `ConfigurationException`, re-throw

### Task 1.5: Registration Extension Wires Up DI

- [ ] **TEST + IMPLEMENT: UseBoxProvisioning registers provisioners and hosted service**
  - **USE COMMAND**: `/test-first when using box provisioning extension it should register hosted service and provisioners`
  - Test location: `tests/Paramore.Brighter.Core.Tests/BoxProvisioning/`
  - Test file: `When_using_box_provisioning_extension_it_should_register_hosted_service_and_provisioners.cs`
  - Test should verify:
    - Given a service collection with `AddBrighter()` called
    - When `UseBoxProvisioning(opts => opts.Add(...))` is called with a test provisioner registration
    - Then `IHostedService` includes `BoxProvisioningHostedService`
    - And the test provisioner is resolvable as `IAmABoxProvisioner`
    - And calling `UseBoxProvisioning` twice does not register the hosted service twice
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `BrighterBuilderBoxProvisioningExtensions` with `UseBoxProvisioning` method
    - Execute the configure action to collect registrations from `BoxProvisioningOptions`
    - Apply each registration to `IServiceCollection`
    - Use `TryAddEnumerable` for the hosted service to prevent duplicates

---

## Phase 2: MSSQL Backend

Create `Paramore.Brighter.BoxProvisioning.MsSql` as the first backend implementation.

### Task 2.1: MSSQL Backend Project Setup

- [ ] **IMPLEMENT: Create MsSql BoxProvisioning project with migration definitions**
  - Create new project `src/Paramore.Brighter.BoxProvisioning.MsSql/Paramore.Brighter.BoxProvisioning.MsSql.csproj`
  - Reference `Paramore.Brighter.BoxProvisioning`, `Paramore.Brighter.Inbox.MsSql`, `Paramore.Brighter.Outbox.MsSql`
  - Reference `Microsoft.Data.SqlClient`
  - Implement:
    - `MsSqlOutboxMigrations.All(config)` — version 1 reuses `SqlOutboxBuilder.GetDDL()`
    - `MsSqlInboxMigrations.All(config)` — version 1 reuses `SqlInboxBuilder.GetDDL()`
    - `MsSqlBoxProvisioningExtensions` — extension methods on `BoxProvisioningOptions` for `AddMsSqlOutbox` and `AddMsSqlInbox` (both explicit config and connection-name overloads)
  - Add project to solution file

### Task 2.2: MSSQL Outbox Provisioner Creates Table on Fresh Database

- [ ] **TEST + IMPLEMENT: MsSql outbox provisioner creates outbox table when none exists**
  - **USE COMMAND**: `/test-first when mssql outbox provisioner runs on fresh database it should create outbox table`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`
  - Test should verify:
    - Given a MSSQL database with no outbox table (use unique table name with UUID)
    - When `ProvisionAsync` is called on `MsSqlOutboxProvisioner`
    - Then the outbox table exists in the database
    - And `__BrighterMigrationHistory` contains a version 1 row for this table
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `MsSqlOutboxProvisioner` implementing `IAmABoxProvisioner`
    - Implement `DetectTableStateAsync` with `DoesTableExistAsync` using `sys.tables`
    - Create `MsSqlBoxMigrationRunner` implementing `IAmABoxMigrationRunner`
    - Runner creates `__BrighterMigrationHistory` table, acquires `sp_getapplock`, runs migrations in transaction
  - **Note**: This is the largest single task — it implements the full provisioner + runner for MSSQL

### Task 2.3: MSSQL Outbox Provisioner Is Idempotent

- [ ] **TEST + IMPLEMENT: MsSql outbox provisioner is idempotent when table already at latest version**
  - **USE COMMAND**: `/test-first when mssql outbox provisioner runs on already provisioned database it should be idempotent`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_outbox_provisioner_runs_on_already_provisioned_database_it_should_be_idempotent.cs`
  - Test should verify:
    - Given an outbox table already provisioned (run `ProvisionAsync` once)
    - When `ProvisionAsync` is called a second time
    - Then no error occurs
    - And the migration history still has exactly one version-1 row (no duplicates)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `DetectTableStateAsync` detects table exists + history exists → returns `BoxTableState(true, true, 1)`
    - Runner skips all migrations up to and including `CurrentVersion`

### Task 2.4: MSSQL Outbox Provisioner Bootstraps Pre-Migration Installation

- [ ] **TEST + IMPLEMENT: MsSql outbox provisioner bootstraps existing table without migration history**
  - **USE COMMAND**: `/test-first when mssql outbox provisioner finds existing table without history it should bootstrap`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`
  - Test should verify:
    - Given an outbox table created directly via `SqlOutboxBuilder.GetDDL()` (simulating pre-migration install)
    - And no `__BrighterMigrationHistory` table
    - When `ProvisionAsync` is called
    - Then `__BrighterMigrationHistory` is created with a synthetic version-1 row
    - And the outbox table is unchanged (no duplicate CREATE attempt)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `DetectTableStateAsync` detects table exists + no history → calls `DetectCurrentVersionAsync`
    - `DetectCurrentVersionAsync` introspects columns via `INFORMATION_SCHEMA.COLUMNS` to determine version
    - Runner receives `BoxTableState(true, false, 1)` → inserts synthetic history rows, skips version-1 migration

### Task 2.5: MSSQL Outbox Provisioner Validates Payload Mode

- [ ] **TEST + IMPLEMENT: MsSql outbox provisioner fails when payload mode mismatches existing table**
  - **USE COMMAND**: `/test-first when mssql outbox provisioner detects payload mode mismatch it should throw`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_outbox_provisioner_detects_payload_mode_mismatch_it_should_throw.cs`
  - Test should verify:
    - Given a text-mode outbox table already exists
    - When provisioner is configured with `binaryMessagePayload = true`
    - Then `ProvisionAsync` throws `ConfigurationException` with a message about payload mode mismatch
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - After detecting table exists, introspect `Body` column type via `INFORMATION_SCHEMA.COLUMNS`
    - Compare against expected type for configured payload mode (NVARCHAR(MAX) vs VARBINARY(MAX))
    - Throw `ConfigurationException` on mismatch

### Task 2.5a: Extract Shared Payload Validation Helper (Structural)

- [ ] **REFACTOR: Extract shared payload validation logic into a reusable helper**
  - This is a **structural change only** (no new behavior) — separated from Task 2.6 per tidy-first guidelines
  - Extract the payload mode validation logic from `MsSqlOutboxProvisioner` (implemented in Task 2.5) into a shared helper method or class (e.g. `MsSqlPayloadModeValidator`) that can be reused by both outbox and inbox provisioners
  - The helper should accept: connection, table name, schema name, column name (e.g. `Body` or `CommandBody`), expected binary mode, and cancellation token
  - All existing tests from Task 2.5 must continue to pass unchanged

### Task 2.6: MSSQL Inbox Provisioner Validates Payload Mode

- [ ] **TEST + IMPLEMENT: MsSql inbox provisioner fails when payload mode mismatches existing table**
  - **USE COMMAND**: `/test-first when mssql inbox provisioner detects payload mode mismatch it should throw`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_inbox_provisioner_detects_payload_mode_mismatch_it_should_throw.cs`
  - Test should verify:
    - Given a text-mode inbox table already exists
    - When provisioner is configured with `binaryMessagePayload = true`
    - Then `ProvisionAsync` throws `ConfigurationException` with a message about payload mode mismatch
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - After detecting table exists, introspect `CommandBody` column type via `INFORMATION_SCHEMA.COLUMNS`
    - Compare against expected type for configured payload mode (NVARCHAR(MAX) vs VARBINARY(MAX))
    - Throw `ConfigurationException` on mismatch
    - Reuse the shared payload validation helper extracted in Task 2.5a

### Task 2.7: MSSQL Inbox Provisioner Creates Table on Fresh Database

- [ ] **TEST + IMPLEMENT: MsSql inbox provisioner creates inbox table when none exists**
  - **USE COMMAND**: `/test-first when mssql inbox provisioner runs on fresh database it should create inbox table`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table.cs`
  - Test should verify:
    - Given a MSSQL database with no inbox table
    - When `ProvisionAsync` is called on `MsSqlInboxProvisioner`
    - Then the inbox table exists in the database
    - And `__BrighterMigrationHistory` contains a version 1 row for this table
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `MsSqlInboxProvisioner` implementing `IAmABoxProvisioner` (mirrors outbox pattern)
    - Reuse `MsSqlBoxMigrationRunner` (already created in Task 2.2)

### Task 2.8: MSSQL Registration Extensions Resolve from IConfiguration

- [ ] **TEST + IMPLEMENT: MsSql box provisioning extensions resolve connection string from IConfiguration**
  - **USE COMMAND**: `/test-first when mssql box provisioning uses connection name it should resolve from configuration`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_box_provisioning_uses_connection_name_it_should_resolve_from_configuration.cs`
  - Test should verify:
    - Given `IConfiguration` with a connection string named "BrighterDb"
    - When `AddMsSqlOutbox("BrighterDb")` is used in `UseBoxProvisioning`
    - Then the provisioner resolves the connection string at runtime from `IConfiguration`
    - And provisioning succeeds (table is created)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - The connection-name overload of `AddMsSqlOutbox` registers a factory delegate
    - Factory resolves `IConfiguration` from `IServiceProvider` and calls `GetConnectionString(connectionName)`
    - Creates `RelationalDatabaseConfiguration` and `MsSqlOutboxProvisioner` at resolution time

### Task 2.9: MSSQL Migration Runner Handles Concurrent Instances

- [ ] **TEST + IMPLEMENT: MsSql migration runner serializes concurrent migration attempts**
  - **USE COMMAND**: `/test-first when multiple mssql provisioners run concurrently they should not corrupt state`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_multiple_mssql_provisioners_run_concurrently_they_should_not_corrupt_state.cs`
  - Test should verify:
    - Given a fresh database with no outbox table
    - When two `MsSqlOutboxProvisioner` instances run `ProvisionAsync` concurrently (via `Task.WhenAll`)
    - Then the outbox table exists exactly once
    - And `__BrighterMigrationHistory` contains exactly one version-1 row (no duplicates)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `MsSqlBoxMigrationRunner` acquires `sp_getapplock` with resource `BrighterMigration_{tableName}` in Exclusive mode
    - Lock is held for the entire migration run within the transaction
    - Second instance blocks until lock is released, then finds no pending migrations

---

## Phase 3: PostgreSQL Backend

Create `Paramore.Brighter.BoxProvisioning.PostgreSql` as the second backend to validate the abstraction.

### Task 3.1: PostgreSQL Outbox Provisioner Creates Table on Fresh Database

- [ ] **TEST + IMPLEMENT: PostgreSql outbox provisioner creates outbox table when none exists**
  - **USE COMMAND**: `/test-first when postgresql outbox provisioner runs on fresh database it should create outbox table`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgresql_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`
  - Test should verify:
    - Given a PostgreSQL database with no outbox table
    - When `ProvisionAsync` is called on `PostgreSqlOutboxProvisioner`
    - Then the outbox table exists
    - And `__BrighterMigrationHistory` contains version 1 for this table
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create project `src/Paramore.Brighter.BoxProvisioning.PostgreSql/`
    - Create `PostgreSqlOutboxProvisioner`, `PostgreSqlBoxMigrationRunner`
    - `PostgreSqlOutboxMigrations.All()` reuses `PostgreSqlOutboxBuilder.GetDDL()`
    - Runner uses `pg_try_advisory_lock(74726, hashtext(...))` for concurrency control (two-argument form with Brighter namespace constant)
    - `DoesTableExistAsync` uses `INFORMATION_SCHEMA.TABLES`

### Task 3.2: PostgreSQL Outbox Provisioner Bootstraps Pre-Migration Installation

- [ ] **TEST + IMPLEMENT: PostgreSql outbox provisioner bootstraps existing table without history**
  - **USE COMMAND**: `/test-first when postgresql outbox provisioner finds existing table without history it should bootstrap`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgresql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`
  - Test should verify:
    - Given an outbox table created directly via `PostgreSqlOutboxBuilder.GetDDL()`
    - When `ProvisionAsync` is called
    - Then `__BrighterMigrationHistory` contains a synthetic version-1 row
    - And the outbox table is unchanged
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `DetectCurrentVersionAsync` introspects columns via `INFORMATION_SCHEMA.COLUMNS`
    - Bootstrap path inserts synthetic history rows

### Task 3.3: PostgreSQL Inbox Provisioner Creates Table on Fresh Database

- [ ] **TEST + IMPLEMENT: PostgreSql inbox provisioner creates inbox table when none exists**
  - **USE COMMAND**: `/test-first when postgresql inbox provisioner runs on fresh database it should create inbox table`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgresql_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table.cs`
  - Test should verify:
    - Given a PostgreSQL database with no inbox table
    - When `ProvisionAsync` is called on `PostgreSqlInboxProvisioner`
    - Then the inbox table exists
    - And migration history contains version 1
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `PostgreSqlInboxProvisioner` and `PostgreSqlInboxMigrations`
    - Reuse `PostgreSqlInboxBuilder.GetDDL()` for version-1 migration

### Task 3.4: PostgreSQL Outbox Provisioner Is Idempotent

- [ ] **TEST + IMPLEMENT: PostgreSql outbox provisioner is idempotent when table already at latest version**
  - **USE COMMAND**: `/test-first when postgresql outbox provisioner runs on already provisioned database it should be idempotent`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgresql_outbox_provisioner_runs_on_already_provisioned_database_it_should_be_idempotent.cs`
  - Test should verify:
    - Given an outbox table already provisioned (run `ProvisionAsync` once)
    - When `ProvisionAsync` is called a second time
    - Then no error occurs
    - And the migration history still has exactly one version-1 row (no duplicates)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Validates the PostgreSQL runner correctly detects `BoxTableState(true, true, 1)` and skips all migrations
    - This confirms the abstraction works across SQL dialects (MSSQL uses `sys.tables`, PostgreSQL uses `INFORMATION_SCHEMA`)

### Task 3.5: PostgreSQL Migration Runner Handles Concurrent Instances

- [ ] **TEST + IMPLEMENT: PostgreSql migration runner serializes concurrent migration attempts**
  - **USE COMMAND**: `/test-first when multiple postgresql provisioners run concurrently they should not corrupt state`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_multiple_postgresql_provisioners_run_concurrently_they_should_not_corrupt_state.cs`
  - Test should verify:
    - Given a fresh database
    - When two provisioners run concurrently
    - Then table exists once and history has exactly one version-1 row
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `PostgreSqlBoxMigrationRunner` uses `pg_try_advisory_lock(74726, hashtext(...))` with retry loop (two-argument form with Brighter namespace constant `74726`)
    - Retry budget bounded by `MigrationLockTimeout`
    - Throws `TimeoutException` if lock not acquired within timeout

---

## Phase 4: MySQL Backend

### Task 4.1: MySQL Outbox Provisioner Creates Table on Fresh Database

- [ ] **TEST + IMPLEMENT: MySql outbox provisioner creates outbox table when none exists**
  - **USE COMMAND**: `/test-first when mysql outbox provisioner runs on fresh database it should create outbox table`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`
  - Test should verify:
    - Given a MySQL database with no outbox table
    - When `ProvisionAsync` is called on `MySqlOutboxProvisioner`
    - Then the outbox table exists
    - And migration history contains version 1
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create project `src/Paramore.Brighter.BoxProvisioning.MySql/`
    - Create `MySqlOutboxProvisioner`, `MySqlBoxMigrationRunner`
    - Runner uses `GET_LOCK('BrighterMigration_{tableName}', timeout)` for concurrency
    - `DoesTableExistAsync` uses `information_schema.tables`

### Task 4.2: MySQL Migration Runner Handles Concurrent Instances

- [ ] **TEST + IMPLEMENT: MySql migration runner serializes concurrent migration attempts via GET_LOCK**
  - **USE COMMAND**: `/test-first when multiple mysql provisioners run concurrently they should not corrupt state`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_multiple_mysql_provisioners_run_concurrently_they_should_not_corrupt_state.cs`
  - Test should verify:
    - Given a fresh database with no outbox table
    - When two `MySqlOutboxProvisioner` instances run `ProvisionAsync` concurrently (via `Task.WhenAll`)
    - Then the outbox table exists exactly once
    - And `__BrighterMigrationHistory` contains exactly one version-1 row (no duplicates)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `MySqlBoxMigrationRunner` uses `GET_LOCK('BrighterMigration_{tableName}', timeout)` where timeout is `(int)MigrationLockTimeout.TotalSeconds`
    - Lock is session-level — held on the same connection for the entire migration run
    - Releases lock with `RELEASE_LOCK` after migrations complete

### Task 4.3: MySQL Inbox Provisioner and Bootstrap

- [ ] **TEST + IMPLEMENT: MySql inbox provisioner creates table and handles bootstrap**
  - **USE COMMAND**: `/test-first when mysql inbox provisioner runs it should create table or bootstrap existing`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_inbox_provisioner_runs_it_should_create_table_or_bootstrap_existing.cs`
  - Test should verify:
    - Given a fresh MySQL database, provisioner creates inbox table with history
    - Given a pre-existing inbox table without history, provisioner bootstraps with synthetic row
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `MySqlInboxProvisioner` and `MySqlInboxMigrations`
    - `DetectCurrentVersionAsync` uses `information_schema.columns` for column introspection

---

## Phase 5: SQLite Backend

### Task 5.1: SQLite Outbox Provisioner Creates Table on Fresh Database

- [ ] **TEST + IMPLEMENT: Sqlite outbox provisioner creates outbox table when none exists**
  - **USE COMMAND**: `/test-first when sqlite outbox provisioner runs on fresh database it should create outbox table`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`
  - Test should verify:
    - Given a SQLite database with no outbox table
    - When `ProvisionAsync` is called on `SqliteOutboxProvisioner`
    - Then the outbox table exists
    - And migration history contains version 1
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create project `src/Paramore.Brighter.BoxProvisioning.Sqlite/`
    - Create `SqliteOutboxProvisioner`, `SqliteBoxMigrationRunner`
    - SQLite uses file-level locking (implicit serialization — no advisory lock needed)
    - `DoesTableExistAsync` uses `sqlite_master`
    - `DetectCurrentVersionAsync` uses `pragma_table_info`

### Task 5.2: SQLite Inbox Provisioner and Bootstrap

- [ ] **TEST + IMPLEMENT: Sqlite inbox provisioner creates table and handles bootstrap**
  - **USE COMMAND**: `/test-first when sqlite inbox provisioner runs it should create table or bootstrap existing`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_inbox_provisioner_runs_it_should_create_table_or_bootstrap_existing.cs`
  - Test should verify:
    - Given a fresh SQLite database, provisioner creates inbox table with history
    - Given a pre-existing inbox table, provisioner bootstraps with synthetic row
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `SqliteInboxProvisioner` and `SqliteInboxMigrations`

---

## Phase 6: Spanner Backend

### Task 6.1: Spanner Outbox Provisioner Creates Table on Fresh Database

- [ ] **TEST + IMPLEMENT: Spanner outbox provisioner creates outbox table when none exists**
  - **USE COMMAND**: `/test-first when spanner outbox provisioner runs on fresh database it should create outbox table`
  - Test location: `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/`
  - Test file: `When_spanner_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`
  - Test should verify:
    - Given a Spanner database with no outbox table
    - When `ProvisionAsync` is called
    - Then the outbox table exists
    - And migration history contains version 1
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create project `src/Paramore.Brighter.BoxProvisioning.Spanner/`
    - Create `SpannerOutboxProvisioner`, `SpannerBoxMigrationRunner`
    - DDL operations via `ExecuteDdlAsync` (separate from read-write transactions)
    - History updates in normal read-write transactions
    - Catch "already exists" errors on DDL to handle crash-between-DDL-and-history-write

### Task 6.2: Spanner Inbox Provisioner Creates Table

- [ ] **TEST + IMPLEMENT: Spanner inbox provisioner creates inbox table when none exists**
  - **USE COMMAND**: `/test-first when spanner inbox provisioner runs on fresh database it should create inbox table`
  - Test location: `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/`
  - Test file: `When_spanner_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table.cs`
  - Test should verify:
    - Given a Spanner database with no inbox table
    - When `ProvisionAsync` is called
    - Then the inbox table exists and migration history contains version 1
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `SpannerInboxProvisioner` and `SpannerInboxMigrations`

---

## Phase 7: Sample Update

### Task 7.1: Update WebAPI Samples to Use Box Provisioning

- [ ] **IMPLEMENT: Replace DbMaker inbox/outbox creation with UseBoxProvisioning in WebAPI samples**
  - Update `samples/WebAPI/` samples to use the new `UseBoxProvisioning()` API
  - Remove inbox/outbox creation from `samples/WebAPI/WebAPI_Common/DbMaker/SchemaCreation.cs`
  - Keep `DbMaker` for sample-specific application database creation (FluentMigrator)
  - Verify samples still start and function correctly with the new provisioning approach
  - Update any relevant sample documentation

---

## Task Dependencies

```
Task 0.1, 0.2 ──→ Task 1.1 ──→ Tasks 1.2, 1.3, 1.4, 1.5 (can be parallel)
                                        │
                                        ▼
                                  Task 2.1 ──→ Task 2.2 ──→ Tasks 2.3–2.5, 2.7–2.9 (can be parallel after 2.2)
                                                                │
                                                          Task 2.5 ──→ Task 2.5a (structural) ──→ Task 2.6
                                                                │
                                                                ▼
                                                          Tasks 3.x (PostgreSQL)
                                                                │
                                                                ▼
                                                          Tasks 4.x (MySQL)
                                                          Tasks 5.x (SQLite)    (4, 5, 6 can be parallel)
                                                          Tasks 6.x (Spanner)
                                                                │
                                                                ▼
                                                          Task 7.1 (Samples)
```

## Risk Mitigation

- **Concurrent migration safety** is explicitly tested for MSSQL (Task 2.9), PostgreSQL (Task 3.5), and MySQL (Task 4.2) — the three backends with distinct locking mechanisms
- **Bootstrap correctness** is tested per backend (Tasks 2.4, 3.2, 4.3, 5.2)
- **Payload mode validation** prevents silent data corruption for both outbox (Task 2.5) and inbox (Task 2.6)
- **Idempotency** is verified for MSSQL (Task 2.3) and PostgreSQL (Task 3.4) to validate across SQL dialects
- **Interface change** (Task 0.1) is done first as a prerequisite to unblock all provisioner code

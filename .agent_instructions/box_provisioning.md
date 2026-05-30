# Box Provisioning and Migration

## Overview

Brighter includes a box provisioning system that creates and migrates Outbox and Inbox database tables at application startup. The system is modular with a core abstractions package and per-backend implementations. Specs 0023 (initial framework), 0027 (versioned migration chain), and 0028 (role interfaces + template-method bases) shipped together on the `database_migration` branch / PR #4039.

> Developer-facing companion guides live in `docs/guides/`:
> - [box-provisioning-adding-columns.md](../docs/guides/box-provisioning-adding-columns.md) — workflow for adding a new Outbox/Inbox column across all backends.
> - [box-provisioning-new-backend.md](../docs/guides/box-provisioning-new-backend.md) — workflow for implementing a brand-new BoxProvisioning backend that supports migrations.

## ⚠ Mandatory Rule: Adding a Column Requires a New Migration

> Every column added to a `*OutboxBuilder` or `*InboxBuilder` MUST ship with a new `V(N+1)` `BoxMigration` entry in the corresponding `*MigrationCatalog` class for the same backend.

A column added to the builder DDL without a matching migration entry is a schema drift. Existing deployments that already passed the previous fresh-install path will never run the new ALTER, so their tables fall behind silently and runtime SQL fails with "invalid column name" the next time the new column is read or written.

The new `BoxMigration` entry MUST populate:

- **`Version`** — strictly `previous_version + 1` (no gaps); same V-number across all backends for outbox columns where the column lands on every backend.
- **`LogicalColumns`** — the cumulative column set after this migration applies (V_N's columns ∪ the new column). Used by drift detection and by `IAmAVersionDetectingMigrationHelper.DetectCurrentVersionAsync` for legacy-table version inference.
- **`SourceReference`** — the commit SHA (and PR number where available) that introduced the column. Required from V2 onwards; V1 stays `null`.
- **`IdempotencyCheckSql`** — SQLite only (its grammar lacks `ALTER TABLE ADD COLUMN IF NOT EXISTS`, so the runner needs an explicit existence probe). MSSQL / PostgreSQL / MySQL bake the existence check into the `UpScript` itself and leave `IdempotencyCheckSql` `null`.
- **`UpScript`** — provider-appropriate idempotent ALTER:
  - **MSSQL**: `IF COL_LENGTH(N'[{schema}].[{table}]', N'{col}') IS NULL ALTER TABLE [{schema}].[{table}] ADD [{col}] {type} NULL;`
  - **PostgreSQL**: `ALTER TABLE {schema}.{table} ADD COLUMN IF NOT EXISTS {col} {type} NULL;`
  - **MySQL**: `information_schema.columns` existence check + prepared statement (`ALTER TABLE` cannot be parameterised).
  - **SQLite**: plain `ALTER TABLE [{table}] ADD COLUMN [{col}] {type} NULL;`. The runner skips the ALTER if `IdempotencyCheckSql` returns `> 0`.

Drift detection runs in CI: each backend's `When_*_builder_is_compared_to_*_migration_columns_*` test asserts `latest_migration.LogicalColumns ∪ housekeeping == DdlColumnExtractor.GetExpectedColumns(builder.GetDDL(...))`. The build fails if a column lands on the builder without a matching migration entry. See `tests/Paramore.Brighter.BoxProvisioning.Tests/Drift/` (parser) and `tests/Paramore.Brighter.{Backend}.Tests/BoxProvisioning/Drift/` (per-backend housekeeping + assertions).

For the per-backend migration history (V1..V7 outbox uniform across the four relational backends, V1..V2 inbox on MSSQL/MySQL/SQLite, V1-only inbox on Postgres) and rationale, see:
- [ADR 0057](../docs/adr/0057-box-schema-versioning-and-migrations.md) — versioning model, three-path runner, advisory locks, Spanner exemption.
- [ADR 0058](../docs/adr/0058-box-provisioning-rdd-role-interfaces.md) — role-based interfaces (RDD) and template-method abstract bases.
- [ADR 0059](../docs/adr/0059-box-provisioning-abstract-base-naming-symmetry.md) — `SqlBoxMigrationRunner` / `SqlBoxProvisioner` naming symmetry (no `*Base` suffix).
- [Spec 0027 README](../specs/0027-box-schema-versioning-and-migrations/README.md) — archaeology of the V1..V7 chain.
- [Spec 0028 README](../specs/0028-box-provisioning-rdd-role-interfaces/README.md) — RDD refresh + abstract-base pull-up.

## Package Structure

- **Core**: `src/Paramore.Brighter.BoxProvisioning/` — interfaces, abstract bases, hosted service, DI extensions.
- **Backends**: `src/Paramore.Brighter.BoxProvisioning.{MsSql,PostgreSql,MySql,Sqlite,Spanner}/`.
- **Existing builders**: `src/Paramore.Brighter.Outbox.{MsSql,PostgreSql,MySql,Sqlite,Spanner}/` and `src/Paramore.Brighter.Inbox.{...}/` (DDL generation; consumed by V1 migrations and by green-field installs).

## Key Abstractions (`src/Paramore.Brighter.BoxProvisioning/`)

| Type | Role |
|------|------|
| `IAmABoxMigration` | One versioned migration step (`Version`, `Description`, `UpScript`, `LogicalColumns`, `SourceReference`, `IdempotencyCheckSql`). |
| `BoxMigration` (record) | Default implementation of `IAmABoxMigration`. |
| `IAmABoxMigrationCatalog` | Per-backend, per-box-type ordered chain of migrations. Method: `IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration)`. |
| `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` | Probes: `DoesTableExistAsync`, `DoesHistoryExistAsync`, `GetMaxVersionAsync`, `GetTableColumnsAsync`, `DiscriminatorFor(BoxType)`. |
| `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` | Extends detection helper with `DetectCurrentVersionAsync` for legacy-table bootstrap. Relational only; Spanner exempt. |
| `IAmABoxPayloadModeValidator<TConnection>` | Validates configured payload mode (binary/text) against live column type. |
| `IAmAProvisioningUnitOfWork` | Transaction + advisory-lock lifecycle owned by the per-backend UoW. Spanner exempt. |
| `IAmABoxProvisioner` | Per (backend × box-type) entry point; `ProvisionAsync(CancellationToken)`. |
| `IAmABoxMigrationRunner` | Applies pending migrations under lock and writes history rows. |
| `SqlBoxProvisioner<TConnection, TTransaction>` | Abstract base for the 8 relational provisioners (4 backends × Outbox/Inbox). Owns the orchestration body; derived classes supply only `CreateConnection` + `PayloadColumnName`. Spanner pair is free-standing. |
| `SqlBoxMigrationRunner<TConnection, TTransaction>` | Abstract base for the 4 relational migration runners (template-method pattern; sealed `MigrateAsync` orchestration with abstract hooks). Spanner runner is free-standing. |
| `BoxTableState` (record) | Snapshot of `(TableExists, HistoryExists, CurrentVersion)`. |
| `BoxType` | `Outbox` or `Inbox`. |
| `BoxProvisioningHostedService` | `IHostedService` — runs all registered provisioners at startup (Outbox before Inbox). |
| `BoxProvisioningOptions` | Configuration builder threaded through `UseBoxProvisioning(o => ...)`. |
| `BrighterBuilderBoxProvisioningExtensions` | `UseBoxProvisioning(this IBrighterBuilder, Action<BoxProvisioningOptions>)`. Single-call contract enforced by `BoxProvisioningMarker`. |
| `Identifiers.AssertSafe(identifier, parameterName)` | Defence-in-depth chokepoint validating SQL identifiers against `^[A-Za-z][A-Za-z0-9_]*$`. Rejects leading digits and leading underscores (Spanner reserves `_`-prefixed names). |

## Per-Backend Layout

For each backend, the package `Paramore.Brighter.BoxProvisioning.{Backend}` contains the canonical family of files:

| File | Role / Base |
|------|-------------|
| `{Backend}OutboxMigrationCatalog.cs`, `{Backend}InboxMigrationCatalog.cs` | `IAmABoxMigrationCatalog` |
| `{Backend}BoxDetectionHelper.cs` | `IAmAVersionDetectingMigrationHelper<TConn, TTx>` (Spanner: base `IAmABoxMigrationDetectionHelper` only) |
| `{Backend}OutboxProvisioner.cs`, `{Backend}InboxProvisioner.cs` | `SqlBoxProvisioner<TConn, TTx>` (Spanner: implements `IAmABoxProvisioner` directly) |
| `{Backend}BoxMigrationRunner.cs` | `SqlBoxMigrationRunner<TConn, TTx>` (Spanner: implements `IAmABoxMigrationRunner` directly) |
| `{Backend}PayloadModeValidator.cs` | `IAmABoxPayloadModeValidator<TConn>` |
| `{Backend}ProvisioningUnitOfWork.cs` | `IAmAProvisioningUnitOfWork` (Spanner: no UoW — optimistic concurrency) |
| `{Backend}BoxProvisioningExtensions.cs` | DI extensions — `Add{Backend}Outbox`, `Add{Backend}Inbox` |

## Migration History

Applied migrations are tracked in `__BrighterMigrationHistory` with a composite primary key of (SchemaName, BoxTableName, MigrationVersion). Pre-migration tables are bootstrapped with synthetic history rows based on column introspection via `DetectCurrentVersionAsync`.

*Spanner exception:* the Spanner backend uses `BrighterMigrationHistory` (no leading underscores) because Spanner GoogleSQL rejects identifiers beginning with `_`. The framework-wide `Identifiers.AssertSafe` chokepoint applies the same strictest-backend rule to user-supplied table and schema names.

**Current versions**: V7 outbox uniform across the four relational backends (MSSQL/PG/MySQL/SQLite); V2 inbox on MSSQL/MySQL/SQLite; V1-only inbox on Postgres (shipped with `ContextKey` from day one — see ADR 0057 §1). Spanner is fresh-install-only (no V_k chain — ADR 0057 §6).

## Adding New Columns to the Outbox or Inbox

When Brighter needs a new column on the Outbox (or Inbox), the following files must be updated across **all 4 relational backends** (MSSQL, PostgreSQL, MySQL, SQLite) and the **Spanner builder** (no Spanner migration is added — see ADR 0057 §6).

### 1. Migration catalogs

Append a new `BoxMigration` with the next version number to each backend's catalog. See the **Mandatory Rule** section above for required fields. Also extend the `s_v(N+1)AddedColumns` array and the `Cumulative(int)` helper that builds `LogicalColumns`.

| Box Type | Files                                                          |
|----------|----------------------------------------------------------------|
| Outbox   | `BoxProvisioning.{Backend}/{Backend}OutboxMigrationCatalog.cs` |
| Inbox    | `BoxProvisioning.{Backend}/{Backend}InboxMigrationCatalog.cs`  |

**Rules:**
- Version numbers must match across all four relational backends for outbox columns that land everywhere (Postgres inbox is V1-only by design — ADR 0057 §1).
- New columns must be nullable or have a `DEFAULT` value (the runner cannot apply NOT-NULL adds against existing rows).
- Use the provider-appropriate idempotent ALTER syntax — the runner may re-apply `UpScript` when a deployment has been bootstrapped from a legacy table and is mid-chain on a re-run.
- Update both text and binary payload variants of the builder DDL if applicable.
- Keep V1's `UpScript` pointed at the live builder DDL — it is the fresh-install fast path and stays in sync with the builder. Only V1's `LogicalColumns` changes on V1.

### 2. Version detection — **no change required**

`IAmAVersionDetectingMigrationHelper.DetectCurrentVersionAsync` (implemented once per relational backend in `{Backend}BoxDetectionHelper.cs`) walks the migration list `V_latest..V1` and returns the first version whose `LogicalColumns` is a subset of the table's actual columns. Detection is data-driven from `LogicalColumns`; do not edit detection helpers when adding a column.

### 3. Initial DDL builders

Update the CREATE TABLE DDL in the existing builder classes so new installations get the complete schema:

| Box Type | Files                                          |
|----------|-------------------------------------------------|
| Outbox   | `Outbox.{Backend}/{Backend}OutboxBuilder.cs`    |
| Inbox    | `Inbox.{Backend}/{Backend}InboxBuilder.cs`      |

**The Spanner builder MUST also be updated** — it is the only path by which Spanner installations receive the new column.

### 4. Read/write code (if the column is exercised at runtime)

Update INSERT and SELECT statements plus result mapping in the Outbox / Inbox implementation classes under `src/Paramore.Brighter.Outbox.{Backend}/` and `src/Paramore.Brighter.Inbox.{Backend}/`. These are **separate** from the `BoxProvisioning.{Backend}` projects.

### 5. Tests

Write tests for: migration application, idempotency, bootstrap detection, data round-trip, and **drift detection** — the per-backend drift test under `tests/Paramore.Brighter.{Backend}.Tests/BoxProvisioning/Drift/` will go RED the moment the builder changes in step 3 without a matching catalog entry from step 1, and flip GREEN when both are in place.

## Concurrency Control

Each backend uses database-specific locking during migrations:

| Backend    | Mechanism                              | UoW class                              |
|------------|----------------------------------------|----------------------------------------|
| MSSQL      | `sp_getapplock` (transaction-scoped)   | `MsSqlProvisioningUnitOfWork`          |
| PostgreSQL | `pg_try_advisory_lock` (session-scoped)| `PostgreSqlProvisioningUnitOfWork`     |
| MySQL      | `GET_LOCK` (session-scoped)            | `MySqlProvisioningUnitOfWork`          |
| SQLite     | `BEGIN IMMEDIATE` writer-slot          | `SqliteProvisioningUnitOfWork`         |
| Spanner    | Optimistic (single-stmt transactions)  | — (no UoW; per ADR 0057 §6)            |

## DI Registration

```csharp
services
    .AddBrighter()
    .UseBoxProvisioning(opts =>
    {
        opts.AddMsSqlOutbox(configuration);
        opts.AddMsSqlInbox(configuration);
        // Optional: override the default 30s migration lock timeout
        opts.MigrationLockTimeout = TimeSpan.FromMinutes(2);
    });
```

`UseBoxProvisioning` enforces a single-call contract — a second invocation throws `ConfigurationException`. Configure all outboxes and inboxes inside one delegate. The hosted service (`BoxProvisioningHostedService`) is registered via `TryAddEnumerable` and runs at app start, in Outbox-then-Inbox order, blocking until all provisioners complete.

## Per-Schema History Placement (spec 0029)

By default `__BrighterMigrationHistory` lives in the backend default schema (`dbo` on MSSQL, `public` on PostgreSQL, the connection-bound database on MySQL) regardless of `IAmARelationalDatabaseConfiguration.SchemaName`. Set `BoxProvisioningOptions.MigrationHistoryScope = MigrationHistoryScope.PerSchema` to opt this deployment into per-tenant placement: on MSSQL and PostgreSQL the history table is created inside the configured `SchemaName`, co-located with the tenant's box tables inside its isolation/backup boundary. The default `MigrationHistoryScope.Global` is byte-for-byte today's behaviour — no operator action is required to upgrade.

```csharp
services
    .AddBrighter()
    .UseBoxProvisioning(opts =>
    {
        opts.MigrationHistoryScope = MigrationHistoryScope.PerSchema;
        opts.AddMsSqlOutbox(configuration);    // history lands in configuration.SchemaName
        opts.AddPostgreSqlInbox(configuration);
    });
```

| Backend       | `PerSchema` behaviour                                                          |
|---------------|--------------------------------------------------------------------------------|
| MSSQL         | History created in `SchemaName`. Null `SchemaName` throws `ConfigurationException`. |
| PostgreSQL    | History created in `SchemaName` (folded to lowercase per PG semantics). Null `SchemaName` throws `ConfigurationException`. |
| MySQL         | No-op — schema equals database; history stays in the connection-bound database. |
| SQLite        | No-op — no schema concept; history stays at the database-file level.            |
| Spanner       | No-op — degenerate fresh-install-only model per ADR 0057 §6.                    |

Each provisioning run emits an `Information` log of the form `Box migration history for {BoxTable} resolved to schema {HistorySchema} (scope {Scope})` so operators can confirm placement without inspecting the database; on the no-op backends `HistorySchema` is the literal `<backend default>`.

### Global → PerSchema flip (ADR 0060 D5)

On the first run after flipping a previously-`Global` deployment to `PerSchema`, the MSSQL/PG runners auto-seed the new per-schema history table from the legacy default-schema history so existing migrations are not re-applied. The seed:

- Runs under the same advisory lock and transaction as the CREATE.
- Copies only this tenant's rows (`WHERE SchemaName=@schemaName AND BoxTableName=@boxTableName`) for all five history columns.
- Uses a composite-primary-key `NOT EXISTS` guard, so re-running the flip is idempotent.
- Emits a distinct `Information` log: `Seeded {RowCount} legacy history row(s) for {BoxTable} from {LegacySchema} to {TargetSchema}` plus an OpenTelemetry `Activity` event `legacy_history_seeded` (constant `BrighterSemanticConventions.BoxMigrationEventLegacyHistorySeeded`) carrying the row count as the `brighter.box.migration.seed.rows` tag.

**Permission requirement.** The seed requires `SELECT` on the legacy default-schema history table (`dbo.__BrighterMigrationHistory` on MSSQL, `public.__BrighterMigrationHistory` on PG). When the database role lacks that grant the runner surfaces a `ConfigurationException` with the inner provider exception attached — provision the grant (or run the first PerSchema migration under a role that has it) and retry. The error message names the documented operator-facing phrase so it can be alerted on.

**Reverse flip (`PerSchema → Global`) and cleanup of the legacy rows are out of scope.** The per-schema history table remains in the tenant's schema if a deployment is later switched back to `Global`; operators wanting to reclaim that storage must drop the table themselves. Legacy rows in the default-schema history are likewise left in place after a successful PerSchema flip — they are harmless but redundant and can be deleted with an ad-hoc `DELETE` if desired.

Per-tenant identifiers flow through `Identifiers.AssertSafe` before any DDL is emitted, so an injection-shaped `SchemaName` is rejected at the provisioner entry well before reaching the runner or the database. See [ADR 0060](../docs/adr/0060-multi-tenancy-migration-history-scope.md) for the full decision record (D1–D6) and the spec at [specs/0029-multi-tenancy-migrations/](../specs/0029-multi-tenancy-migrations/) for the requirements + task breakdown.

## For Maintainers — Long-form playbooks

- Adding a column: `docs/guides/box-provisioning-adding-columns.md` (worked example, file-by-file checklist).
- Adding a new backend that supports migrations: `docs/guides/box-provisioning-new-backend.md` (role-interface checklist, abstract-base wiring, drift-test setup, Spanner-exemption decisions).
- Historical: `specs/0023-box_database_migration/adding-outbox-columns.md` (legacy long-form; superseded by the `docs/guides` guides — kept for archaeology).

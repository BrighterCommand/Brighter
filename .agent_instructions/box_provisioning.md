# Box Provisioning and Migration

## Overview

Brighter includes a box provisioning system that creates and migrates Outbox and Inbox database tables at application startup. The system is modular with a core abstractions package and per-backend implementations.

## ⚠ Mandatory Rule: Adding a Column Requires a New Migration

> Every column added to a `*OutboxBuilder` or `*InboxBuilder` MUST ship with a new `V(N+1)` `BoxMigration` entry in the corresponding `*Migrations` class for the same backend.

A column added to the builder DDL without a matching migration entry is a schema drift. Existing deployments that already passed the previous fresh-install path will never run the new ALTER, so their tables fall behind silently and runtime SQL fails with "invalid column name" the next time the new column is read or written.

The new `BoxMigration` entry MUST populate:

- **`Version`** — strictly `previous_version + 1` (no gaps); same V-number across all backends for outbox columns where the column lands on every backend.
- **`LogicalColumns`** — the cumulative column set after this migration applies (V_N's columns ∪ the new column). Used by drift detection and version inference for legacy tables.
- **`SourceReference`** — the commit SHA (and PR number where available) that introduced the column. Required from V2 onwards; V1 stays `null`.
- **`IdempotencyCheckSql`** — SQLite only (its grammar lacks `ALTER TABLE ADD COLUMN IF NOT EXISTS`, so the runner needs an explicit existence probe). MSSQL / PostgreSQL / MySQL bake the existence check into the `UpScript` itself and leave `IdempotencyCheckSql` `null`.
- **`UpScript`** — provider-appropriate idempotent ALTER:
  - **MSSQL**: `IF COL_LENGTH(N'[{schema}].[{table}]', N'{col}') IS NULL ALTER TABLE [{schema}].[{table}] ADD [{col}] {type} NULL;`
  - **PostgreSQL**: `ALTER TABLE {schema}.{table} ADD COLUMN IF NOT EXISTS {col} {type} NULL;`
  - **MySQL**: `information_schema.columns` existence check + prepared statement (`ALTER TABLE` cannot be parameterised).
  - **SQLite**: plain `ALTER TABLE [{table}] ADD COLUMN [{col}] {type} NULL;`. The runner skips the ALTER if `IdempotencyCheckSql` returns `> 0`.

Drift detection runs in CI: each backend's `When_*_builder_is_compared_to_*_migration_columns_*` test asserts `latest_migration.LogicalColumns ∪ housekeeping == DdlColumnExtractor.GetExpectedColumns(builder.GetDDL(...))`. The build fails if a column lands on the builder without a matching migration entry. See `tests/Paramore.Brighter.BoxProvisioning.Tests/Drift/`.

For the per-backend migration history (V1..V7 outbox, V1..V2 inbox on MSSQL/MySQL/SQLite, V1-only inbox on Postgres) and rationale, see [ADR 0057](../docs/adr/0057-box-schema-versioning-and-migrations.md) and [spec 0027 README](../specs/0027-box-schema-versioning-and-migrations/README.md).

## Package Structure

- **Core**: `src/Paramore.Brighter.BoxProvisioning/` - interfaces, hosted service, DI extensions
- **Backends**: `src/Paramore.Brighter.BoxProvisioning.{MsSql,PostgreSql,MySql,Sqlite,Spanner}/`
- **Existing builders**: `src/Paramore.Brighter.Outbox.{MsSql,PostgreSql,MySql,Sqlite,Spanner}/` (DDL generation)

## Key Abstractions

- `IAmABoxProvisioner` - creates or migrates a single box table
- `IAmABoxMigration` - defines a versioned migration (Version, Description, UpScript)
- `IAmABoxMigrationRunner` - executes pending migrations with locking and history tracking
- `BoxTableState` - captures whether a table exists, has history, and its current version
- `BoxProvisioningHostedService` - runs all provisioners at startup (Outbox before Inbox)

## Migration History

Applied migrations are tracked in `__BrighterMigrationHistory` with a composite primary key of (SchemaName, BoxTableName, MigrationVersion). Pre-migration tables are bootstrapped with synthetic history rows based on column introspection.

*Spanner exception:* the Spanner backend uses `BrighterMigrationHistory` (no leading underscores) because Spanner GoogleSQL rejects identifiers beginning with `_` (reserved for system objects).

## Adding New Columns to the Outbox or Inbox

When Brighter needs a new column on the Outbox (or Inbox), the following files must be updated across **all 5 backends** (MSSQL, PostgreSQL, MySQL, SQLite, Spanner):

### 1. Migration definitions

Add a new `BoxMigration` with the next version number to each backend's migration list. See the **Mandatory Rule** section above for the required fields (`Version`, `LogicalColumns`, `SourceReference`, `IdempotencyCheckSql`, idempotent `UpScript`).

| Box Type | Files                                                    |
|----------|----------------------------------------------------------|
| Outbox   | `BoxProvisioning.{Backend}/{Backend}OutboxMigrations.cs` |
| Inbox    | `BoxProvisioning.{Backend}/{Backend}InboxMigrations.cs`  |

**Rules:**
- Version numbers must match across all backends for the outbox (Postgres inbox is V1-only by design — see ADR 0057 §1)
- New columns must be nullable or have a DEFAULT value (the runner cannot apply NOT-NULL adds against existing rows)
- Use provider-appropriate idempotent ALTER TABLE syntax — the runner re-applies `UpScript` when a deployment has been bootstrapped from a legacy table and is mid-chain on a re-run
- Update both text and binary payload variants if applicable
- Keep V1's `UpScript` pointed at the live builder DDL — it is the fresh-install fast path and stays in sync with the builder; only `LogicalColumns` changes on V1

### 2. Version detection

Update `DetectCurrentVersionAsync()` in each provisioner to detect the new column via introspection. This enables bootstrapping pre-migration tables.

| Box Type | Files                                                     |
|----------|-----------------------------------------------------------|
| Outbox   | `BoxProvisioning.{Backend}/{Backend}OutboxProvisioner.cs`  |
| Inbox    | `BoxProvisioning.{Backend}/{Backend}InboxProvisioner.cs`   |

### 3. Initial DDL builders

Update the CREATE TABLE DDL in the existing builder classes so new installations get the complete schema:

| Box Type | Files                                          |
|----------|-------------------------------------------------|
| Outbox   | `Outbox.{Backend}/{Backend}OutboxBuilder.cs`    |
| Inbox    | `Inbox.{Backend}/{Backend}InboxBuilder.cs`      |

### 4. Read/write code (if column is used at runtime)

Update INSERT and SELECT statements and result mapping in the Outbox/Inbox implementation classes.

### 5. Tests

Write tests for: migration application, idempotency, bootstrap detection, data round-trip, and **drift detection** — the per-backend `*BuilderDriftTests` must continue to assert the new column set after the migration is added (the test will go RED until the new migration ships, then flip GREEN). Spec 0027 Phases 1–4 cover the drift-detection pattern per backend; mirror the existing test class for any new backend.

## Concurrency Control

Each backend uses database-specific locking during migrations:

| Backend    | Mechanism                    |
|------------|------------------------------|
| MSSQL      | `sp_getapplock` (exclusive)  |
| PostgreSQL | `pg_try_advisory_lock`       |
| MySQL      | `GET_LOCK` (session-level)   |
| SQLite     | Implicit file-level locking  |
| Spanner    | Optimistic (transactions)    |

## DI Registration

```csharp
services.AddBrighter()
    .UseBoxProvisioning(opts => {
        opts.AddMsSqlOutbox(config);
        opts.AddMsSqlInbox(config);
    });
```

For full details, see `specs/0023-box_database_migration/adding-outbox-columns.md`.

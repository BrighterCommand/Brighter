# Box Provisioning and Migration

## Overview

Brighter includes a box provisioning system that creates and migrates Outbox and Inbox database tables at application startup. The system is modular with a core abstractions package and per-backend implementations.

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

## Adding New Columns to the Outbox or Inbox

When Brighter needs a new column on the Outbox (or Inbox), the following files must be updated across **all 5 backends** (MSSQL, PostgreSQL, MySQL, SQLite, Spanner):

### 1. Migration definitions

Add a new `BoxMigration` with the next version number to each backend's migration list:

| Box Type | Files                                                    |
|----------|----------------------------------------------------------|
| Outbox   | `BoxProvisioning.{Backend}/{Backend}OutboxMigrations.cs` |
| Inbox    | `BoxProvisioning.{Backend}/{Backend}InboxMigrations.cs`  |

**Rules:**
- Version numbers must match across all backends
- New columns must be nullable or have a DEFAULT value
- Use provider-appropriate ALTER TABLE syntax
- Update both text and binary payload variants if applicable

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

Write tests for: migration application, idempotency, bootstrap detection, and data round-trip.

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

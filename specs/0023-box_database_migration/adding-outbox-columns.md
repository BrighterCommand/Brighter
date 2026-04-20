# Adding New Columns to the Outbox (or Inbox)

This document describes the process a Brighter maintainer must follow to add a new column to the Outbox (or Inbox) schema. The same process applies to both box types.

## Overview

The box provisioning system uses a versioned migration approach. Each database backend maintains its own list of migrations with matching version numbers. When a new column is needed, a new migration must be added to every backend, the version detection logic must be updated, and the initial DDL builders should be updated for consistency.

## Step-by-Step Process

### 1. Add a New Migration to Each Backend

Each backend has a `*OutboxMigrations.cs` (or `*InboxMigrations.cs`) file containing a static `All()` method that returns an ordered list of `BoxMigration` records. Append a new migration with the next version number.

**Files to update (for Outbox):**

| Backend    | File                                                                          |
|------------|-------------------------------------------------------------------------------|
| MSSQL      | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrations.cs`        |
| PostgreSQL | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxMigrations.cs` |
| MySQL      | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxMigrations.cs`        |
| SQLite     | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxMigrations.cs`      |
| Spanner    | `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxMigrations.cs`    |

**Example (MSSQL):**

```csharp
public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
{
    return [
        new BoxMigration(
            Version: 1,
            Description: "Create outbox table",
            UpScript: SqlOutboxBuilder.GetDDL(
                config.OutBoxTableName,
                config.BinaryMessagePayload)),

        // NEW MIGRATION
        new BoxMigration(
            Version: 2,
            Description: "Add CustomField column",
            UpScript: @$"ALTER TABLE [{config.OutBoxTableName}] ADD [CustomField] NVARCHAR(255) NULL;")
    ];
}
```

**Rules:**

- **Version numbers must match across all backends.** If MSSQL is Version 2, PostgreSQL, MySQL, SQLite, and Spanner must also be Version 2.
- **New columns must be nullable** (or have a DEFAULT value) to avoid breaking existing data.
- **Use provider-appropriate DDL syntax** (e.g., `ALTER TABLE ... ADD COLUMN` for PostgreSQL/MySQL/SQLite, `ALTER TABLE ... ADD` for MSSQL).
- **Update both text and binary payload variants** if the builder has separate DDL for each (check the existing `GetDDL()` method).

### 2. Update Version Detection in Each Provisioner

Each provisioner (e.g., `MsSqlOutboxProvisioner`) has a `DetectCurrentVersionAsync()` method that introspects table columns to determine the current schema version. This is used to bootstrap pre-migration tables (tables created before the migration system existed) by inserting synthetic history rows.

**Files to update (for Outbox):**

| Backend    | File                                                                          |
|------------|-------------------------------------------------------------------------------|
| MSSQL      | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxProvisioner.cs`       |
| PostgreSQL | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxProvisioner.cs` |
| MySQL      | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxProvisioner.cs`       |
| SQLite     | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxProvisioner.cs`     |
| Spanner    | `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxProvisioner.cs`   |

**Example:**

```csharp
internal static async Task<int> DetectCurrentVersionAsync(
    SqlConnection connection, string tableName, string schemaName,
    CancellationToken cancellationToken)
{
    // Check for the newest column first, then work backwards
    var hasCustomField = await ColumnExistsAsync(
        connection, tableName, schemaName, "CustomField", cancellationToken);
    if (hasCustomField) return 2;

    // Version 1 is the original CREATE TABLE
    return 1;
}
```

### 3. Update the Initial DDL Builders

The existing builder classes (e.g., `SqlOutboxBuilder`) generate the Version 1 CREATE TABLE DDL. While the migration system handles upgrades for existing tables, updating the builders ensures that new green-field installations get the complete schema in a single CREATE TABLE statement (the migration runner still records this as Version 1, and the ALTER from Version 2 becomes a no-op if the column already exists -- or you can design the migration script to be conditional).

**Files to update (for Outbox):**

| Backend    | File                                                              |
|------------|-------------------------------------------------------------------|
| MSSQL      | `src/Paramore.Brighter.Outbox.MsSql/SqlOutboxBuilder.cs`         |
| PostgreSQL | `src/Paramore.Brighter.Outbox.PostgreSql/PostgreSqlOutboxBuilder.cs` |
| MySQL      | `src/Paramore.Brighter.Outbox.MySql/MySqlOutboxBuilder.cs`       |
| SQLite     | `src/Paramore.Brighter.Outbox.Sqlite/SqliteOutboxBuilder.cs`     |
| Spanner    | `src/Paramore.Brighter.Outbox.Spanner/SpannerOutboxBuilder.cs`   |

Update both the text and binary DDL string templates to include the new column.

### 4. Update the Outbox Read/Write Code

If the new column should be populated or read by Brighter at runtime, update the relevant Outbox implementation classes:

- The `Add`/`AddAsync` methods (INSERT statements)
- The `Get`/`GetAsync` methods (SELECT statements and result mapping)
- The `Message` or related model classes if the column maps to a new property

### 5. Write Tests

- **Migration test**: Verify that the ALTER TABLE migration applies cleanly to an existing Version 1 table.
- **Idempotency test**: Verify that running provisioning twice with the new migration is safe.
- **Bootstrap test**: Verify that a pre-migration table (created via the old builders) is correctly detected and bootstrapped, then migrated.
- **Round-trip test**: If the column is read/written at runtime, verify data round-trips correctly.

## Checklist

- [ ] New `BoxMigration` added to all 5 backend `*OutboxMigrations.cs` files with matching version numbers
- [ ] `DetectCurrentVersionAsync()` updated in all 5 backend `*OutboxProvisioner.cs` files
- [ ] DDL builders updated in all 5 `*OutboxBuilder.cs` files (both text and binary variants)
- [ ] Outbox read/write code updated if the column is used at runtime
- [ ] Tests written for migration, idempotency, and bootstrap scenarios
- [ ] If adding to Inbox as well, repeat all steps for `*InboxMigrations.cs`, `*InboxProvisioner.cs`, and `*InboxBuilder.cs`

## Architecture Reference

For full details on the migration system architecture, see:

- **Core abstractions**: `src/Paramore.Brighter.BoxProvisioning/`
- **Migration history table**: `__BrighterMigrationHistory` (tracks applied versions per box table)
- **Concurrency control**: Each backend uses database-specific locking (sp_getapplock, pg_advisory_lock, GET_LOCK, etc.)
- **Startup orchestration**: `BoxProvisioningHostedService` runs provisioners at application startup (Outbox before Inbox)

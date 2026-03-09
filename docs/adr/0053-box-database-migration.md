# 53. Box Database Migration

Date: 2026-03-01

## Status

Accepted

## Context

**Parent Requirement**: [specs/0023-box_database_migration/requirements.md](../../specs/0023-box_database_migration/requirements.md)

**Scope**: This ADR covers the architecture for a modular library that creates and migrates Inbox and Outbox database tables, and integrates with .NET Aspire for connection management.

Brighter provides static builder classes (`SqlInboxBuilder`, `MySqlOutboxBuilder`, etc.) that return DDL strings for creating inbox and outbox tables. However, there are significant gaps:

1. **No migration support**: The builders only support initial table creation. When Brighter adds columns between versions (e.g. `DataRef`, `SpecVersion` were added to outbox schemas), there is no mechanism to evolve existing tables. Developers must manually alter their schemas.

2. **No unified abstraction**: Each builder has a different static method signature:

| Builder | `GetExistsQuery` Parameters | Notes |
|---------|---------------------------|-------|
| `SqlInboxBuilder` (MSSQL) | `(tableName, schemaName)` | Uses `sys.tables` |
| `MySqlInboxBuilder` | `(tableSchema, tableName)` | Uses `information_schema` |
| `PostgreSqlInboxBuilder` | `(tableSchema, tableName)` | Uses `INFORMATION_SCHEMA` |
| `SqliteInboxBuilder` | `(tableName)` | Uses `sqlite_master` |
| `SpannerInboxBuilder` | *none — missing* | No exists check |

3. **Sample-only orchestration**: The only code that wires builders together lives in `samples/WebAPI/WebAPI_Common/DbMaker/SchemaCreation.cs`. It mixes concerns (application DB creation, FluentMigrator, inbox, outbox) and cannot be reused as a library.

### How Inbox/Outbox Are Currently Configured

Today, inbox and outbox instances are constructed by the application before DI registration:

```csharp
// Application constructs the outbox directly
var config = new RelationalDatabaseConfiguration(connectionString, outBoxTableName: "Outbox");
var outbox = new MsSqlOutbox(config, new MsSqlConnectionProvider(config));

// Then passes it into Brighter's builder
services.AddBrighter()
    .AddProducers(configure =>
    {
        configure.Outbox = outbox;
        configure.TransactionProvider = typeof(MsSqlTransactionProvider);
        configure.ConnectionProvider = typeof(MsSqlConnectionProvider);
    });
```

All relational inbox/outbox implementations accept `IAmARelationalDatabaseConfiguration` (which holds connection string, table name, schema name, and payload format) and optionally an `IAmARelationalDbConnectionProvider`.

The builder pattern in Brighter uses `IBrighterBuilder` (returned by `AddBrighter()`) with extension methods like `AddProducers()`, `UseOutboxSweeper()`. This is the natural extension point for box provisioning.

## Decision

### 1. Role-Based Architecture

The design follows Responsibility-Driven Design with clear roles:

```
┌───────────────────────────────────────────────────────────┐
│  Application Startup                                       │
│                                                            │
│  services.AddBrighter()                                    │
│      .AddProducers(...)                                    │
│      .UseBoxProvisioning(options => {                      │
│          options.AddMsSqlOutbox(config);                   │
│          options.AddMsSqlInbox(config);                    │
│      });                                                   │
│                                                            │
│  // or with Aspire:                                        │
│  services.AddBrighter()                                    │
│      .AddProducers(...)                                    │
│      .UseBoxProvisioning(options => {                      │
│          options.AddMsSqlOutbox();  // connection from     │
│      });                            // Aspire discovery    │
└───────────────────┬───────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────┐
│  BoxProvisioningHostedService : IHostedService              │
│  Role: Coordinator                                         │
│  Responsibility: Runs all registered IAmABoxProvisioner     │
│  instances at startup before the app begins processing     │
└───────────────────┬───────────────────────────────────────┘
                    │
         ┌──────────┼──────────┐
         ▼          ▼          ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│MsSqlOutbox   │ │MsSqlInbox    │ │PostgreSqlOut │
│Provisioner   │ │Provisioner   │ │boxProvisioner│
│              │ │              │ │              │
│Role: Service │ │Role: Service │ │Role: Service │
│Provider      │ │Provider      │ │Provider      │
│              │ │              │ │              │
│Knows: DDL,   │ │Knows: DDL,   │ │Knows: DDL,   │
│connection,   │ │connection,   │ │connection,   │
│migrations    │ │migrations    │ │migrations    │
└──────┬───────┘ └──────┬───────┘ └──────┬───────┘
       │                │                │
       ▼                ▼                ▼
┌───────────────────────────────────────────────────────────┐
│  IAmABoxMigrationRunner                                    │
│  Role: Service Provider                                    │
│  Responsibility: Executes migrations against a database,   │
│  tracks applied versions, ensures idempotency              │
└───────────────────────────────────────────────────────────┘
```

| Component | Role | Responsibility |
|-----------|------|----------------|
| `IAmABoxProvisioner` | Service Provider (interface) | Knows how to provision (create/migrate) a specific box type for a specific database backend |
| `MsSqlOutboxProvisioner`, etc. | Service Provider | Knows MSSQL-specific DDL and migrations for the outbox; decides whether to create or migrate |
| `BoxProvisioningHostedService` | Coordinator | Runs all registered provisioners at startup; decides ordering (outbox before inbox if both present) |
| `IAmABoxMigrationRunner` | Service Provider (interface) | Knows how to execute migration steps against a database, track which versions have been applied |
| `RelationalBoxMigrationRunner` | Service Provider | Implements migration tracking using a `__BrighterMigrationHistory` table; executes DDL steps in transactions |
| `BoxProvisioningOptions` | Information Holder | Holds the list of provisioners to run and their configuration |
| `IAmABoxMigration` | Information Holder (interface) | Describes a single forward migration step: version number, description, up DDL |

### 2. Core Abstractions (Package: `Paramore.Brighter.BoxProvisioning`)

This package defines the interfaces and the hosted service. It has no database-specific dependencies.

```csharp
/// <summary>
/// Captures the current state of a box table in the database, as detected by
/// the provisioner before handing off to the migration runner.
/// </summary>
/// <param name="TableExists">Whether the box table (inbox or outbox) exists in the database.</param>
/// <param name="HistoryExists">Whether the <c>__BrighterMigrationHistory</c> table has rows
/// for this specific box table and schema. Only meaningful when <paramref name="TableExists"/>
/// is <c>true</c>. An empty history table (created by a different box provisioner) is treated
/// the same as no history for this table.</param>
/// <param name="CurrentVersion">The highest migration version already reflected in the schema.
/// <list type="bullet">
///   <item><c>0</c> when the table does not exist (fresh install)</item>
///   <item>Introspected version (from column checks) when the table exists but has no history
///     (bootstrap of a pre-migration installation)</item>
///   <item>Max applied version from <c>__BrighterMigrationHistory</c> when history exists</item>
/// </list></param>
public record BoxTableState(bool TableExists, bool HistoryExists, int CurrentVersion);
```

```csharp
/// <summary>
/// Knows how to provision (create and migrate) a box (inbox or outbox) table.
/// </summary>
public interface IAmABoxProvisioner
{
    /// <summary>The type of box being provisioned.</summary>
    BoxType BoxType { get; }

    /// <summary>
    /// Provision the box table: create if it doesn't exist, then apply any
    /// outstanding migrations. Idempotent — safe to call on every startup.
    /// </summary>
    /// <exception cref="Exception">
    /// Throws if the database is unreachable, DDL execution fails, or migration
    /// tracking cannot be updated. The hosted service catches this, logs diagnostics,
    /// and re-throws as a <see cref="ConfigurationException"/> to fail-fast the host.
    /// </exception>
    Task ProvisionAsync(CancellationToken cancellationToken = default);
}

public enum BoxType { Inbox, Outbox }
```

The provisioner gathers all detection state into a `BoxTableState` record before calling the runner. This makes the three scenarios (fresh install, bootstrap, normal migration) explicit in the type system and eliminates ambiguity about the meaning of `currentVersion`.

The provisioner performs cascading checks to build the `BoxTableState`:

1. **`DoesTableExistAsync()`** — always called. Queries backend-specific catalog (e.g. `sys.tables` for MSSQL, `information_schema` for PostgreSQL/MySQL, `sqlite_master` for SQLite). If false, returns `BoxTableState(false, false, 0)`.
2. **`DoesHistoryExistAsync()`** — called only when the table exists. Checks whether `__BrighterMigrationHistory` has rows for this specific table name and schema. If false, falls through to version detection.
3. **`DetectCurrentVersionAsync()`** — called only in the bootstrap case (table exists, no history). Introspects actual column existence (e.g. via `INFORMATION_SCHEMA.COLUMNS` on MSSQL/PostgreSQL/MySQL, `pragma_table_info` on SQLite) to determine the highest migration version that matches the current schema. Returns 1 as a safe fallback for tables created by the original static builders; backend implementations must check for v2+ columns (e.g. `DataRef`, `SpecVersion`) and return a higher version when they are present.
4. **`GetMaxVersionAsync()`** — called only when history exists. Reads `MAX(MigrationVersion)` from `__BrighterMigrationHistory` for this table and schema.

```csharp
/// <summary>
/// Knows how to run migration steps against a database and track which
/// versions have been applied.
/// </summary>
public interface IAmABoxMigrationRunner
{
    /// <summary>
    /// Apply all outstanding migrations for the specified box table.
    /// </summary>
    /// <param name="tableName">The box table name.</param>
    /// <param name="schemaName">The database schema name (e.g. "dbo" for MSSQL). Used to
    /// distinguish identically-named tables in different schemas within the migration
    /// history. Defaults to the backend's default schema when null.</param>
    /// <param name="migrations">The ordered list of migrations to apply.</param>
    /// <param name="tableState">The current state of the box table, as detected by the
    /// provisioner. The runner uses this to determine its strategy:
    /// <list type="bullet">
    ///   <item><c>!TableExists</c> — run all migrations from version 1</item>
    ///   <item><c>TableExists &amp;&amp; !HistoryExists</c> — bootstrap: insert synthetic
    ///     history rows up to <c>CurrentVersion</c>, then apply remaining migrations</item>
    ///   <item><c>TableExists &amp;&amp; HistoryExists</c> — normal: skip migrations up to
    ///     and including <c>CurrentVersion</c>, apply remaining</item>
    /// </list></param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MigrateAsync(
        string tableName,
        string? schemaName,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default);
}
```

```csharp
/// <summary>
/// Describes a single schema migration step.
/// </summary>
public interface IAmABoxMigration
{
    /// <summary>Monotonically increasing version number.</summary>
    int Version { get; }

    /// <summary>Human-readable description of what this migration does.</summary>
    string Description { get; }

    /// <summary>Script to apply the migration (SQL for relational backends).</summary>
    string UpScript { get; }
}
```

The property is named `UpScript` rather than `UpSql` to avoid implying that only SQL databases are supported. While the current scope is relational backends only, the name leaves room for future extension to non-SQL backends (e.g. DynamoDB API calls serialized as scripts) without renaming a public interface member.

**Forward-only migrations (no `DownScript`)**: This is a deliberate design choice. Brighter's box migrations are exclusively additive — adding columns, widening types, adding indexes. DDL rollbacks (`DROP COLUMN`, `ALTER COLUMN` to a narrower type) risk data loss and are rarely safe in production. If a migration introduces a defect, the correct remediation is a new forward migration that fixes the schema, not a rollback. This keeps the migration interface simple and avoids giving operators a footgun. The `Up` prefix is retained for clarity and consistency with migration tooling conventions, not to imply a corresponding `Down`.

The `BoxProvisioningHostedService` is an `IHostedService` that resolves all `IAmABoxProvisioner` instances from DI and calls `ProvisionAsync` on each.

**Fail-fast behavior**: If any provisioner throws (database unreachable, migration DDL fails, etc.), the hosted service catches the exception, logs full diagnostic details at `Error` level, wraps it in Brighter's `ConfigurationException`, and re-throws. This is intentional — an application that cannot provision its inbox/outbox tables cannot function correctly and should not start. The `ConfigurationException` wrapper is consistent with how Brighter signals misconfiguration elsewhere (e.g. missing producers, invalid subscriptions) and gives operators a clear signal that the failure is infrastructure/configuration related, not a transient runtime error.

```csharp
public class BoxProvisioningHostedService : IHostedService
{
    private readonly IEnumerable<IAmABoxProvisioner> _provisioners;
    private readonly ILogger<BoxProvisioningHostedService> _logger;

    public BoxProvisioningHostedService(
        IEnumerable<IAmABoxProvisioner> provisioners,
        ILogger<BoxProvisioningHostedService> logger)
    {
        _provisioners = provisioners;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Outbox before inbox: the outbox is the critical path for message
        // production; provision it first so failures surface early.
        var ordered = _provisioners.OrderBy(p => p.BoxType == BoxType.Outbox ? 0 : 1);

        foreach (var provisioner in ordered)
        {
            _logger.LogInformation("Provisioning {BoxType}...", provisioner.BoxType);
            try
            {
                await provisioner.ProvisionAsync(cancellationToken);
                _logger.LogInformation("Provisioned {BoxType} successfully", provisioner.BoxType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to provision {BoxType}. The application cannot start " +
                    "without a valid box table. Check the database connection " +
                    "string and ensure the database is reachable.",
                    provisioner.BoxType);
                throw new ConfigurationException(
                    $"Box provisioning failed for {provisioner.BoxType}. " +
                    $"See inner exception for details.", ex);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### 3. Registration API — Extension on `IBrighterBuilder`

The entry point follows Brighter's existing pattern of extension methods on `IBrighterBuilder`:

```csharp
// In Paramore.Brighter.BoxProvisioning
public static class BrighterBuilderBoxProvisioningExtensions
{
    public static IBrighterBuilder UseBoxProvisioning(
        this IBrighterBuilder builder,
        Action<BoxProvisioningOptions> configure)
    {
        var options = new BoxProvisioningOptions();
        configure(options);

        foreach (var registration in options.Registrations)
        {
            registration(builder.Services);
        }

        // Guard against multiple calls — TryAddEnumerable ensures the hosted
        // service is registered at most once, even if UseBoxProvisioning is
        // called multiple times (e.g. from different configuration paths).
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, BoxProvisioningHostedService>());
        return builder;
    }
}
```

`BoxProvisioningOptions` collects provisioner registrations without directly depending on any backend. The registration list is exposed via a public `Add` method so that backend packages in separate assemblies can register provisioners, while the list itself remains internal to the core package:

```csharp
public class BoxProvisioningOptions
{
    private readonly List<Action<IServiceCollection>> _registrations = [];
    internal IReadOnlyList<Action<IServiceCollection>> Registrations => _registrations;

    /// <summary>
    /// Timeout for acquiring a database-level migration lock. Used by backends
    /// that require an explicit timeout (e.g. MySQL GET_LOCK). Default: 30 seconds.
    /// </summary>
    public TimeSpan MigrationLockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public void Add(Action<IServiceCollection> registration)
        => _registrations.Add(registration);
}
```

Each backend package adds extension methods to `BoxProvisioningOptions`. Two overloads are provided: one that accepts an explicit configuration, and one that accepts a connection name and resolves the configuration from `IServiceProvider` at runtime (for  deferred configuration scenarios):

```csharp
// In Paramore.Brighter.BoxProvisioning.MsSql
public static class MsSqlBoxProvisioningExtensions
{
    /// <summary>
    /// Register with an explicit configuration (non-Aspire).
    /// The provisioner is constructed immediately during service registration.
    /// </summary>
    public static BoxProvisioningOptions AddMsSqlOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        var lockTimeout = options.MigrationLockTimeout;
        options.Add(services =>
        {
            var runner = new MsSqlBoxMigrationRunner(configuration, lockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new MsSqlOutboxProvisioner(configuration, runner));
        });
        return options;
    }

    /// <summary>
    /// Register with a connection name resolved from IConfiguration at runtime.
    /// Used with .NET Aspire, where connection strings are not available until
    /// the service provider is built and Aspire's service discovery has populated
    /// IConfiguration.
    /// </summary>
    public static BoxProvisioningOptions AddMsSqlOutbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? outboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        var lockTimeout = options.MigrationLockTimeout;
        options.Add(services =>
        {
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString(connectionName)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{connectionName}' not found in configuration.");
                var dbConfig = new RelationalDatabaseConfiguration(
                    connectionString,
                    outBoxTableName: outboxTableName ?? "Outbox",
                    schemaName: schemaName,
                    binaryMessagePayload: binaryMessagePayload);
                var runner = new MsSqlBoxMigrationRunner(dbConfig, lockTimeout);
                return new MsSqlOutboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }

    public static BoxProvisioningOptions AddMsSqlInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        var lockTimeout = options.MigrationLockTimeout;
        options.Add(services =>
        {
            var runner = new MsSqlBoxMigrationRunner(configuration, lockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new MsSqlInboxProvisioner(configuration, runner));
        });
        return options;
    }

    /// <summary>
    /// Register inbox with a connection name resolved from IConfiguration at runtime.
    /// Symmetric with the outbox connectionName overload for Aspire scenarios.
    /// </summary>
    public static BoxProvisioningOptions AddMsSqlInbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? inboxTableName = null,
        string? schemaName = null)
    {
        var lockTimeout = options.MigrationLockTimeout;
        options.Add(services =>
        {
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString(connectionName)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{connectionName}' not found in configuration.");
                var dbConfig = new RelationalDatabaseConfiguration(
                    connectionString,
                    inboxTableName: inboxTableName ?? "Inbox",
                    schemaName: schemaName);
                var runner = new MsSqlBoxMigrationRunner(dbConfig, lockTimeout);
                return new MsSqlInboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }
}
```

The factory-based overload (`connectionName`) is critical for Aspire integration: Aspire populates `IConfiguration` with connection strings via service discovery *after* the DI container is configured but *before* hosted services run. By using an `IServiceProvider` factory, the provisioner's configuration is resolved at the right time.

Note that `MigrationLockTimeout` is captured from `BoxProvisioningOptions` at registration time and passed through to the provisioner constructor. This ensures the timeout configured centrally on the options flows through to each backend's migration runner, which uses it when acquiring database-level advisory locks (e.g. MySQL's `GET_LOCK`, MSSQL's `sp_getapplock`).

### 4. Backend Provisioner Implementation Pattern

Each backend provisioner encapsulates the connection, DDL, and migration knowledge for one box type. Example for MSSQL outbox:

```csharp
public class MsSqlOutboxProvisioner : IAmABoxProvisioner
{
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    public MsSqlOutboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
    {
        _configuration = configuration;
        _migrationRunner = migrationRunner;
    }

    public BoxType BoxType => BoxType.Outbox;

    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MsSqlOutboxMigrations.All(_configuration);

        // The provisioner owns all schema introspection knowledge and gathers
        // the current state into a BoxTableState record. The runner's job is
        // purely "apply the right migrations given this state".
        var tableState = await DetectTableStateAsync(cancellationToken);

        await _migrationRunner.MigrateAsync(
            _configuration.OutBoxTableName,
            _configuration.SchemaName,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Does the box table exist?
        var tableExists = await DoesTableExistAsync(connection, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        // 2. Does migration history exist for this table?
        var historyExists = await DoesHistoryExistAsync(connection, cancellationToken);
        if (!historyExists)
        {
            // 3. Bootstrap: introspect columns to detect current schema version
            var detectedVersion = await DetectCurrentVersionAsync(connection, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        // 4. Normal: read max applied version from history
        var maxVersion = await GetMaxVersionAsync(connection, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }
}
```

The `DetectTableStateAsync` method is private to each backend provisioner, not part of the `IAmABoxProvisioner` interface. The interface stays minimal (`BoxType` + `ProvisionAsync`), while backend implementations encapsulate all detection logic internally. The four helper methods (`DoesTableExistAsync`, `DoesHistoryExistAsync`, `DetectCurrentVersionAsync`, `GetMaxVersionAsync`) are also private/protected — they use backend-specific SQL and share a single open connection to avoid unnecessary reconnections.

### 5. Migration Runner and Version Tracking

The `RelationalBoxMigrationRunner` (one per database backend, since SQL dialect differs) tracks applied migrations in a `__BrighterMigrationHistory` table:

```sql
-- MSSQL variant (MSSQL does not support IF NOT EXISTS on CREATE TABLE)
IF NOT EXISTS (SELECT 1 FROM sys.tables
    WHERE name = '__BrighterMigrationHistory'
    AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [__BrighterMigrationHistory] (
        [MigrationVersion] INT NOT NULL,
        [SchemaName] VARCHAR(256) NOT NULL DEFAULT 'dbo',
        [BoxTableName] VARCHAR(256) NOT NULL,
        [Description] NVARCHAR(512) NOT NULL,
        [AppliedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_BrighterMigrationHistory
            PRIMARY KEY ([SchemaName], [BoxTableName], [MigrationVersion])
    );
END
```

The migration runner receives a `BoxTableState` from the provisioner and uses it to determine its strategy:

1. Acquires a database-level advisory lock to prevent concurrent migration attempts (see below)
2. Creates the history table if it doesn't exist
3. Determines which migrations to apply based on `BoxTableState`:
   - `!TableExists` — all migrations are pending (fresh install)
   - `TableExists && !HistoryExists` — bootstrap: insert synthetic history rows for versions up to and including `CurrentVersion`, then apply remaining migrations
   - `TableExists && HistoryExists` — skip migrations up to and including `CurrentVersion`, apply remaining
4. For each unapplied migration (in version order):
   a. Begins a transaction (where supported)
   b. Executes the `UpScript`
   c. Inserts a row into `__BrighterMigrationHistory`
   d. Commits the transaction
5. Releases the advisory lock
6. If any step fails, the transaction rolls back — the database remains at the last successfully applied version

#### Concurrency Control

When multiple instances of the same service start simultaneously (e.g. in a Kubernetes deployment or horizontal scaling), they could all attempt to run migrations concurrently, leading to duplicate DDL execution or race conditions on the history table.

Each backend's migration runner acquires a database-level lock before running migrations:

| Backend | Locking Mechanism |
|---------|------------------|
| MSSQL | `sp_getapplock @Resource='BrighterMigration_{tableName}', @LockMode='Exclusive'` within a transaction |
| PostgreSQL | `pg_try_advisory_lock(hashtext('BrighterMigration_' \|\| tableName)::bigint)` with a retry loop. The runner calls `pg_try_advisory_lock` in a loop with a short delay between attempts, logging "Waiting for migration lock on {tableName}..." on each retry so operators can diagnose slow startups. The total retry budget is bounded by `MigrationLockTimeout` (default 30s). If the lock is not acquired within the timeout, the runner throws `TimeoutException`. Note: `hashtext` returns a 32-bit integer, so hash collisions between different table names are theoretically possible but unlikely in practice. Two tables locked by the same hash would serialize rather than deadlock, causing spurious contention but no correctness issue. An alternative is the two-argument form `pg_advisory_lock(constant, hash)` with a Brighter-specific namespace constant, but the single-argument form is simpler and sufficient given the small number of box tables per database. |
| MySQL | `GET_LOCK('BrighterMigration_{tableName}', lockTimeout)` where `lockTimeout` defaults to 30 seconds, configurable via `BoxProvisioningOptions.MigrationLockTimeout` |
| SQLite | SQLite's file-level locking provides implicit serialization |
| Spanner | Spanner DDL operations are submitted via `ExecuteDdlAsync` (a separate DDL batch API) and are serialized by the Spanner service — they cannot run inside read-write transactions. The migration runner submits each DDL statement via `ExecuteDdlAsync`, then updates the history table in a normal read-write transaction. **Failure window**: Because DDL and history updates cannot share a transaction, there is a window where DDL has been applied but the history row has not been written (e.g. if the process crashes between the two steps). On next startup, the runner would attempt to re-apply the migration. To handle this safely, Spanner DDL migrations must be idempotent: `CREATE TABLE` uses `IF NOT EXISTS`; for `ALTER TABLE` (which does not support `IF NOT EXISTS` on columns), the Spanner runner must catch "column already exists" errors from `ExecuteDdlAsync` and treat them as success — the migration was applied but untracked, so the runner writes the missing history row and continues. |

**TimeSpan-to-backend unit conversions**: `BoxProvisioningOptions.MigrationLockTimeout` is a `TimeSpan`. Each backend converts it to the units expected by its locking API:

| Backend | API | Expected Unit | Conversion |
|---------|-----|--------------|------------|
| MSSQL | `sp_getapplock @LockTimeout` | Milliseconds (int) | `(int)timeout.TotalMilliseconds` |
| MySQL | `GET_LOCK(name, timeout)` | Whole seconds (int) | `(int)timeout.TotalSeconds` |
| PostgreSQL | `pg_try_advisory_lock` with retry | Milliseconds (int) | `(int)timeout.TotalMilliseconds` used as total retry budget |
| SQLite | File-level locking | N/A | Implicit serialization |
| Spanner | `ExecuteDdlAsync` | N/A | DDL serialized by Spanner service; history updates use normal transactions |

**Cancellation token propagation**: The `CancellationToken` passed through `StartAsync` → `ProvisionAsync` → `MigrateAsync` must be propagated to every `DbCommand.ExecuteNonQueryAsync(cancellationToken)` call. If the host signals shutdown during startup (before provisioning completes), in-flight DDL commands must respect the token to avoid blocking shutdown indefinitely.

The lock is held for the duration of the migration run (typically milliseconds for a single ALTER TABLE). Other instances that attempt to migrate concurrently will block until the lock is released, then check the history table and find no outstanding migrations to apply. This makes the migration runner safe for multi-instance deployments.

#### Connection Lifecycle

The migration runner opens a **single `DbConnection`** (e.g. `SqlConnection`, `NpgsqlConnection`, `MySqlConnection`) from `IAmARelationalDatabaseConfiguration.ConnectionString` at the start of `MigrateAsync` and disposes it when the method completes. The runner does **not** use `IAmARelationalDbConnectionProvider` — it manages its own connection because:

1. **The advisory lock must be held on the same connection** for the duration of all migrations. Session-level locks (PostgreSQL `pg_try_advisory_lock`, MySQL `GET_LOCK`) are released when the connection closes. Opening a new connection per migration would lose the lock.
2. **Provisioning runs at startup before DI is fully built**, so connection providers may not yet be available when the hosted service runs.

The connection is opened once, the advisory lock is acquired, all migrations run on that connection (each in its own transaction), and the lock is released before the connection is disposed. For MSSQL, which uses `sp_getapplock` within a transaction, the runner opens a single transaction that spans the entire migration run. MSSQL does not support true nested transactions — `BEGIN TRANSACTION` inside an outer transaction merely increments `@@TRANCOUNT`, and a rollback at any level rolls back everything. Therefore, on MSSQL the lock transaction and the migration transaction are the **same** transaction: the runner begins one transaction, acquires the app lock, executes all migration DDL and history inserts within it, and commits once at the end. If any migration step fails, the entire transaction (including the lock) rolls back, leaving the database at the last successfully committed state.

### 6. Migration Definitions — Reusing Existing Builders

The initial migration (version 1) for each backend reuses the existing static builder DDL. This avoids duplicating the carefully crafted DDL:

```csharp
public static class MsSqlOutboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(
        IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                version: 1,
                description: "Create outbox table",
                upScript: SqlOutboxBuilder.GetDDL(
                    config.OutBoxTableName,
                    config.BinaryMessagePayload))
        ];
    }
}
```

Future migrations are added to this list:

```csharp
// Example future migration when a new column is added:
new BoxMigration(
    version: 2,
    description: "Add SpecVersion column",
    upScript: $"ALTER TABLE [{config.OutBoxTableName}] ADD [SpecVersion] NVARCHAR(10) NULL")
```

**Binary vs text payload mode**: The `binaryMessagePayload` flag on `IAmARelationalDatabaseConfiguration` produces a structurally different table schema (e.g. `VARBINARY(MAX)` vs `NVARCHAR(MAX)` for the message body column). Changing this flag after the table has been created is **not supported** — the version-1 migration is already recorded in the history table, and the runner will not re-run it with different DDL. If a user needs to switch payload modes, they must drop and recreate the table (or manually alter the column), which is a destructive operation outside the scope of this library.

**Payload mode validation**: When the table already exists and has migration history, the provisioner performs a schema introspection step to check that the actual column type of the message body column matches the configured `binaryMessagePayload` mode. If there is a mismatch (e.g. the table has `NVARCHAR(MAX)` but the configuration specifies `binaryMessagePayload = true`), the provisioner throws a `ConfigurationException` at startup with a descriptive message. This fail-fast check prevents silent data corruption where binary data would be stored as text or vice versa. The introspection uses `INFORMATION_SCHEMA.COLUMNS` (MSSQL/PostgreSQL/MySQL) or `pragma_table_info` (SQLite) to determine the actual column data type.

The column to introspect differs by box type: `Body` for outbox tables, `CommandBody` for inbox tables. The expected column types per backend and payload mode are:

| Backend | Text mode (`binaryMessagePayload = false`) | Binary mode (`binaryMessagePayload = true`) |
|---------|---------------------------------------------|----------------------------------------------|
| MSSQL | `NVARCHAR(MAX)` | `VARBINARY(MAX)` |
| PostgreSQL | `TEXT` | `BYTEA` |
| MySQL | `LONGTEXT` / `TEXT` | `BLOB` / `LONGBLOB` |
| SQLite | `TEXT` | `BLOB` |

The version-1 migration for new installations runs the full `CREATE TABLE` DDL. For existing installations that pre-date the migration system, the runner detects the table exists (via the existing `GetExistsQuery` pattern), creates the history table, records version 1 as already applied (since the table was created by the old builder), and then applies any subsequent migrations.

### 7. Handling Pre-Migration Installations (Bootstrap)

The provisioner detects the bootstrap case before calling the runner: the box table exists but `__BrighterMigrationHistory` has no rows for it. The provisioner packages this as `BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: N)` where `N` is determined by introspecting actual column existence (see section 2).

When the runner receives a `BoxTableState` with `TableExists && !HistoryExists`, it performs a bootstrap. **The entire bootstrap path runs within the advisory lock** (acquired in step 1 of the migration runner's flow in section 5), so concurrent instances cannot race on the synthetic history insertion:

1. Insert synthetic history rows for all versions up to and including `CurrentVersion`
2. Apply any remaining migrations after `CurrentVersion`

**Backend-specific provisioners must implement `DetectCurrentVersionAsync`** to introspect actual column existence — for example, checking whether `SpecVersion` or `DataRef` columns exist via `INFORMATION_SCHEMA.COLUMNS` (MSSQL/PostgreSQL/MySQL) or `pragma_table_info` (SQLite) to determine if the schema matches version 2 or later. This is required because a fresh install created with newer DDL that already includes v2+ columns would otherwise be bootstrapped at version 1 and then fail when the runner attempts `ALTER TABLE ADD COLUMN` for columns that already exist (MySQL does not support `IF NOT EXISTS` for columns). The safe fallback is version 1, which assumes the table was created by the original static builder.

This ensures smooth upgrades for existing applications. The detection uses the same exists-query logic as the current builders, unified behind the provisioner.

### 8. .NET Aspire Integration

The core provisioning library is self-contained and does not depend on Aspire. Connection strings are passed in via `IAmARelationalDatabaseConfiguration`, so Aspire (or any other connection-string source) can be layered on top without changes to the provisioning architecture. Aspire integration will be addressed separately in a future ADR.

### 9. Package Structure

```
Paramore.Brighter.BoxProvisioning                         ← Core abstractions + hosted service
├── IAmABoxProvisioner.cs
├── IAmABoxMigrationRunner.cs
├── IAmABoxMigration.cs
├── BoxMigration.cs                                       ← Simple record implementation
├── BoxProvisioningOptions.cs
├── BoxProvisioningHostedService.cs
└── BrighterBuilderBoxProvisioningExtensions.cs

Paramore.Brighter.BoxProvisioning.MsSql                   ← MSSQL backend
├── MsSqlOutboxProvisioner.cs
├── MsSqlInboxProvisioner.cs
├── MsSqlBoxMigrationRunner.cs
├── MsSqlOutboxMigrations.cs
├── MsSqlInboxMigrations.cs
└── MsSqlBoxProvisioningExtensions.cs

Paramore.Brighter.BoxProvisioning.MySql                   ← MySQL backend
Paramore.Brighter.BoxProvisioning.PostgreSql              ← PostgreSQL backend
Paramore.Brighter.BoxProvisioning.Sqlite                  ← SQLite backend
Paramore.Brighter.BoxProvisioning.Spanner                 ← Spanner backend
```

Each backend package depends on:
- `Paramore.Brighter.BoxProvisioning` (core interfaces)
- The corresponding `Paramore.Brighter.Outbox.*` / `Paramore.Brighter.Inbox.*` packages (for the existing builder DDL)
- The database client library (e.g. `Microsoft.Data.SqlClient`, `Npgsql`)

### 10. Implementation Approach

**Prerequisites**:

1. `IAmARelationalDatabaseConfiguration` must be extended with a `string? SchemaName { get; }` property. The concrete `RelationalDatabaseConfiguration` already has this property, but the interface does not expose it. The provisioner code in this ADR references `_configuration.SchemaName` through the interface, so this is a required change before implementation begins. The test stub `StubSqlDbConfiguration` in `Paramore.Brighter.Extensions.Tests` must also be updated to satisfy the new interface member.

2. `SpannerOutboxBuilder` must be updated to add the missing `DataRef` and `SpecVersion` columns to both the text and binary DDL templates. All other outbox builders (MSSQL, PostgreSQL, MySQL, SQLite) already include these columns. There are no known Spanner users, so this is a safe cleanup with no migration concern.

Ordered to enable incremental delivery and testing:

1. **Core abstractions** — `Paramore.Brighter.BoxProvisioning` with interfaces, `BoxTableState`, `BoxProvisioningHostedService`, and registration extensions
2. **MSSQL backend** — `Paramore.Brighter.BoxProvisioning.MsSql` as the first implementation, with full tests against SQL Server in Docker
3. **PostgreSQL backend** — Second backend to validate the abstraction works across different SQL dialects
4. **MySQL, SQLite, Spanner backends** — Remaining relational backends
5. **Sample update** — Update `samples/WebAPI/` to use the new library instead of `DbMaker/SchemaCreation`

Aspire integration is out of scope for this ADR and will be addressed separately.

## Consequences

### Positive

- **Unified API**: Developers use the same `UseBoxProvisioning()` pattern regardless of backend — switching databases requires only changing the backend-specific `Add*Outbox()` call
- **Migration support**: Schema evolution is handled automatically at startup — no manual ALTER TABLE scripts when upgrading Brighter
- **Modular**: Each backend is an independent NuGet package; new backends can be added without touching core code
- **Backward compatible**: The existing static builder classes are unchanged; applications that use them directly continue to work. The new library calls the existing builders internally
- **Aspire-ready**: The architecture accepts connection strings via configuration, so Aspire integration can be layered on top without changes to the core provisioning library
- **Testable**: `IAmABoxProvisioner` and `IAmABoxMigrationRunner` are interfaces that can be mocked
- **Bootstrap path**: Existing installations that pre-date the migration system are detected and handled gracefully

### Negative

- **New NuGet packages**: Adds 5+ new packages to the Brighter ecosystem (core + backends)
- **Migration history table**: Introduces a new `__BrighterMigrationHistory` table in each database — a small footprint but visible to DBAs
- **Two ways to create tables**: Until the old builders are deprecated, developers have two options — direct builder usage and the new provisioning library. This could cause confusion
- **Transaction support varies**: SQLite has limited transaction support for DDL; Spanner requires special handling for DDL operations. Each backend must handle these differences

### Risks and Mitigations

- **Risk**: A migration fails mid-way, leaving the database in an inconsistent state
  - **Mitigation**: Each migration runs in a transaction (where supported). For backends without DDL transaction support (e.g. some MySQL DDL), migrations are designed to be individually idempotent (using `IF NOT EXISTS`, `IF NOT EXISTS COLUMN` patterns)

- **Risk**: Bootstrap detection incorrectly identifies a pre-migration installation's schema version
  - **Mitigation**: Version 1 migration matches the exact DDL of the current builders. Backend provisioners implement `DetectCurrentVersionAsync` to introspect the actual schema (e.g. via `INFORMATION_SCHEMA.COLUMNS`) and return the highest version whose columns are all present. The provisioner packages the result into a `BoxTableState` record so the runner has unambiguous context: `TableExists`, `HistoryExists`, and `CurrentVersion`. This ensures that tables created with newer DDL are correctly bootstrapped at the right version. Additionally, future migrations use `ALTER TABLE IF NOT EXISTS COLUMN` patterns as a safety net where supported

- **Risk**: Concurrent migration attempts from multiple service instances corrupt the schema or history table
  - **Mitigation**: Each backend's migration runner acquires a database-level advisory lock before running migrations. Concurrent instances block until the lock is released, then find no outstanding migrations to apply. See "Concurrency Control" in section 5.

## Alternatives Considered

### A. Use FluentMigrator for Box Migrations

The WebAPI sample already uses FluentMigrator for application database migrations. We could reuse it for inbox/outbox migrations too.

**Rejected because**:
- FluentMigrator is designed for application-level migrations with C# migration classes. Brighter's migrations are library-defined (shipped in NuGet packages), not user-defined
- Adding a FluentMigrator dependency to core Brighter packages is a heavyweight choice for what is essentially a handful of `ALTER TABLE` statements per version
- FluentMigrator's migration runner infrastructure requires per-backend runner registration (e.g. `AddFluentMigratorCore().AddSQLite()`) which conflicts with our goal of backend-agnostic core code

### B. Use EF Core Migrations

Some Brighter users already use EF Core. We could ship EF Core migrations for inbox/outbox tables.

**Rejected because**:
- Not all Brighter users use EF Core — many use Dapper or raw ADO.NET
- Would force an EF Core dependency on all users who want box provisioning
- EF Core migrations require a `DbContext` per box type, adding complexity
- Brighter's existing inbox/outbox implementations use raw ADO.NET, so EF Core migrations would be a mismatch

### C. Single Package with All Backends

Ship one package (`Paramore.Brighter.BoxProvisioning`) containing all backend implementations.

**Rejected because**:
- Forces all database client dependencies (SqlClient, Npgsql, MySqlConnector, etc.) onto every consumer
- Contradicts the existing Brighter pattern where each backend is a separate package
- Makes it impossible to add new backends without releasing a new version of the entire package

### D. Extend the Existing Static Builders with Migration Methods

Add migration methods directly to `SqlInboxBuilder`, `SqlOutboxBuilder`, etc.

**Rejected because**:
- The builders are currently stateless static classes — adding migration state (version tracking, connection management) would change their fundamental nature
- Mixes the "knowing DDL" responsibility with "executing DDL" and "tracking versions"
- Does not solve the hosting/DI integration or Aspire requirements

## References

- Requirements: [specs/0023-box_database_migration/requirements.md](../../specs/0023-box_database_migration/requirements.md)
- Related ADRs: None (first ADR for this feature)
- Existing patterns:
  - `IBrighterBuilder` and `AddProducers()` extension: `src/Paramore.Brighter.Extensions.DependencyInjection/`
  - `UseOutboxSweeper()` hosting extension: `src/Paramore.Brighter.Outbox.Hosting/`
  - Static builders: `src/Paramore.Brighter.{Inbox,Outbox}.{MsSql,MySql,Postgres,PostgreSql,Sqlite,Spanner}/`
  - Sample orchestration: `samples/WebAPI/WebAPI_Common/DbMaker/SchemaCreation.cs`
  - `RelationalDatabaseConfiguration`: `src/Paramore.Brighter/RelationalDatabaseConfiguration.cs`
- External:
  - [.NET Aspire custom component guide](https://learn.microsoft.com/en-us/dotnet/aspire/extensibility/custom-component)
  - [Aspire hosting extensions](https://learn.microsoft.com/en-us/dotnet/aspire/extensibility/custom-hosting-integration)

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

4. **No .NET Aspire integration**: Developers using Aspire must manually extract connection strings and construct inbox/outbox instances. There is no `AddBrighterOutbox()` Aspire hosting extension.

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
| `IAmABoxMigration` | Information Holder (interface) | Describes a single migration step: version number, description, up/down DDL |

### 2. Core Abstractions (Package: `Paramore.Brighter.BoxProvisioning`)

This package defines the interfaces and the hosted service. It has no database-specific dependencies.

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
    Task ProvisionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects the current schema version by introspecting the database.
    /// Used during bootstrap when the table exists but has no migration history.
    /// Returns the highest migration version that matches the current schema.
    /// Default implementation returns 1 (assumes table was created by the original builder).
    /// </summary>
    Task<int> DetectCurrentVersionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(1);
}

public enum BoxType { Inbox, Outbox }
```

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
    Task MigrateAsync(
        string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
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

The `BoxProvisioningHostedService` is an `IHostedService` that resolves all `IAmABoxProvisioner` instances from DI and calls `ProvisionAsync` on each:

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
        foreach (var provisioner in _provisioners)
        {
            _logger.LogInformation("Provisioning {BoxType}...", provisioner.BoxType);
            await provisioner.ProvisionAsync(cancellationToken);
            _logger.LogInformation("Provisioned {BoxType}", provisioner.BoxType);
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

        builder.Services.AddHostedService<BoxProvisioningHostedService>();
        return builder;
    }
}
```

`BoxProvisioningOptions` collects provisioner registrations without directly depending on any backend:

```csharp
public class BoxProvisioningOptions
{
    internal List<Action<IServiceCollection>> Registrations { get; } = [];
}
```

Each backend package adds extension methods to `BoxProvisioningOptions`. Two overloads are provided: one that accepts an explicit configuration (for non-Aspire use), and one that accepts a connection name and resolves the configuration from `IServiceProvider` at runtime (for Aspire and deferred configuration scenarios):

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
        options.Registrations.Add(services =>
        {
            services.AddSingleton<IAmABoxProvisioner>(
                new MsSqlOutboxProvisioner(configuration));
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
        options.Registrations.Add(services =>
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
                return new MsSqlOutboxProvisioner(dbConfig);
            });
        });
        return options;
    }

    public static BoxProvisioningOptions AddMsSqlInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Registrations.Add(services =>
        {
            services.AddSingleton<IAmABoxProvisioner>(
                new MsSqlInboxProvisioner(configuration));
        });
        return options;
    }
}
```

The factory-based overload (`connectionName`) is critical for Aspire integration: Aspire populates `IConfiguration` with connection strings via service discovery *after* the DI container is configured but *before* hosted services run. By using an `IServiceProvider` factory, the provisioner's configuration is resolved at the right time.

### 4. Backend Provisioner Implementation Pattern

Each backend provisioner encapsulates the connection, DDL, and migration knowledge for one box type. Example for MSSQL outbox:

```csharp
public class MsSqlOutboxProvisioner : IAmABoxProvisioner
{
    private readonly IAmARelationalDatabaseConfiguration _configuration;

    public MsSqlOutboxProvisioner(IAmARelationalDatabaseConfiguration configuration)
    {
        _configuration = configuration;
    }

    public BoxType BoxType => BoxType.Outbox;

    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var runner = new MsSqlBoxMigrationRunner(_configuration);
        var migrations = MsSqlOutboxMigrations.All(_configuration);
        await runner.MigrateAsync(
            _configuration.OutBoxTableName,
            migrations,
            cancellationToken);
    }
}
```

### 5. Migration Runner and Version Tracking

The `RelationalBoxMigrationRunner` (one per database backend, since SQL dialect differs) tracks applied migrations in a `__BrighterMigrationHistory` table:

```sql
-- MSSQL variant
CREATE TABLE IF NOT EXISTS [__BrighterMigrationHistory] (
    [MigrationVersion] INT NOT NULL,
    [BoxTableName] VARCHAR(256) NOT NULL,
    [Description] NVARCHAR(512) NOT NULL,
    [AppliedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_BrighterMigrationHistory
        PRIMARY KEY ([BoxTableName], [MigrationVersion])
);
```

The migration runner:
1. Acquires a database-level advisory lock to prevent concurrent migration attempts (see below)
2. Creates the history table if it doesn't exist
3. Queries which versions have already been applied for the target table name
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
| MSSQL | `sp_getapplock @Resource='BrighterMigration', @LockMode='Exclusive'` within a transaction |
| PostgreSQL | `pg_advisory_lock(hash)` where hash is derived from the table name |
| MySQL | `GET_LOCK('BrighterMigration_{tableName}', timeout)` |
| SQLite | SQLite's file-level locking provides implicit serialization |
| Spanner | Spanner transactions provide serializable isolation by default |

The lock is held for the duration of the migration run (typically milliseconds for a single ALTER TABLE). Other instances that attempt to migrate concurrently will block until the lock is released, then check the history table and find no outstanding migrations to apply. This makes the migration runner safe for multi-instance deployments.

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

The version-1 migration for new installations runs the full `CREATE TABLE` DDL. For existing installations that pre-date the migration system, the runner detects the table exists (via the existing `GetExistsQuery` pattern), creates the history table, records version 1 as already applied (since the table was created by the old builder), and then applies any subsequent migrations.

### 7. Handling Pre-Migration Installations (Bootstrap)

When the migration runner encounters a table that exists but has no entry in `__BrighterMigrationHistory`, it performs a bootstrap:

1. Detect the table exists (query `information_schema` or equivalent)
2. Call the provisioner's `DetectCurrentVersionAsync()` to determine the schema's actual state
3. Insert synthetic history rows for all versions up to and including the detected version
4. Apply any remaining migrations from the detected version onwards

The migration runner calls `DetectCurrentVersionAsync()` on the provisioner (see `IAmABoxProvisioner` in section 2) to determine the schema's actual state. The default implementation returns version 1, which is correct for the common case (table created by the existing static builder). Backend-specific provisioners can override this to introspect actual column existence — for example, checking whether `SpecVersion` or `DataRef` columns exist to determine if the schema matches version 2 or later. This handles edge cases where someone manually altered the schema or where the table was created by a newer builder version.

This ensures smooth upgrades for existing applications. The detection uses the same exists-query logic as the current builders, unified behind the provisioner.

### 8. .NET Aspire Integration

Two Aspire integration packages follow [Aspire's conventions for component authoring](https://learn.microsoft.com/en-us/dotnet/aspire/extensibility/custom-component):

**Package: `Paramore.Brighter.BoxProvisioning.Aspire.Hosting`** — AppHost-side

This package provides extensions on Aspire's `IDistributedApplicationBuilder` to declare that a project uses a Brighter outbox/inbox backed by a database resource:

```csharp
// In the AppHost Program.cs
var sqlServer = builder.AddSqlServer("sql").AddDatabase("brighter");

builder.AddProject<Projects.MyService>("my-service")
    .WithReference(sqlServer)
    .WithBrighterOutbox(sqlServer, tableName: "Outbox");
```

The `WithBrighterOutbox` extension writes connection metadata into the resource's environment/configuration so the service project can resolve it.

**Package: `Paramore.Brighter.BoxProvisioning.Aspire.MsSql`** (and per-backend equivalents) — Service-side

These provide overloads of `AddMsSqlOutbox()` that resolve connection strings from Aspire's `IConfiguration` (populated by Aspire's service discovery):

```csharp
// In the service's Program.cs
services.AddBrighter()
    .AddProducers(...)
    .UseBoxProvisioning(options =>
    {
        // Connection string resolved from Aspire's configuration
        options.AddMsSqlOutbox(connectionName: "brighter");
    });
```

The `connectionName`-based overload resolves the connection string via `IConfiguration.GetConnectionString(connectionName)`, which Aspire populates through its service discovery mechanism. This cleanly separates infrastructure wiring (AppHost) from application code (service).

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

Paramore.Brighter.BoxProvisioning.Aspire.Hosting          ← AppHost extensions
Paramore.Brighter.BoxProvisioning.Aspire.MsSql            ← Aspire service-side MSSQL
Paramore.Brighter.BoxProvisioning.Aspire.MySql            ← etc.
Paramore.Brighter.BoxProvisioning.Aspire.PostgreSql
```

Each backend package depends on:
- `Paramore.Brighter.BoxProvisioning` (core interfaces)
- The corresponding `Paramore.Brighter.Outbox.*` / `Paramore.Brighter.Inbox.*` packages (for the existing builder DDL)
- The database client library (e.g. `Microsoft.Data.SqlClient`, `Npgsql`)

### 10. Implementation Approach

Ordered to enable incremental delivery and testing:

1. **Core abstractions** — `Paramore.Brighter.BoxProvisioning` with interfaces, `BoxProvisioningHostedService`, and registration extensions
2. **MSSQL backend** — `Paramore.Brighter.BoxProvisioning.MsSql` as the first implementation, with full tests against SQL Server in Docker
3. **PostgreSQL backend** — Second backend to validate the abstraction works across different SQL dialects
4. **MySQL, SQLite, Spanner backends** — Remaining relational backends
5. **Aspire hosting** — AppHost-side extensions
6. **Aspire service-side** — Per-backend Aspire connection resolution
7. **Sample update** — Update `samples/WebAPI/` to use the new library instead of `DbMaker/SchemaCreation`

## Consequences

### Positive

- **Unified API**: Developers use the same `UseBoxProvisioning()` pattern regardless of backend — switching databases requires only changing the backend-specific `Add*Outbox()` call
- **Migration support**: Schema evolution is handled automatically at startup — no manual ALTER TABLE scripts when upgrading Brighter
- **Modular**: Each backend is an independent NuGet package; new backends can be added without touching core code
- **Backward compatible**: The existing static builder classes are unchanged; applications that use them directly continue to work. The new library calls the existing builders internally
- **Aspire-ready**: First-class Aspire integration follows Microsoft's conventions
- **Testable**: `IAmABoxProvisioner` and `IAmABoxMigrationRunner` are interfaces that can be mocked
- **Bootstrap path**: Existing installations that pre-date the migration system are detected and handled gracefully

### Negative

- **New NuGet packages**: Adds 5+ new packages to the Brighter ecosystem (core + backends + Aspire)
- **Migration history table**: Introduces a new `__BrighterMigrationHistory` table in each database — a small footprint but visible to DBAs
- **Two ways to create tables**: Until the old builders are deprecated, developers have two options — direct builder usage and the new provisioning library. This could cause confusion
- **Transaction support varies**: SQLite has limited transaction support for DDL; Spanner requires special handling for DDL operations. Each backend must handle these differences

### Risks and Mitigations

- **Risk**: A migration fails mid-way, leaving the database in an inconsistent state
  - **Mitigation**: Each migration runs in a transaction (where supported). For backends without DDL transaction support (e.g. some MySQL DDL), migrations are designed to be individually idempotent (using `IF NOT EXISTS`, `IF NOT EXISTS COLUMN` patterns)

- **Risk**: Bootstrap detection incorrectly identifies a pre-migration installation's schema version
  - **Mitigation**: Version 1 migration matches the exact DDL of the current builders. If the table exists and has no migration history, we assume it was created by the builder and mark version 1 as applied. Future migrations use `ALTER TABLE IF NOT EXISTS COLUMN` patterns as a safety net

- **Risk**: Concurrent migration attempts from multiple service instances corrupt the schema or history table
  - **Mitigation**: Each backend's migration runner acquires a database-level advisory lock before running migrations. Concurrent instances block until the lock is released, then find no outstanding migrations to apply. See "Concurrency Control" in section 5.

- **Risk**: Aspire conventions change in future versions
  - **Mitigation**: Follow the official Aspire extensibility guide closely. Keep Aspire packages as thin wrappers over the core provisioning library, minimizing the surface area that depends on Aspire APIs

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

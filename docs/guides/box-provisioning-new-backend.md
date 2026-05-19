# Adding a New BoxProvisioning Backend (with Migration Support)

This guide walks a contributor through implementing a brand-new BoxProvisioning backend — for example, adding support for Oracle, DuckDB, ClickHouse, or any other relational store. It covers the role interfaces you must implement, the abstract bases you derive from, the DI surface to expose, and the drift-test wiring you must mirror.

> **Quick reference for AI agents**: [`.agent_instructions/box_provisioning.md`](../../.agent_instructions/box_provisioning.md). This guide is the long-form companion focused on a new backend rather than a new column.

## Before you start — choose your migration story

Two shapes are supported:

1. **Full V_k migration chain** (recommended for any relational backend with `ALTER TABLE ADD COLUMN`). MSSQL, PostgreSQL, MySQL, SQLite all use this shape. You implement the full set of role interfaces, derive from `SqlBoxProvisioner<TConn, TTx>` and `SqlBoxMigrationRunner<TConn, TTx>`, and ship a migration catalog with V1..V_latest.

2. **Degenerate fresh-install only** (the Spanner shape, per [ADR 0057 §6](../adr/0057-box-schema-versioning-and-migrations.md)). Used when the backend's DDL grammar or transaction model makes a forward-migration chain impractical. You implement `IAmABoxProvisioner` and `IAmABoxMigrationRunner` directly (no abstract base), ship no migration catalog, and rely on builder updates for schema evolution. This is a **deliberate degeneracy** — discuss with maintainers before adopting it, because it sacrifices in-place upgrades.

This guide assumes shape (1). The Spanner exemption is documented separately in ADR 0057 §6 if you need it.

## Package layout

Create two new projects:

```
src/
  Paramore.Brighter.Outbox.{Backend}/           # Outbox runtime + builder DDL
  Paramore.Brighter.Inbox.{Backend}/            # Inbox runtime + builder DDL
  Paramore.Brighter.BoxProvisioning.{Backend}/  # Provisioning glue (this guide)

tests/
  Paramore.Brighter.{Backend}.Tests/            # Integration tests including drift tests
```

The provisioning project depends on the runtime projects (for the builder classes) and on the core `Paramore.Brighter.BoxProvisioning` assembly (for the role interfaces and abstract bases).

## The role interfaces — what you implement

The five role interfaces in `src/Paramore.Brighter.BoxProvisioning/`:

| Interface | Your class |
|-----------|------------|
| `IAmABoxMigrationCatalog` | `{Backend}OutboxMigrationCatalog`, `{Backend}InboxMigrationCatalog` |
| `IAmABoxMigrationDetectionHelper<TConn, TTx>` *and* `IAmAVersionDetectingMigrationHelper<TConn, TTx>` | `{Backend}BoxDetectionHelper` (one helper covers both Outbox and Inbox) |
| `IAmABoxPayloadModeValidator<TConn>` | `{Backend}PayloadModeValidator` |
| `IAmAProvisioningUnitOfWork` | `{Backend}ProvisioningUnitOfWork` |
| `IAmABoxProvisioner` (via `SqlBoxProvisioner<TConn, TTx>` base) | `{Backend}OutboxProvisioner`, `{Backend}InboxProvisioner` |
| `IAmABoxMigrationRunner` (via `SqlBoxMigrationRunner<TConn, TTx>` base) | `{Backend}BoxMigrationRunner` |

You also write a DI extension class: `{Backend}BoxProvisioningExtensions` exposing `Add{Backend}Outbox(...)` and `Add{Backend}Inbox(...)`.

## Step-by-step implementation

### 1. Builders (the easy part — already exist if you have a runtime)

If your backend already has an `Outbox.{Backend}` / `Inbox.{Backend}` runtime, the builder is already in place. The builder's `GetDDL(...)` method returns the CREATE TABLE DDL for the latest schema, and is the source of truth for V1's `UpScript`.

If you are also building a brand-new runtime, follow the existing examples (e.g. `SqlOutboxBuilder.cs` for MSSQL) and put the builder in the runtime project, **not** the provisioning project.

### 2. Migration catalogs

Implement `IAmABoxMigrationCatalog.All(IAmARelationalDatabaseConfiguration)`. The shape is canonical across backends — see [`MsSqlOutboxMigrationCatalog.cs`](../../src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrationCatalog.cs) as the reference.

Skeleton:

```csharp
public class {Backend}OutboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private const string DefaultSchema = "<your-backend-default>"; // e.g. "dbo", "public"

    private static readonly string[] s_v1Columns =
        ["MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"];
    private static readonly string[] s_v2AddedColumns = ["Dispatched"];
    // ... V3..V_latest column arrays ...

    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var schema = configuration.SchemaName ?? DefaultSchema;
        var table = configuration.OutBoxTableName;

        // Defence-in-depth — same check the SqlBoxProvisioner runs.
        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.OutBoxTableName));
        Identifiers.AssertSafe(schema, nameof(IAmARelationalDatabaseConfiguration.SchemaName));

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: {Backend}OutboxBuilder.GetDDL(table, configuration.BinaryMessagePayload),
                LogicalColumns: Cumulative(1)),

            new BoxMigration(
                Version: 2,
                Description: "Add Dispatched column",
                UpScript: AddColumns(schema, table, ("Dispatched", "<DATETIME-TYPE>")),
                LogicalColumns: Cumulative(2),
                SourceReference: "<commit-sha-from-history>"),

            // ... V3..V_latest, matching the cross-backend versioning ...
        ];
    }

    private static IReadOnlyCollection<string> Cumulative(int upToVersion) { /* HashSet union */ }
    private static string AddColumns(string schema, string table, params (string Column, string Type)[] columns) { /* backend-specific idempotent ALTER */ }
}
```

**Cross-backend version uniformity**: the outbox V1..V7 chain is shared across MSSQL/PG/MySQL/SQLite, with the same column set per V_k. Your new backend should match this if it is a general-purpose relational store. Inbox is V1..V2 on MSSQL/MySQL/SQLite and V1-only on PostgreSQL — discuss with maintainers if your backend differs.

**Idempotent UpScripts** are MANDATORY. Pick the strongest existence-check your DDL grammar supports:

- If your backend has `ADD COLUMN IF NOT EXISTS` (PostgreSQL-style) → use it inline.
- If your backend has `INFORMATION_SCHEMA.COLUMNS` lookups → emit a conditional ALTER (MSSQL `IF COL_LENGTH(...) IS NULL` shape).
- If your backend supports neither → set `IdempotencyCheckSql` to a probe query and emit a plain ALTER (SQLite shape).

### 3. Detection helper

Implement `IAmAVersionDetectingMigrationHelper<TConn, TTx>` (it extends `IAmABoxMigrationDetectionHelper<TConn, TTx>`). One helper class covers both Outbox and Inbox via the `BoxType` parameter on `DiscriminatorFor` and `DetectCurrentVersionAsync`.

You implement five methods:

| Method | What it does | Cost |
|--------|--------------|------|
| `DoesTableExistAsync` | `INFORMATION_SCHEMA.TABLES` (or backend equivalent) lookup | One round-trip |
| `DoesHistoryExistAsync` | Same as above for `__BrighterMigrationHistory` | One round-trip |
| `GetMaxVersionAsync` | `SELECT MAX(MigrationVersion) FROM __BrighterMigrationHistory WHERE ...` | One round-trip |
| `GetTableColumnsAsync` | `INFORMATION_SCHEMA.COLUMNS` lookup → set of column names | One round-trip |
| `DetectCurrentVersionAsync` | Walks the migration list `V_latest..V1`, returns the highest version whose `LogicalColumns` is a subset of the live columns. Default implementation pattern shown in the existing backends. | Re-uses `GetTableColumnsAsync` |
| `DiscriminatorFor(BoxType)` | Pure function — returns the value the runner writes to `__BrighterMigrationHistory.BoxType` (typically `"Outbox"` / `"Inbox"`). | No I/O |

**Parameter ordering note**: the detection methods place `cancellationToken` **before** the optional `transaction` parameter, departing from the .NET convention of trailing CT. This is deliberate — the transaction is the rarely-supplied optional (only the runner's under-lock re-detection path passes one), and trailing it lets transactionless call sites use positional CT arguments. Match this ordering for consistency.

**Reference implementation**: [`MsSqlBoxDetectionHelper.cs`](../../src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxDetectionHelper.cs).

### 4. Payload-mode validator

Implement `IAmABoxPayloadModeValidator<TConn>`. The validator inspects the live column type (e.g. `BYTEA` vs `TEXT`, `VARBINARY(MAX)` vs `NVARCHAR(MAX)`) against the configured `BinaryMessagePayload` flag and throws `ConfigurationException` on mismatch. This catches the "tried to switch from text to binary mode on an existing table without migration" class of bug.

**Reference implementation**: [`MsSqlPayloadModeValidator.cs`](../../src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlPayloadModeValidator.cs).

### 5. Provisioning unit of work

Implement `IAmAProvisioningUnitOfWork`. The UoW pairs a transaction with a backend-appropriate advisory lock; the runner's template method invokes `BeginAsync` → applies migrations → `CommitAsync` / `RollbackAsync` → `DisposeAsync`. Decisions you must make:

- **Lock granularity** — session-scoped (PostgreSQL, MySQL) or transaction-scoped (MSSQL). Session-scoped requires an explicit `Release` in the catch / finally arm; transaction-scoped is freed by the transaction lifecycle itself.
- **Transaction model** — does your backend allow DDL in a transaction (MSSQL/PG/SQLite — yes) or does DDL auto-commit (MySQL — yes; the MySQL UoW is therefore lock-only with no transaction). The runner template handles both shapes via the UoW abstraction.
- **Rollback catch breadth** — the relational UoWs catch `Exception` (not `InvalidOperationException`) on rollback to ensure the lock is always released. The PostgreSQL UoW additionally has a narrow catch for the rollback itself plus a wide catch for the release-cleanup arm. Mirror the existing pattern.

**Reference implementations**:
- [`MsSqlProvisioningUnitOfWork.cs`](../../src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlProvisioningUnitOfWork.cs) — transaction-scoped lock.
- [`PostgreSqlProvisioningUnitOfWork.cs`](../../src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlProvisioningUnitOfWork.cs) — session-scoped lock with two-arm catch.
- [`MySqlProvisioningUnitOfWork.cs`](../../src/Paramore.Brighter.BoxProvisioning.MySql/MySqlProvisioningUnitOfWork.cs) — lock-only (no transaction, DDL auto-commits).

### 6. Migration runner — derive from `SqlBoxMigrationRunner<TConn, TTx>`

The abstract base owns the template-method orchestration:

- Acquire the advisory lock via the UoW.
- Re-detect table state under the lock (the pre-lock detection from the provisioner was a hint; the runner is the source of truth).
- Walk the three paths: **fresh** (no table) → run V1 → V_latest; **bootstrap** (table exists, no history) → infer V_k → walk forward; **normal** (table + history) → walk forward from `current_max + 1`.
- Insert one `__BrighterMigrationHistory` row per applied migration.
- Commit / rollback / dispose via the UoW.

Derived classes supply only the **abstract hooks** for connection factory, lock acquisition/release, and SQL for the history table interactions. See [`SqlBoxMigrationRunner.cs`](../../src/Paramore.Brighter.BoxProvisioning/SqlBoxMigrationRunner.cs) for the exact hook signatures.

**Reference implementation**: [`MsSqlBoxMigrationRunner.cs`](../../src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs).

### 7. Provisioners — derive from `SqlBoxProvisioner<TConn, TTx>`

The provisioner is the entry point invoked by `BoxProvisioningHostedService`. The abstract base owns:

- The `Identifiers.AssertSafe` chokepoint over `OutBoxTableName` / `InBoxTableName` / `SchemaName` (called BEFORE any connection is opened — defence-in-depth).
- Pre-lock detection (single connection lifecycle covers detection AND payload-mode validation).
- The call to `_migrationRunner.MigrateAsync(...)`.

Derived classes supply only **two hooks**: `CreateConnection(connectionString)` and `PayloadColumnName` (e.g. `"Body"` for the outbox, `"CommandBody"` for the inbox; lower-case variants for PostgreSQL's case-folding convention).

Skeleton:

```csharp
public class {Backend}OutboxProvisioner : SqlBoxProvisioner<{Backend}Connection, {Backend}Transaction>
{
    public {Backend}OutboxProvisioner(
        IAmAVersionDetectingMigrationHelper<{Backend}Connection, {Backend}Transaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<{Backend}Connection> payloadValidator,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : base(detectionHelper, catalog, payloadValidator, configuration, migrationRunner, BoxType.Outbox)
    {
    }

    protected override {Backend}Connection CreateConnection(string connectionString)
        => new {Backend}Connection(connectionString);

    protected override string PayloadColumnName => "Body";
}
```

The Inbox provisioner is identical with `BoxType.Inbox` and `PayloadColumnName => "CommandBody"`.

### 8. DI extensions

Expose two pairs of `Add{Backend}Outbox` and `Add{Backend}Inbox` overloads on `BoxProvisioningOptions`:

- One taking explicit `IAmARelationalDatabaseConfiguration`.
- One taking a connection string name resolved from `IConfiguration` at runtime.

Each registration:

- Uses `TryAddSingleton` for the shared role implementations (detection helper, payload validator, UoW class) so a second Outbox/Inbox registration on the same backend re-uses them.
- Uses `AddSingleton` for the provisioner itself (one per box-type).
- Defers connection-string lookup until the registration delegate runs inside `UseBoxProvisioning` (so the `IConfiguration` is available).

**Reference implementation**: [`MsSqlBoxProvisioningExtensions.cs`](../../src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxProvisioningExtensions.cs).

Resulting call-site shape:

```csharp
services
    .AddBrighter()
    .UseBoxProvisioning(opts =>
    {
        opts.Add{Backend}Outbox(configuration);
        opts.Add{Backend}Inbox(configuration);
    });
```

## Step 9 — Tests

### Drift tests (mandatory)

Create `tests/Paramore.Brighter.{Backend}.Tests/BoxProvisioning/Drift/`:

- `{Backend}OutboxHousekeeping.cs` — backend-specific columns not in `LogicalColumns` (typically the identity / surrogate PK column, e.g. MSSQL `Id`).
- `{Backend}InboxHousekeeping.cs` — same for the inbox.
- One assertion test per box that proves:

```csharp
var ddl = {Backend}OutboxBuilder.GetDDL(tableName, binaryPayload: false);
var expected = DdlColumnExtractor.GetExpectedColumns(ddl, QuoteStyle.{Backend});
var migrations = new {Backend}OutboxMigrationCatalog().All(configuration);
var covered = migrations.Last().LogicalColumns.Union({Backend}OutboxHousekeeping.V1);
Assert.Equal(expected, covered, comparerFor({Backend}));
```

If your backend's identifier-quote style is unusual, you may need to extend `QuoteStyle` and `DdlColumnExtractor` under `tests/Paramore.Brighter.BoxProvisioning.Tests/Drift/`.

### Behaviour tests

Mirror the per-backend test suites under `tests/Paramore.Brighter.{Existing Backend}.Tests/BoxProvisioning/` — they cover fresh-install, bootstrap, idempotency, payload-mode mismatch, concurrent-provisioner, and (where applicable) per-version migration application. Aim for parity with the most-similar existing backend; gaps are acceptable but should be filed as follow-up issues.

### Integration test infrastructure

Add a `docker-compose-{backend}.yaml` to the repo root if your backend has a local image. Document its startup pattern (and any state-wipe quirks) in your test project's README. The existing PG / MySQL / MSSQL / Spanner compose files are reference examples.

## Operational notes (backend-specific surprises)

A handful of backend-specific behaviours are intentional but not obvious from the code — operators tuning a Brighter deployment should know about them upfront.

### MySQL — runner enables `AllowUserVariables` on its own connection

The MySQL migration runner mutates its own connection string to set `AllowUserVariables=true` if the caller-supplied connection string disabled it. V2..V7 outbox/inbox migrations (per ADR 0057 §5a) use the MySQL `information_schema.columns` probe + prepared-statement idempotency pattern, which depends on session-scoped user variables (`SET @q = ...; PREPARE stmt FROM @q;`) — without `AllowUserVariables=true` the prepared form fails because MySqlConnector treats `@variable` tokens as parameter markers by default. The flip is **scoped to the runner's own `MySqlConnection`** and does not affect the caller-supplied connection string instance. An `Information`-level log records the mutation so a security-conscious operator who deliberately disabled user variables (the documented prepared-statement-injection surface) can correlate it against migration activity without enabling Debug-level logging.

### Migration history is global, not per-tenant

`__BrighterMigrationHistory` (or `BrighterMigrationHistory` on Spanner) is created in the backend's default schema (`dbo` on MSSQL, `public` on PG, the connection-bound `DATABASE()` on MySQL). The PK `(SchemaName, BoxTableName, MigrationVersion)` keeps multi-tenant rows unambiguous, so deployments using a separate schema per tenant remain correct — but the **history rows for all tenants share a single physical table**. Per-tenant isolation of the history table is not supported; if your operating model needs it, file an issue. Tracked at <https://github.com/BrighterCommand/Brighter/issues/4144>.

### MySQL/SQLite advisory-lock timeout is floored at 1 second

`BoxProvisioningOptions.MigrationLockTimeout` is honoured at millisecond resolution on MSSQL (`sp_getapplock`) and PG (`pg_try_advisory_lock` retry loop with a monotonic deadline), but MySQL's `GET_LOCK` takes an integer-second argument and SQLite's runner falls back to the same whole-second granularity. Sub-second timeouts on MySQL/SQLite are rounded **up** to 1 second — `TimeSpan.Zero` is **not** fail-fast on these backends. Cross-backend callers expecting fail-fast semantics should size their probe/deploy budgets accordingly.

## Identifier safety

Brighter's `Identifiers.AssertSafe` chokepoint validates table and schema names against `^[A-Za-z][A-Za-z0-9_]*$`. This is the **strictest backend's** rule (Spanner rejects `_`-prefixed names as reserved) applied framework-wide. If your new backend allows characters outside this set inside quoted/backticked identifiers, the chokepoint will still reject them — this is deliberate defence-in-depth. Do not loosen the regex; if your backend has a legitimate use case for an unusual identifier shape, rename the configuration value or document the constraint.

The migration-history table name is hard-coded as `__BrighterMigrationHistory` on relational backends. If your backend rejects this name (e.g. Spanner GoogleSQL rejects leading underscores → uses `BrighterMigrationHistory`), document the override and apply it consistently in your detection helper, runner, and any tests.

## Checklist

- [ ] `Paramore.Brighter.BoxProvisioning.{Backend}` project created and added to the solution.
- [ ] `{Backend}OutboxMigrationCatalog.cs` and `{Backend}InboxMigrationCatalog.cs` implement `IAmABoxMigrationCatalog` with idempotent UpScripts and `Identifiers.AssertSafe` at the entry of `All(...)`.
- [ ] `{Backend}BoxDetectionHelper.cs` implements `IAmAVersionDetectingMigrationHelper<TConn, TTx>` with the parameter-ordering convention (CT before transaction).
- [ ] `{Backend}PayloadModeValidator.cs` implements `IAmABoxPayloadModeValidator<TConn>`.
- [ ] `{Backend}ProvisioningUnitOfWork.cs` implements `IAmAProvisioningUnitOfWork` with the lock-granularity and rollback-catch-breadth decisions documented inline.
- [ ] `{Backend}BoxMigrationRunner.cs` derives from `SqlBoxMigrationRunner<TConn, TTx>` and supplies the abstract hooks.
- [ ] `{Backend}OutboxProvisioner.cs` and `{Backend}InboxProvisioner.cs` derive from `SqlBoxProvisioner<TConn, TTx>` and supply `CreateConnection` + `PayloadColumnName`.
- [ ] `{Backend}BoxProvisioningExtensions.cs` exposes `Add{Backend}Outbox` / `Add{Backend}Inbox` overloads and uses `TryAddSingleton` for shared role implementations.
- [ ] Drift test passes for both Outbox and Inbox; housekeeping file documents backend-specific columns.
- [ ] Behaviour-test parity with the closest existing backend (or gaps filed as follow-up issues).
- [ ] Docker-compose file added (if applicable).
- [ ] Outbox V1..V_latest version numbers match the cross-backend uniform chain (or divergence is discussed with maintainers).
- [ ] ADR addendum or new ADR if your backend introduces a novel constraint (e.g. fresh-only model like Spanner) — discuss with maintainers.

## Architecture reference

- [ADR 0057 — Box schema versioning and migrations](../adr/0057-box-schema-versioning-and-migrations.md) — versioning model, three-path runner, advisory locks, drift-test design, Spanner exemption.
- [ADR 0058 — Box provisioning RDD role interfaces](../adr/0058-box-provisioning-rdd-role-interfaces.md) — the role-based interfaces this guide enumerates, and the template-method runner/provisioner bases.
- [ADR 0059 — Box provisioning abstract base naming symmetry](../adr/0059-box-provisioning-abstract-base-naming-symmetry.md) — naming convention for `SqlBoxMigrationRunner` / `SqlBoxProvisioner`.
- [Spec 0027 README](../../specs/0027-box-schema-versioning-and-migrations/README.md) — archaeology of the V1..V7 outbox / V1..V2 inbox chain.
- [Spec 0028 README](../../specs/0028-box-provisioning-rdd-role-interfaces/README.md) — RDD refresh + abstract-base pull-up.
- Existing reference backend: `src/Paramore.Brighter.BoxProvisioning.MsSql/` — the canonical shape.
- Companion guide: [box-provisioning-adding-columns.md](box-provisioning-adding-columns.md) — once your backend is in place, columns are added via the cross-backend column workflow.

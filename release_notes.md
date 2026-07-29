# Release Notes

## Master

### Azure Service Bus: dead-letter reason and description (#4196)

When a handler rejects a message consumed from Azure Service Bus, `AzureServiceBusConsumer` now records the rejection reason and description in the broker's native `DeadLetterReason` and `DeadLetterErrorDescription` fields rather than dead-lettering with blank values — so the reason is visible to operators triaging the dead-letter queue instead of living only in logs. Values are truncated to the 4096-character limit Azure Service Bus enforces. A `DeadLetterAsync(lockToken, reason, description)` overload is added to the public `IServiceBusReceiverWrapper`.

### Box Schema Versioning and Migrations (spec 0027)

Brighter's box-provisioning system now ships a versioned migration chain for the Outbox and Inbox tables. New deployments install at `V_latest` directly; deployments installed under spec 0023 (which only had a `V=1` history row) are recognised by the runner and advance to `V_latest` without re-running DDL — the existing `V=1` row is preserved verbatim and the V2..V_latest rows are appended. Deployments with pre-spec-0023 (legacy) tables are bootstrapped via column introspection, gated by a `HeaderBag`/`CommandBody` discriminator, then upgraded to `V_latest` under the existing per-backend migration lock.

#### Source-breaking change: `IAmABoxMigration`

The `IAmABoxMigration` interface (and the `BoxMigration` record) gain three new required members:

* **`IReadOnlyCollection<string> LogicalColumns`** — the cumulative column set the table has after this migration applies. Used by drift detection (the build fails if a column lands on the builder DDL without a matching migration entry) and by version inference for legacy tables (the runner walks the `LogicalColumns` chain to determine which migration to bootstrap from).
* **`string? SourceReference`** — the commit SHA (and PR number where available) that introduced the column. Required from V2 onwards; V1 stays `null`.
* **`string? IdempotencyCheckSql`** — used **only by SQLite**, whose grammar lacks `ALTER TABLE ADD COLUMN IF NOT EXISTS`. The SQLite runner evaluates this scalar as an existence probe and skips `UpScript` when the probe returns `> 0` (still inserting the history row). MSSQL / PostgreSQL / MySQL bake the existence check into the `UpScript` itself and leave `IdempotencyCheckSql` `null`.

External implementors of `IAmABoxMigration` will fail to compile until they add the new members. The change is source-breaking by design: `Paramore.Brighter` targets `netstandard2.0`, which does not support default interface members, so the spec-0023 pattern of adding required surface as a plain abstract member (e.g. `SchemaName`) is reused here. See ADR 0057 "Consequences → Negative" for the rationale.

```csharp
// Before
public class MyMigration : IAmABoxMigration
{
    public int Version => 8;
    public string Description => "Add MyColumn";
    public string UpScript => "ALTER TABLE Outbox ADD COLUMN MyColumn TEXT NULL";
}

// After
public class MyMigration : IAmABoxMigration
{
    public int Version => 8;
    public string Description => "Add MyColumn";
    public string UpScript => "ALTER TABLE Outbox ADD COLUMN MyColumn TEXT NULL";

    // Cumulative column set after this migration applies. The drift test compares this
    // to the live builder DDL — adding a column to the builder without listing it here
    // (or vice versa) fails CI.
    public IReadOnlyCollection<string> LogicalColumns { get; } =
        new[] { /* V1..V7 columns */, "MyColumn" };

    // V2+ migrations carry the commit SHA / PR number that introduced the column.
    public string? SourceReference => "abcd1234 (PR #4xxx)";

    // SQLite-only: existence probe so the runner can skip the ALTER if the column
    // already lives on the table (legacy bootstrap or a half-applied chain).
    // Leave null on MSSQL/PostgreSQL/MySQL — those backends use IF NOT EXISTS in UpScript.
    public string? IdempotencyCheckSql =>
        "SELECT COUNT(*) FROM pragma_table_info('Outbox') WHERE name = 'MyColumn'";
}
```

#### Source-breaking change: `IAmARelationalDatabaseConfiguration.SchemaName`

`IAmARelationalDatabaseConfiguration` gains a new required `string? SchemaName` member used by the box-provisioning runners (and the schema-qualified MSSQL advisory-lock resource — see "Behaviour notes" below). External implementors of the interface will fail to compile until they expose `SchemaName`. The shipped `RelationalDatabaseConfiguration` record in `Paramore.Brighter` already exposes the property and accepts `schemaName:` as an optional named argument, so call sites that use the shipped configuration record require no change.

```csharp
// Before — custom configuration class only needed to cover the message-payload mode
public class MyDatabaseConfiguration : IAmARelationalDatabaseConfiguration
{
    public string ConnectionString { get; }
    public string? OutBoxTableName { get; }
    public string? InboxTableName { get; }
    public bool BinaryMessagePayload { get; }
}

// After — must also expose SchemaName (defaulting to null preserves the V9 default of dbo/public)
public class MyDatabaseConfiguration : IAmARelationalDatabaseConfiguration
{
    public string ConnectionString { get; }
    public string? OutBoxTableName { get; }
    public string? InboxTableName { get; }
    public string? SchemaName { get; }
    public bool BinaryMessagePayload { get; }
}
```

#### Source-breaking change: `UseBoxProvisioning` overload consolidation

The `BrighterBuilderBoxProvisioningExtensions.UseBoxProvisioning` extension previously exposed two overlapping ways to set the migration lock timeout: a `TimeSpan? migrationLockTimeout` parameter on the extension method, and `BoxProvisioningOptions.MigrationLockTimeout` assignable from the configure delegate. The dual surface was confusing and the parameter form did not allow backends to read the timeout late.

The fix removes the parameter. Callers set the timeout exclusively through `BoxProvisioningOptions.MigrationLockTimeout` inside the configure delegate. Backend `AddXxxOutbox`/`AddXxxInbox` methods read the option lazily at registration time, so the order of statements inside the configure delegate does not matter. Existing callers that did not pass `migrationLockTimeout` (the typical case — all in-tree call sites and samples used the default) require no change.

```csharp
// Before
builder.UseBoxProvisioning(opts => opts.AddMsSqlOutbox(config), TimeSpan.FromMinutes(2));

// After — order inside the delegate is free
builder.UseBoxProvisioning(opts =>
{
    opts.AddMsSqlOutbox(config);
    opts.MigrationLockTimeout = TimeSpan.FromMinutes(2);
});
```

#### Additive: per-backend advisory-lock abstraction

The session-level migration-lock collaborator is now substitutable per backend, so tests and advanced integrators (custom connection-pool sharing, external lock-key derivation) can plug in their own implementation. Each runner gains two additive optional constructor parameters (the lock interface plus `Microsoft.Extensions.Logging.ILogger?`); existing two-arg construction continues to work unchanged. Lock-key derivation stays at the runner; the abstraction owns the lock SQL. See ADR 0057 §5b.

* **PostgreSQL**: `IPostgreSqlAdvisoryLock` / `PostgreSqlAdvisoryLock` (in `Paramore.Brighter.BoxProvisioning.PostgreSql`). Owns `pg_try_advisory_lock` / `pg_advisory_unlock`. Runner logs a Warning when `pg_advisory_unlock` returns `false` at release time (previously discarded silently).
* **MySQL**: `IMySqlAdvisoryLock` / `MySqlAdvisoryLock` (in `Paramore.Brighter.BoxProvisioning.MySql`). Owns `GET_LOCK` / `RELEASE_LOCK`. Release returns `bool?` (`true` released by us, `false` held by another, `null` did not exist); runner logs a Warning on any non-`true` outcome, naming the result code, table name, and lock key. Lock-key derivation continues to flow through the existing public `MySqlMigrationLockName.For` helper.
* **MSSQL**: `IMsSqlAdvisoryLock` / `MsSqlAdvisoryLock` (in `Paramore.Brighter.BoxProvisioning.MsSql`). Owns the `sp_getapplock` call. Acquire-only — `@LockOwner = 'Transaction'` means the lock auto-releases on the surrounding transaction's commit or rollback, so the abstraction has no `ReleaseAsync`. Each documented `sp_getapplock` negative return code is now translated into a distinguishable exception type so an operator can react with the right strategy: `-1` (timeout) → `TimeoutException`, `-2` (cancelled) → `OperationCanceledException`, `-3` (deadlock victim) → **new** `MigrationLockDeadlockException`, `-999` (parameter validation / call error) → `ArgumentException`. Previously every `< 0` result was collapsed into a generic `TimeoutException`. The 255-character `@Resource` length guard moves into the abstraction's acquire path. Lock-resource derivation `BrighterMigration_{table}` continues to live at the runner.

#### Behaviour notes

* Spec-0023-era `__BrighterMigrationHistory` rows at `MigrationVersion = 1` are still valid. The runner's normal path resumes from `MAX(V)`, the `IsMigrationAppliedAsync` gate skips the V1 row, and V2..V_latest are applied as ALTERs against the existing table. The original V1 description is preserved verbatim.
* `IAmABoxMigrationRunner.MigrateAsync` now takes a `BoxType boxType` argument so the runner can pick the correct discriminator (`HeaderBag` for outbox, `CommandBody` for inbox) when bootstrapping pre-spec-0023 tables. External callers must add the new argument on recompile.
* Spanner remains a degenerate runner: fresh installs stamp `V_latest` and existing tables either no-op (`MAX(V) == V_latest`), bootstrap to `V_latest` via the discriminator gate (no history row yet), or throw `ConfigurationException` (`MAX(V) != V_latest`, manual recovery required). See ADR 0057 §6.
* The MSSQL advisory-lock resource is `BrighterMigration_<schema>.<table>` (previously `BrighterMigration_<table>`). Two same-named tables in different schemas (e.g. `dbo.Outbox` and `billing.Outbox`) now acquire distinct `sp_getapplock` resources and migrate in parallel instead of serialising on a shared lock. The resource still stays well under the 255-character `@Resource` limit for any realistic `<schema>.<table>` pair.
* The SQLite runner emits `PRAGMA journal_mode=WAL` on every migration call by default. WAL is database-file-wide and persistent, so a host application that has deliberately picked DELETE or TRUNCATE journal mode would have its choice silently overridden. Pass `enableWalMode: false` to `AddSqliteOutbox` / `AddSqliteInbox` (or to the `SqliteBoxMigrationRunner` constructor) to skip the pragma and leave the existing journal mode untouched.

See [ADR 0057](docs/adr/0057-box-schema-versioning-and-migrations.md) and [spec 0027](specs/0027-box-schema-versioning-and-migrations/) for full details.

### Box Provisioning RDD role-interface refactor (spec 0028)

A fourth-pass review of PR #4039 surfaced static helper classes and free-standing runners across spec 0027's BoxProvisioning surface. Spec 0028 restructures that surface around Responsibility-Driven-Design role interfaces and a template-method runner base. The change is purely a structural refactor — no behaviour changes — but it is source-breaking for any external implementor of the affected types. The shipped Brighter call-sites and DI extensions absorb the cascade; existing `UseBoxProvisioning` configure-delegate users require no change. See [ADR 0058](docs/adr/0058-box-provisioning-rdd-role-interfaces.md) and [spec 0028](specs/0028-box-provisioning-rdd-role-interfaces/) for full details.

#### Source-breaking change: detection helpers become instance classes (`{Backend}BoxDetectionHelper`)

The static `{Backend}BoxDetectionHelpers` (plural) classes for all five backends become public instance classes `{Backend}BoxDetectionHelper` (singular) implementing the new role interfaces:

* **Relational four** (MSSQL/PostgreSQL/MySQL/SQLite) implement `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` — adds `DetectCurrentVersionAsync` on top of the base interface.
* **Spanner** implements the base interface `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` only — degenerate fresh-install model per ADR 0057 §6, no version inference. `SpannerBoxDetectionHelpers` was `internal`; the new `SpannerBoxDetectionHelper` is `public`.

Method-signature changes on the new instance methods:

* **MSSQL / PostgreSQL / MySQL**: `string schemaName` widens to `string? schemaName` (existing slot). Each impl substitutes the backend default when null (`"dbo"` / `"public"` / `connection.Database`). Positional argument lists at existing call-sites are unchanged.
* **SQLite / Spanner**: gain a `string? schemaName` parameter inserted between `tableName` and `cancellationToken`. Existing positional call-sites that passed `(connection, tableName, cancellationToken, transaction)` must insert an explicit `null` and become `(connection, tableName, null, cancellationToken, transaction)`. Each impl ignores the parameter.
* **All five backends**: `GetTableColumnsAsync` return type changes from `HashSet<string>` to `IReadOnlyCollection<string>` (looser; symmetric with `IAmABoxMigration.LogicalColumns` and netstandard2.0-compatible).

The widened nullability + return-type looseness are licensed by NF1: the spec 0027 surface had not shipped at the time spec 0028 landed (same PR).

```csharp
// Before
var exists = await MsSqlBoxDetectionHelpers.DoesTableExistAsync(
    connection, "Outbox", "dbo", ct, transaction);

// After
var helper = new MsSqlBoxDetectionHelper();
var exists = await helper.DoesTableExistAsync(
    connection, "Outbox", "dbo", ct, transaction);
// or null for schemaName — the helper substitutes "dbo":
var exists = await helper.DoesTableExistAsync(
    connection, "Outbox", null, ct, transaction);
```

#### Source-breaking change: migration catalogues become instance classes (`{Backend}{Box}MigrationCatalog`)

The static `{Backend}{Box}Migrations` classes (eight total — MSSQL/PG/MySQL/SQLite × Outbox/Inbox) become public instance classes `{Backend}{Box}MigrationCatalog` implementing `IAmABoxMigrationCatalog`. Spanner is exempt per ADR 0057 §6 (no migration catalogue).

```csharp
// Before
IReadOnlyList<IAmABoxMigration> migrations = MsSqlOutboxMigrations.All(config);

// After
IAmABoxMigrationCatalog catalog = new MsSqlOutboxMigrationCatalog();
IReadOnlyList<IAmABoxMigration> migrations = catalog.All(config);
// or receive the catalogue via DI (singleton lifetime registered by AddMsSqlOutbox).
```

#### Source-breaking change: payload-mode validators become instance classes (`{Backend}PayloadModeValidator`)

The static `{Backend}PayloadModeValidator` classes for all five backends become public instance classes implementing `IAmABoxPayloadModeValidator<TConnection>` (single-generic, no `TTransaction`). Method-signature changes:

* **MSSQL / PostgreSQL / MySQL**: `string schemaName` widens to `string?`. Existing positional call-sites are unchanged; each impl substitutes the backend default when null.
* **SQLite / Spanner**: gain a `string? schemaName` parameter inserted between `tableName` and `columnName`. Existing positional call-sites that passed `(connection, tableName, columnName, binaryMessagePayload, cancellationToken)` must become `(connection, tableName, null, columnName, binaryMessagePayload, cancellationToken)`. Each impl ignores the parameter.

#### Source-breaking change: provisioner constructor cascade

All ten existing provisioner classes (`{Backend}{Box}Provisioner` × four relational backends × two box-types, plus the `SpannerOutboxProvisioner`/`SpannerInboxProvisioner` pair) gain three new typed constructor parameters reflecting the static→instance conversion:

* `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` for the relational eight (provisioners call `DetectCurrentVersionAsync` during the bootstrap branch). Spanner's pair receives `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` (base interface — no version-detection capability).
* `IAmABoxMigrationCatalog` for the relational eight (Outbox provisioners receive the Outbox catalogue; Inbox provisioners receive the Inbox catalogue). Spanner's pair: omitted per ADR 0057 §6.
* `IAmABoxPayloadModeValidator<TConnection>` for all ten.

External code that constructs provisioners directly must supply the new parameters. Existing call-sites that wire provisioners via `UseBoxProvisioning(opts => opts.Add{Backend}Outbox(config))` are absorbed by the DI extensions — no change required.

#### Source-breaking change: runner constructor cascade and template-method base

The four relational migration runners (`MsSqlBoxMigrationRunner`, `PostgreSqlBoxMigrationRunner`, `MySqlBoxMigrationRunner`, `SqliteBoxMigrationRunner`) now derive from the new abstract base `SqlBoxMigrationRunner<TConnection, TTransaction>`. Each derived runner forwards new constructor parameters to the base:

* `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` — the typed detection helper.
* `IAmARelationalDatabaseConfiguration` — for `OpenConnectionAsync` to read `ConnectionString`, plus access to `OutBoxTableName`/`InBoxTableName`/payload-mode flags.
* `TimeSpan lockTimeout` — per-runner-instance deployment knob, supplied by `Add{Backend}Outbox`/`Add{Backend}Inbox` from `BoxProvisioningOptions.MigrationLockTimeout`.
* `ILogger? logger` — exposed to derived classes as `protected Logger { get; }` and forwarded into per-backend `IAmAProvisioningUnitOfWork<TTransaction>` construction.

The base owns the `MigrateAsync` algorithm — open connection, create UoW, begin UoW (lock + transaction in backend-specific order), ensure history table, re-detect existence under the UoW (TOCTOU defence per ADR 0057 §3), dispatch on detection state (fresh / bootstrap / normal), commit, rollback-on-throw with `CancellationToken.None`, dispose via `await using`. Each derived runner implements only the irreducibly-backend-specific hooks: `OpenConnectionAsync`, `CreateUnitOfWorkAsync`, `LockResourceFor`, `EnsureHistoryTableAsync`, `RunFreshPathAsync`, `RunBootstrapPathAsync`, `RunNormalPathAsync`. The Spanner runner remains free-standing per ADR 0057 §6 (degenerate fresh-install-only).

External code that constructs the relational runners directly must supply the new parameters. The harmonised UoW lifecycle / cancellation / disposal contract is described in ADR 0058 §B.3.

#### Additive: new public types

Spec 0028 introduces the following net-new public surface (all in `Paramore.Brighter.BoxProvisioning` unless noted):

* **Role interfaces** (5): `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>`, `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` (extends the base), `IAmABoxMigrationCatalog`, `IAmABoxPayloadModeValidator<TConnection>`, `IAmAProvisioningUnitOfWork<TTransaction>`.
* **Abstract base** (1): `SqlBoxMigrationRunner<TConnection, TTransaction>` implementing `IAmABoxMigrationRunner`.
* **Abstract base** (1, sub-phase A): `SqlBoxProvisioner<TConnection, TTransaction>` — abstract base class in `Paramore.Brighter.BoxProvisioning` for the eight relational provisioners (MSSQL/PG/MySQL/SQLite × Outbox/Inbox). Spanner's pair stays free-standing per ADR 0057 §6.
* **Provisioning UoW implementations** (4 — one per relational backend, in each backend's package): `MsSqlProvisioningUnitOfWork`, `PostgreSqlProvisioningUnitOfWork`, `MySqlProvisioningUnitOfWork`, `SqliteProvisioningUnitOfWork`. Each encapsulates that backend's specific lock+transaction pairing and ordering.
* **Detection-helper instance classes** (5): `MsSqlBoxDetectionHelper`, `PostgreSqlBoxDetectionHelper`, `MySqlBoxDetectionHelper`, `SqliteBoxDetectionHelper`, `SpannerBoxDetectionHelper`.
* **Migration-catalogue instance classes** (8): `{MsSql,PostgreSql,MySql,Sqlite}{Outbox,Inbox}MigrationCatalog` (Spanner exempt).
* **Payload-validator instance classes** (5): `{MsSql,PostgreSql,MySql,Sqlite,Spanner}PayloadModeValidator`.

DI extensions in each `Add{Backend}{Box}` register the detection helper, catalogue, and payload validator as singletons (each role-impl is stateless after construction); existing call-site shape `UseBoxProvisioning(opts => opts.Add{Backend}Outbox(config))` is unchanged.

### Multi-Tenancy Migration History Scope (spec 0029)

The box migration-history table can now be placed **per tenant schema** instead of always landing in the backend default schema. The default behaviour is unchanged — existing deployments keep `__BrighterMigrationHistory` in `dbo` / `public` / the connection-bound database regardless of `SchemaName`. Set `BoxProvisioningOptions.MigrationHistoryScope = MigrationHistoryScope.PerSchema` to opt this deployment into per-schema placement on MSSQL and PostgreSQL. See [ADR 0060](docs/adr/0060-multi-tenancy-migration-history-scope.md) and [spec 0029](specs/0029-multi-tenancy-migrations/) for full details.

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

#### Additive: new public types

* **Enum**: `MigrationHistoryScope` in `Paramore.Brighter.BoxProvisioning` with values `Global` (default — today's behaviour) and `PerSchema`.
* **Property**: `BoxProvisioningOptions.MigrationHistoryScope` (defaults to `MigrationHistoryScope.Global`).

#### Source-breaking change: `IAmABoxMigrationDetectionHelper.DoesHistoryExistAsync` / `GetMaxVersionAsync` gain a `historySchema` parameter

`IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` gains a `string? historySchema` parameter on `DoesHistoryExistAsync` and `GetMaxVersionAsync` (placed after the existing `schemaName`). The derived `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` interface file itself is unchanged — its implementors inherit the new signature through interface inheritance. `null` means "the backend default" — i.e. today's behaviour — so the bundled Brighter detection helpers and call-sites are byte-for-byte unchanged. External implementors of either interface must add the new parameter on recompile; passing `null` preserves existing semantics.

```csharp
// Before
Task<bool> DoesHistoryExistAsync(
    TConnection connection, string tableName, string? schemaName,
    CancellationToken cancellationToken = default,
    TTransaction? transaction = null);

// After
Task<bool> DoesHistoryExistAsync(
    TConnection connection, string tableName, string? schemaName, string? historySchema,
    CancellationToken cancellationToken = default,
    TTransaction? transaction = null);
```

`DetectCurrentVersionAsync` is **unchanged** — it reads box-table columns, not history.

#### Source-breaking change: runner constructor cascade gains an optional `scope` parameter

The four relational runners (`MsSqlBoxMigrationRunner`, `PostgreSqlBoxMigrationRunner`, `MySqlBoxMigrationRunner`, `SqliteBoxMigrationRunner`) and the abstract base `SqlBoxMigrationRunner<TConnection, TTransaction>` gain a final `MigrationHistoryScope scope = MigrationHistoryScope.Global` constructor parameter. The default keeps existing positional call-sites compiling; external code that constructs the runners directly with named arguments past this position will need a small adjustment. `Add{Backend}Outbox`/`Add{Backend}Inbox` absorb the cascade and read `BoxProvisioningOptions.MigrationHistoryScope`; existing DI call-sites are unchanged.

#### Source-breaking change: `EnsureHistoryTableAsync` hook gains a `tableName` parameter

The `protected abstract Task EnsureHistoryTableAsync(...)` hook on `SqlBoxMigrationRunner<TConnection, TTransaction>` gains a `string tableName` parameter. The MSSQL and PostgreSQL hook implementations use it to filter the `Global → PerSchema` auto-seed to this tenant's rows; MySQL and SQLite accept and ignore it. External code that derives from `SqlBoxMigrationRunner<TConnection, TTransaction>` (rare — designed for the four shipped backends) must thread the new parameter through.

#### Behaviour notes

* **Backend support.** Only MSSQL and PostgreSQL honour `PerSchema` placement. MySQL (where schema == database), SQLite (no schema concept), and Spanner (degenerate fresh-install-only model per ADR 0057 §6) treat `PerSchema` as a no-op and keep history in their default location — no exception, so a single `BoxProvisioningOptions` can target a mixed backend set without per-backend branching. The placement decision is surfaced per run via an `Information` log of the form `Box migration history for {BoxTable} resolved to schema {HistorySchema} (scope {Scope})` (on no-op backends `HistorySchema` is the literal `<backend default>`).
* **Global → PerSchema auto-seed.** After flipping a previously-`Global` MSSQL/PG deployment to `PerSchema`, the runner copies this tenant's prior history rows from the legacy default-schema table into the per-schema table under the same advisory lock and transaction as the CREATE — existing migrations are not re-applied. The seed copies all five columns (`MigrationVersion`, `SchemaName`, `BoxTableName`, `Description`, `AppliedAt`) filtered by `(SchemaName, BoxTableName)`, with a composite-primary-key `NOT EXISTS` guard so repeated flips are idempotent. The seed runs on **every** PerSchema provision (so the second box-type to flip — e.g. inbox after outbox — still gets seeded into the per-schema history table the first flip created); the NOT EXISTS guard makes steady-state runs a zero-row no-op. A distinct `Information` log records `Seeded {RowCount} legacy history row(s) for {BoxTable} from {LegacySchema} to {TargetSchema}` plus an OpenTelemetry `Activity` event `legacy_history_seeded` carrying the row count as the `brighter.box.migration.seed.rows` tag.
* **Permission requirement (every run, not just the first flip).** Because the seed's `INSERT…SELECT` executes on every PerSchema provision, the runner needs `SELECT` on the legacy default-schema history table for the **lifetime of the PerSchema deployment**. Operators who grant `SELECT` only for the initial flip and then revoke it will hit a `ConfigurationException` on every subsequent provision run, with the inner provider exception attached.
* **Reverse flip (`PerSchema → Global`) and legacy-row cleanup are out of scope.** The per-schema history table remains in the tenant's schema if a deployment is later switched back to `Global`; the legacy default-schema rows survive after a PerSchema flip. Both are harmless but storage-redundant; operators wanting to reclaim that storage must run their own ad-hoc DELETE / DROP.
* **Misconfiguration.** Selecting `PerSchema` on a placement backend with a `null` `SchemaName` throws `ConfigurationException` at the entry to the runner. Per-tenant identifiers flow through `Identifiers.AssertSafe` before any DDL is emitted, so an injection-shaped `SchemaName` is rejected at the provisioner entry well before reaching the database.

### Replace Primitive Obsession in Box Provisioning with Value Types (spec 0030)

The box-provisioning contracts now use dedicated value types instead of bare `string`/`int`, following the `Id` template and [ADR 0019 "Avoid Primitive Obsession"](docs/adr/0019-avoid-primitive-obsession.md). See [ADR 0061](docs/adr/0061-box-provisioning-value-types.md) and [spec 0030](specs/0030-primitive_obsession/) for full details.

#### Additive: new value types

Six value types land in `Paramore.Brighter.BoxProvisioning`, each with bidirectional implicit conversions to/from its underlying primitive, so existing string/int call-sites continue to compile unchanged:

* **`BoxTableName`**, **`MigrationDescription`**, **`SqlScript`**, **`SourceReference`** — wrap `string`.
* **`SchemaName`** — wraps `string`; `null` models "not supplied" (e.g. SQLite has no schema).
* **`MigrationVersion`** — wraps `int`, with arithmetic and `IComparable` ordering preserved through the implicit `int` conversion.

The `IAmABoxMigration` / `BoxMigration` and `IAmABoxMigrationRunner` surfaces are retyped to these value types. Because the conversions are implicit and bidirectional, this is **source-compatible** for callers passing primitives; external implementors overriding members will see the value types in the new signatures.

#### Source-nullability ripple: core implicit `operator string` widened to `operator string?`

While applying the value-type pattern we corrected a latent null-safety bug in nine existing core value types — `Id`, `RoutingKey`, `CloudEventsType`, `PartitionKey`, `SubscriptionName`, `TraceContext.TraceParent`/`TraceState` (in `Paramore.Brighter`), and `ConsumerName`/`HostName` (in `Paramore.Brighter.ServiceActivator`). These are **reference-type** records/classes, and a user-defined conversion on a reference type is *not* null-lifted the way a `Nullable<T>` conversion is: `(string?)(T?)null` invoked `operator string` on a null receiver and threw a `NullReferenceException`. Each operator changed from `operator string(T t) => t.Value` to the null-safe `operator string?(T t) => t?.Value`, matching the long-standing `ChannelName` precedent.

* **Binary-compatible.** `string` and `string?` are the same IL type, so already-compiled consumers are unaffected at runtime.
* **Source ripple for downstream NRT consumers.** Because these operators now return `string?`, downstream projects with nullable reference types enabled will see `CS8600`/`CS8604` where the result feeds a non-nullable `string`, e.g.:

  ```csharp
  string topic = someRoutingKey;        // CS8600: converting string? to string
  dict.Add(someId, value);              // CS8604: someId is now string?
  ```

  The fix is the same one applied throughout this PR across ~60 in-box assemblies: take the underlying value explicitly with `.Value` (which is non-nullable), or `?.Value ?? fallback` when the source is itself nullable:

  ```csharp
  string topic = someRoutingKey.Value;          // non-nullable source
  dict.Add(someId.Value, value);
  string reply = header.ReplyTo?.Value ?? "";   // nullable RoutingKey? source
  ```

  No call-site fix is needed unless your code both has NRT enabled and treats warnings as errors.

> Note: `Tenant` (in `Paramore.Brighter.Transformers.JustSaying`) is a `readonly record struct`, not a reference type — its receiver can never be null, so its `operator string` is intentionally left non-nullable.

### Per-message factory scope leak fix; transient handler lifetime now isolates its DI scope (#4252, #4254)

`ServiceProviderLifetimeScope` — the lifetime helper shared by the handler, mapper and transformer factories — previously created a **single** `IServiceScope` per factory and reused it for every transient resolution. For the app-lifetime mapper and transformer factories that scope was never released per message, so a transient mapper or transform accumulated one scope per message for the life of the process — the leak reported in #4252. Because `MapperLifetime` and `TransformerLifetime` both **default to `Transient`** (`ServiceLifetime.Transient`), this was the default code path, not an opt-in one. `GetTransient` now creates a fresh `IServiceScope` per resolution, tracked by the scope's own identity and disposed when the resolution's lease is released, closing the leak: each transient mapper/transform now gets and releases its own scope.

#### Breaking change: `Create`/`Get` return an opaque `Lease<T>`, and `Release` keys on the lease, across six public factory / registry interfaces

Closing the leak on the mapper path required a way to return a mapper to its factory (transformers already had one), and returning a mapper/transform to the *right* resolution's scope required keying release on the resolution rather than the instance. The factory and registry surface therefore now flows an opaque `Lease<T>` (see *Opaque lease* below) from `Create`/`Get` to `Release`:

* `IAmAMessageMapperFactory` — `Lease<IAmAMessageMapper>? Create(Type)`, `void Release(Lease<IAmAMessageMapper>?)`
* `IAmAMessageMapperFactoryAsync` — `Lease<IAmAMessageMapperAsync>? Create(Type)`, `void Release(Lease<IAmAMessageMapperAsync>?)`, `ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>?)`
* `IAmAMessageTransformerFactory` — `Lease<IAmAMessageTransform>? Create(Type)`, `void Release(Lease<IAmAMessageTransform>?)`
* `IAmAMessageTransformerFactoryAsync` — `Lease<IAmAMessageTransformAsync>? Create(Type)`, `void Release(Lease<IAmAMessageTransformAsync>?)`, `ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync>?)`
* `IAmAMessageMapperRegistry` — `Lease<IAmAMessageMapper<T>>? Get<T>()`, `void Release<T>(Lease<IAmAMessageMapper<T>>?)`
* `IAmAMessageMapperRegistryAsync` — `Lease<IAmAMessageMapperAsync<T>>? GetAsync<T>()`, `void Release<T>(Lease<IAmAMessageMapperAsync<T>>?)`, `ValueTask ReleaseAsync<T>(Lease<IAmAMessageMapperAsync<T>>?)`

`Release`/`ReleaseAsync` take the lease as `Lease<T>?` and treat a `null` lease as a no-op, so the "over-release is harmless" contract holds for the `null` a caller following the "release what you `Get`" rule may still hold (`Get`/`Create` return `Lease<T>?`) without a hand-written null check.

`Paramore.Brighter` targets `netstandard2.0`, which has no runtime support for default interface members, so these ship without a default body — **any third-party implementation of these interfaces must update to the lease-typed signatures** to compile, and any caller holding a `Create`/`Get` result as a bare mapper/transform must read it through `.Instance` and release the lease. All in-tree implementations are updated; the `Func`-based `SimpleMessageMapperFactory` constructor is unchanged (it wraps the `Func` result in a no-op lease), so its call sites are unaffected.

##### Opaque lease

`Lease<T>` (new, in `Paramore.Brighter`) is a small `sealed class` pairing the resolved `Instance` with an opaque `ReleaseToken`. The token — for the DI-backed factories, the resolution's own `IServiceScope` — lets the factory reclaim exactly the one resolution being released, so a shared instance handed out under a transient lifetime is torn down one resolution at a time and an over-release is a no-op. A factory that opens a per-resolution scope **must** return a lease carrying its release token (`new Lease<T>(instance, token)`); the token-less "reclaims nothing on release" case — a shared instance, or a no-op factory — is built through the named `Lease<T>.ForSharedInstance(instance)`. There is no implicit conversion from `T`: a bare `return mapper;` from a custom factory does not compile, so a scope-owning factory cannot silently produce a token-less lease whose `Release` is a no-op (which would reopen the leak this change closes). If you implement `IAmAMessageMapperFactory`/`IAmAMessageTransformerFactory` (or their async forms), return `new Lease<T>(instance, token)` when you open a scope, `Lease<T>.ForSharedInstance(instance)` when you hand out a shared instance, and `null` when nothing resolves.

The two mapper-registry interfaces also gain a type-resolution member so a caller can ask *"is a mapper registered for this request?"* without creating (and then having to release) one:

* `IAmAMessageMapperRegistry` — `(Type? MapperType, bool IsDefault) ResolveMapperInfo(Type requestType)`
* `IAmAMessageMapperRegistryAsync` — `(Type? MapperType, bool IsDefault) ResolveAsyncMapperInfo(Type requestType)`

Both mirror `Get`/`GetAsync` (factory-aware, same default and generic-definition guards) without instantiating. `TransformPipelineBuilder[Async].HasPipeline` now answers through them, so the outbox send/receive path no longer creates and releases a throwaway probe mapper per message. Same netstandard2.0 rule: third-party registry implementations must add the member to compile.

#### Breaking change: validation/diagnostics constructors take a `Func<MessageMapperRegistry>`

Making registry ownership explicit (the disposer creates the disposable it disposes) changed three `public` signatures. These are compile-time source breaks — the registry parameter moved from an instance to a factory delegate:

| Symbol | Was | Now |
|---|---|---|
| `PipelineValidator` constructor, param 6 | `MessageMapperRegistry? mapperRegistry` | `Func<MessageMapperRegistry>? mapperRegistryFactory` |
| `PipelineDiagnosticWriter` constructor, param 3 | `MessageMapperRegistry? mapperRegistry` | `Func<MessageMapperRegistry>? mapperRegistryFactory` |
| `ConsumerValidationRules.UnwrapTransformResolvable` | `(MessageMapperRegistry, IAmATransformerResolvabilityProbe)` | `(Func<MessageMapperRegistry>, IAmATransformerResolvabilityProbe)` |

Both constructors take the registry positionally among optional parameters, so anyone constructing a `PipelineValidator` or `PipelineDiagnosticWriter` directly — in a test or a custom host — and anyone calling the `public static` `UnwrapTransformResolvable` rule must pass a factory (`() => registry`) instead of the registry. The rule now invokes the factory once and owns/disposes only the registry it created, so it can never dispose a caller's shared registry.

> **`Create`/`Get`/`GetAsync` now return an opaque `Lease<T>`, and `Release` takes that lease.** This is a breaking signature change on the factory and registry surface: `IAmAMessageMapperFactory[Async].Create`, `IAmAMessageTransformerFactory[Async].Create`, and `IAmAMessageMapperRegistry[Async].Get<T>`/`GetAsync<T>` now return a `Lease<T>?` (null when nothing resolves), and `Release`/`ReleaseAsync` take a `Lease<T>` rather than the bare instance. A `Lease<T>` pairs the resolved `Instance` with an opaque `ReleaseToken`; hold the lease from create to release. If you resolve a mapper or transform directly, you must still call `Release` (or `ReleaseAsync`) when finished, **even for a non-disposable mapper such as the default `JsonMessageMapper`** — a transient resolution opens an `IServiceScope` per resolution (it can own the instance's injected dependencies and its own `IServiceProvider`), and that scope is retained until the lease is released or the factory is disposed at host shutdown. All in-tree call sites already release. Note that `SimpleMessageMapperFactory`'s `Release` is a deliberate no-op — the `Func` you supply owns what it returns — so a `Func` that news up a disposable mapper is not disposed for you.

The lease keys release on the **resolution**, not the instance, which designs out a whole bug class: a mapper or transform registered in the container as a `Singleton` (or otherwise handing back one shared instance) while its `MapperLifetime`/`TransformerLifetime` is the default `Transient` opens a fresh scope per resolution over the same object. Keyed by instance identity (the previous model) releasing one resolution could dispose a scope another still-live resolution depended on — a use-after-dispose — and an over-release could pop yet another. Keyed by the lease, `Release(lease)` disposes exactly that resolution's scope, and an over-release of a lease is an **idempotent no-op**. Because the lease's generic argument carries the interface (`Get<T>` returns a `Lease<IAmAMessageMapper<T>>`, `GetAsync<T>` a `Lease<IAmAMessageMapperAsync<T>>`), releasing a dual-interface mapper resolved from `GetAsync` through the sync factory is now a **compile-time type error** rather than a silent leak — no interface cast is needed on the concrete registry:

```csharp
var registry = ServiceCollectionExtensions.MessageMapperRegistry(sp);

var lease = registry.Get<MyCommand>();                        // Lease<IAmAMessageMapper<MyCommand>>?
// ...use lease.Instance...
registry.Release(lease);                                      // binds to the sync overload by lease type

var asyncLease = registry.GetAsync<MyCommand>();              // Lease<IAmAMessageMapperAsync<MyCommand>>?
registry.Release(asyncLease);                                 // ...or ReleaseAsync(asyncLease)
```

#### Deterministic factory disposal at host shutdown

The IoC-backed mapper and transformer factories (`ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory`, `ServiceProviderTransformerFactoryAsync`) are created for, and owned by, the objects that use them; they are not registered in the container. Those owners now dispose them, so every per-resolution `IServiceScope` they retain is drained at teardown instead of being held until the process exits:

* `MessageMapperRegistry` is now `IDisposable` and disposes the two mapper factories it was built from.
* `OutboxProducerMediator.Dispose()` now disposes the `MessageMapperRegistry` (cascading to both mapper factories) and the two transformer factories, in addition to closing the producer registry.
* `PipelineValidator` and `PipelineDiagnosticWriter` are now `IDisposable` and dispose the validation/diagnostic-time `MessageMapperRegistry` each builds.

* `Dispatcher` is now `IDisposable` and disposes the `MessageMapperRegistry` (cascading to both mapper factories) and the two transformer factories built for it — the consumer-side counterpart to the mediator's producer-side disposal.

All of these owners are registered as container singletons, so the container disposes them — and therefore drains their factories — at host shutdown. Previously none were disposed, so a scope a direct resolver failed to release was held for the life of the process. The `IDisposable` additions themselves are binary-compatible; note, however, that the `PipelineValidator` / `PipelineDiagnosticWriter` constructor and `UnwrapTransformResolvable` signatures **did** change — see *Breaking change: validation/diagnostics constructors* above.

> **Ownership note for manual wiring.** Because `MessageMapperRegistry` is now `IDisposable` and `OutboxProducerMediator.Dispose()` cascades into the `IAmAMessageMapperRegistry` **and both transformer factories** it was given, **disposing a `CommandProcessor`'s external bus now disposes the mapper registry and the transformer factories you handed it.** The standard manual shape is `DispatchBuilder.MessageMappers(registry, registryAsync, transformFactory, transformFactoryAsync)`, so the transform factories are shared just as readily as the registry. In the DI path this is airtight — each mediator gets its own registry and factories newed per resolution — but if you **manually** share a `MessageMapperRegistry` or a `ServiceProviderTransformerFactory` between a `CommandProcessor` external bus and a `Dispatcher`, disposing the command processor disposes objects the dispatcher is still using; subsequent mapper or transform resolutions then throw `ObjectDisposedException` per message. This is a runtime break with no compile-time signal. If you share a registry or transformer factory across independently-disposed owners, give each owner its own, or defer disposal until all owners are done.

**This shutdown disposal is a teardown backstop, not a substitute for `Release`.** It reclaims once, at host shutdown; it does nothing for a host that runs for days. Releasing each mapper/transform you resolve directly — as the pipeline does on every message — is what bounds retention *during* the run. Relying on owner disposal for reclamation along the way just reproduces the unbounded accumulation this fix closes.

#### Breaking change: `IBrighterOptions` gains `IsolateTransientHandlerScope`

`IBrighterOptions` (in `Paramore.Brighter.Extensions.DependencyInjection`) gains a `bool IsolateTransientHandlerScope { get; set; }` member — the opt-out described in the *Behaviour change* below. `Paramore.Brighter.Extensions.DependencyInjection` targets `netstandard2.0`, which has no default interface members, so this ships without a default body: **any third-party implementation of `IBrighterOptions` must add the member to compile.** In-tree there is exactly one implementer (`BrighterOptions`), which defaults it to `true` (isolate), so the change is invisible to CI but a source break for external implementers — the same class of break as the factory/registry interfaces above.

#### Behaviour change: transient handler lifetime isolates its DI scope per handler

On the **default** configuration — `HandlerLifetime.Transient` with `IsolateTransientHandlerScope = true` — two handlers in the same pipeline that each inject a DI-`Scoped` dependency (an EF Core `DbContext`, a unit of work, a transaction provider) now receive **two instances, and therefore two transactions, where they previously received one.** If your handlers relied on a single `DbContext` / one transaction being shared across the pipeline for a message, that unit of work is now split — see the fix below.

This is the only **observable semantic** change in this release, and it is invisible at compile time — no signature change and no exception; only the number of DI-`Scoped` instances a pipeline sees changes. (The interface additions above are the compile-time breaks.) It lands on the **default** `HandlerLifetime` (`Transient`).

A handler pipeline for a single message resolves every handler in the chain — attribute/middleware handlers plus the target handler — through one `IAmALifetime`. Because all transient resolutions used to share that factory's single `IServiceScope`, a dependency **registered in DI as `Scoped`** (an EF Core `DbContext`, a unit of work, a transaction provider) was one shared instance across the whole chain for that message. Now each transient handler is resolved in its **own** `IServiceScope`, so a DI-`Scoped` dependency is a **distinct instance per handler** — the two-contexts / two-transactions consequence above.

This aligns `Transient` with its DI meaning (a transient resolution is genuinely isolated) and is what allows a transient's scope to be its own to create and release, which is what closes the leak above.

**If you rely on a DI-`Scoped` dependency being shared across the handlers in a pipeline** — the unit-of-work / one-transaction-per-message pattern — set the **handler** lifetime to `Scoped`:

```csharp
services.AddBrighter(options =>
{
    options.HandlerLifetime = ServiceLifetime.Scoped; // default is Transient
});
```

Under `Scoped`, every handler in the pipeline shares one `IServiceScope`, so a `Scoped` dependency is a single instance for the message — the pre-fix sharing behaviour.

Switching the **handler** lifetime to `Scoped` is the intended way to share state across a pipeline, and almost certainly what you want if you were depending on the old sharing — under `Transient` it was never a designed behaviour, just a side effect of the shared scope. So we do not expect anyone to need an escape hatch. If you do rely on the pre-#4254 sharing and cannot move to `Scoped` immediately, you can restore it **without changing the handler lifetime** by setting `IsolateTransientHandlerScope = false`:

```csharp
services.AddBrighter(options =>
{
    options.IsolateTransientHandlerScope = false; // default is true; prefer HandlerLifetime.Scoped
});
```

With the flag off, the transient handlers in one pipeline again share a single `IServiceScope` (disposed when the pipeline completes), so a DI-`Scoped` dependency is one shared instance across the chain — the pre-#4254 behaviour — while `Transient` still means a fresh handler instance per resolution. The flag governs **only** transient handlers; it has no effect on `Scoped` or `Singleton` handlers, nor on the mapper and transformer factories, which always isolate (that is the leak fix). It defaults to `true`, so the isolating behaviour described above is the default, and the flag is a compatibility fallback rather than a knob most applications should touch.

#### Other observable changes

A few smaller changes that are unlikely to affect you but are observable:

* **`IAmAMessageMapperRegistry.Get<TRequest>()` no longer registers the default mapper it falls back to.** When no mapper is registered for `TRequest`, `Get<TRequest>()` / `GetAsync<TRequest>()` still returns the default mapper, but now records that resolution in a separate cache rather than writing it into the registration table. So a fallback no longer answers *"a mapper is registered for `TRequest`"*, and a subsequent `Register<TRequest, TMapper>()` for that type **succeeds** where it previously threw `ArgumentException("… already has a mapper")`. An explicit `Register` still always wins on a later `Get`.
* **`ServiceProviderHandlerFactory.Release` no longer disposes the handler itself; it disposes the per-resolution scope instead.** Both `Release` overloads dropped the old `if (handler is IDisposable d) d.Dispose();` — for the Transient and Scoped lifetimes the handler was resolved from a `ServiceProviderLifetimeScope`, and disposing that scope is what disposes the handler, exactly once (disposing it here as well double-disposed it). One case changes observably: a handler **registered in the container as a singleton** (`services.AddSingleton<MyDisposableHandler>()`) but resolved under the **default `HandlerLifetime.Transient`** comes from the root provider, so the per-resolution child scope tracks nothing and disposes nothing — that handler used to be `Dispose()`d after every message and now is not disposed until the root provider is torn down at host shutdown. Disposing a process-wide singleton once per message was itself a bug, so this is a fix, but it is an observable change on the handler path. If you relied on it, register the handler as transient (`services.AddTransient<MyDisposableHandler>()`) so each per-message scope owns and disposes it.
* **Resolving a mapper or transform after its factory is disposed now throws `ObjectDisposedException`.** `ServiceProviderMapperFactory` / `ServiceProviderTransformerFactory` (through `ServiceProviderLifetimeScope.GetOrCreate`) previously returned an instance from an already-disposed factory; they now throw. In practice this only surfaces if an in-flight message resolves a mapper during host shutdown, after the factory has been disposed — a race the `Dispatcher` shutdown drain below now closes on the consumer side. The `ObjectDisposedException` message names the configured lifetime and points at the shared-registry cause rather than an internal type name.
* **`Dispatcher.Dispose()` now stops the pumps and drains their in-flight message before disposing the mapper/transform factories, bounded by a configurable `ShutdownTimeout`.** Because `Dispose()` cascades disposal into the factories (see the ownership item below), a container teardown that raced a still-running pump — the host's `ShutdownTimeout` elapsed before the consumers drained, or the provider was disposed without a graceful stop — could tear the factories down under an in-flight message, surfacing the `ObjectDisposedException` above, which was reclassified as *Unacceptable* and the good message **rejected and discarded**. `Dispose()` now calls `End()` first (which pushes a quit onto each pump so it runs out its current message, acknowledges it, and stops) and waits for the drain before disposing. If the drain exceeds the timeout, disposal proceeds anyway and the interrupted message is left **un-acknowledged** so the broker redelivers it rather than dropping it. The wait is bounded by a new `TimeSpan ShutdownTimeout` (default **10 seconds**), configurable so a consumer with long-running handlers (for example video processing) can allow more time: set it on the `AddServiceActivator` consumer options (`IAmConsumerOptions.ShutdownTimeout`), via `DispatchBuilder.Build(shutdownTimeout: …)`, or the `Dispatcher` constructor's new trailing optional `shutdownTimeout` parameter. **Source break for external implementers:** `IAmConsumerOptions` gains a `ShutdownTimeout` member with no default interface body (the assembly targets `netstandard2.0`); the in-tree `ConsumersOptions` implements it defaulting to 10s. `IAmADispatchBuilder.Build` and the `Dispatcher` constructor gain trailing optional parameters (source-compatible for callers; binary-breaking, so recompile against this version).
* **Release each resolution's lease when finished; over-releasing a lease is now a safe no-op.** Release keys on the `Lease<T>` (the resolution), not the instance, so `Release(lease)` disposes exactly that resolution's scope. This designs out the former shared-instance hazard: a mapper or transform registered in the container as a `Singleton` (or otherwise handing back one shared instance) under the default `Transient` `MapperLifetime`/`TransformerLifetime` no longer risks one resolution's release disposing another still-live resolution's scope, and a spurious second `Release` of the same lease — or a `Release(null)`, since `Get`/`Create` return `Lease<T>?` — is an idempotent no-op rather than a pop of another resolution's scope or a `NullReferenceException`. All in-tree call sites already release exactly once; this only concerns code that resolves and releases mappers/transforms directly.
* **`HasPipeline` now answers by resolving the mapper *type*, not by creating a probe instance** (see `ResolveMapperInfo` above). This changes the exception on one narrow misconfiguration — a mapper *type* registered but whose *instance* cannot be built (a `SimpleMessageMapperFactory` `Func` that returns `null`, or a `Register` without a matching container registration). The type is kept in sync with the container on the `AddBrighter` path, so this does not arise there.
  * **Send path** (`OutboxProducerMediator.MapMessage` / `MapMessageAsync`): `HasPipeline` now returns `true` for that type, so building the pipeline fails and throws `ConfigurationException` (with the underlying `InvalidOperationException` as its inner exception) rather than the previous `ArgumentOutOfRangeException("No message mapper defined for request")`.
  * **Reply path** (`CreateRequestFromMessage`): where an unresolvable *async* mapper type previously made the async probe `false` and the reply **fell through to the sync pipeline**, it now throws `ConfigurationException` on the async attempt instead of silently using the sync pipeline. The normal fall-through — no async mapper registered at all — is unchanged.
* **A failed release from a transform scope or pipeline `Dispose`/`DisposeAsync` now surfaces as an `AggregateException`.** The transform lifetime scope drains every tracked transform in one pass and collects any release failures, throwing them together as an `AggregateException` (a single failure is wrapped too); previously the first failure propagated unwrapped. If both a transform-scope disposal and the pipeline's mapper release throw, both are surfaced (the mapper-release failure no longer masks the transform one). Every in-tree caller logs and swallows release failures, so this has no functional impact on the standard paths; it only matters to code that disposes a `TransformPipeline`/`TransformLifetimeScope` directly and catches a specific exception type — catch `AggregateException` (or its `InnerExceptions`).
* **`Dispatcher` and `OutboxProducerMediator` dispose the mapper registry and transform factories only when they own them.** Both types became `IDisposable`/gained disposal of the runtime mapper/transform graph in this release. Ownership is now explicit: the constructors (and `DispatchBuilder.Build`) take `ownsRegistry`/`ownsTransformerFactories`, both **defaulting to `false`**. The DI paths (`AddServiceActivator`/`AddBrighter`) new up a graph solely for their owner and pass `true`, so container teardown disposes it as before. On the **manual-wiring** path — where a `MessageMapperRegistry` is commonly shared between a `Dispatcher` and a `CommandProcessor`'s external bus — the default `false` means neither disposes the shared registry out from under the other; if you construct these directly and want them to own (dispose) the graph, pass `ownsRegistry: true, ownsTransformerFactories: true`.

## Release 10.0.0

With V10 we have made a number of significant changes to Brighter. There are breaking changes that you will need to be aware of. However, most of the changes required are straightforward to make. A summary of the most important changes:

* **Cloud Events**:We now have full support for Cloud Events headers; you can set values in your Publication and have them reflected on messages.
* **Open Telemetry**: We now support the OpenTelemetry Semantic Conventions for Messaging. This will mean that you have different traces to V9, where the OTel conventions were Brighter's own.
* **Default Message Mappers**: There is no need to provide a mapper if your goal is to serialize your body as JSON. You can use a default mapper. You can create your own default mapper for other formats. You only need explicit mappers for complex transform pipelines.
* **Dynamic Message Deserialization**: Previously we required that you used a DataType Channel (one type per channel). Whilst we recommend this, and it remains the default you can now provide a callback to determine the message type from the message itself, such as via the Cloud Events type, before deserializing.
* **Agreement Dispatcher**: We now support a callback for determining the handler to dispatch a Command or Event to. Previously we matched request and handler based on the request type. Whilst this is still a default, you can now add a callback to dynamically determine the handler from the request and the request context.
* **Request Context Improvements**: You can now inject the RequestContext more easily into a pipeline. The RequestContext now supports the `OriginatingMessage` for subscriptions to queues or streams.
* **Reactor and Proactor**: We have made considerable under-the-hood improvements to synchronous and asynchronous message pumps in your consumer. The asynchronous pipeline is now end-to-end.
* **Scheduled Requests/Messaging**: We now support integration with schedulers, like Quartz.NET, Hangfire, or AWS Scheduler. This can be used with requests or messages. We use this support internally, if available, to allow "Requeue with Delay" where the messaging protocol does not natively support it.
* **Nullability**: We have enabled nullable reference types.
* **Simplified Configuration**: We have tried to make configuration simpler, including renaming obscure methods. This needs more work in future releases.

### Cloud Events Support

Full Cloud Events specification support has been added across all supported messaging protocols:

* **Publication**: Support for Cloud Events on the Publication with configurable additional properties
* **Message Mapper**: The Publication is passed into the message mapper, allowing you to read CloudEvents properties
* **Default Mappers**: The default `JsonMessageMapper` writes `binary` Cloud Event headers, and the default `CloudEventJsonMessageMapper` writes `structured` Cloud Events Headers.
* **Transport Integration**: We support writing and reading CloudEvents headers across all supported messaging protocols.
* **Message Routing**: Use Cloud Events type for message deserialization (see below).

### OpenTelemetry Integration

Comprehensive OpenTelemetry support has been added throughout Brighter. We support the [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/concepts/semantic-conventions/):

* **Span Attributes**: OpenTelemetry across all Brighter request handler pipelines.
* **Transport Tracing**: Automatic trace propagation across message boundaries, with support for W3C TraceContext.
* **Outbox Tracing**: Distributed tracing for all outbox implementations.
* **Inbox Tracing**: OpenTelemetry support for all inbox implementations.
* **Claim Check Tracing**: Tracing support for claim check pattern and luggage stores.
* **Instrumentation Control**: Configurable instrumentation options across all tracer operations.

OpenTelemetry integration enables end-to-end distributed tracing across message boundaries, making it easier to diagnose performance issues and understand message flow in distributed systems.

### Default Message Mappers

We no longer require that you implement `IAmAMessageMapper` for each Producer and Consumer message pipeline.

* **Built-in Fallback**: Brighter will attempt to use appropriate default mappers when no explicit mapper is registered
* **JsonMapper**: Automatically handles JSON serialization/deserialization for messages with `binary` CloudEvents support
* **CloudEventsMapper**: Automatically handles JSON serialization/deserialization for messages with `structured` CloudEvents support

You only need to create custom message mappers when you require explicit transforms or have specific serialization requirements. The default mappers can also serve as templates for custom implementations.

```csharp
 services.AddBrighter(options =>
  {
      ... 
  })
  .AddProducers((configure) =>
  {
    ...
  })
  //This is the default mapper type, so you can omit it, but we are explicit for this note to show how to register your own default
  .AutoFromAssemblies([typeof(TaskCreated).Assembly], defaultMessageMapper: typeof(JsonMessageMapper<>), asyncDefaultMessageMapper: typeof(JsonMessageMapper<>));
```

### Dynamic Message Deserialization

Brighter now supports multiple message types on the same channel through dynamic request type resolution. This enables content-based deserialization where the message type is determined at runtime from metadata rather than compile-time generic parameters. We still support the older DataType channel approach. As routing to a handler is based on type, this will decide the handler that receives this message (although see also Agreement Dispatcher).

```csharp
new KafkaSubscription(
    new SubscriptionName("paramore.example.taskstate"),
    channelName: new ChannelName("task.state"),
    routingKey:new RoutingKey("task.update"),
    getRequestType: message => message switch
    {
        var m when m.Header.Type == new CloudEventsType("io.goparamore.task.created") => typeof(TaskCreated),
        var m when m.Header.Type == new CloudEventsType("io.goparamore.task.updated") => typeof(TaskUpdated),
        _ => throw new ArgumentException($"No type mapping found for message with type {message.Header.Type}", nameof(message)),
    },
    groupId: "kafka-TaskReceiverConsole-Sample",
    timeOut: TimeSpan.FromMilliseconds(100),
    offsetDefault: AutoOffsetReset.Earliest,
    commitBatchSize: 5,
    sweepUncommittedOffsetsInterval: TimeSpan.FromMilliseconds(10000),
    messagePumpType: MessagePumpType.Reactor)
```

### Agreement Dispatcher

Brighter now allows you to determine the handler that will be used for a given request dynamically. Whilst we still support the old 1-2-1 mapping, this method can be used for an [Agreement Dispatcher](https://martinfowler.com/eaaDev/AgreementDispatcher.html) where we determine the handler type at runtime not build time.

Note that we do not support auto registration of routes using `AutoFromAssemblies`, you must explicitly add them to the registry. You MUST provide both the mapping function for the agreement dispatcher and a list of possible handler types.

```csharp
registry.RegisterAsync<MyCommand>(((request, context) =>
{
    var myCommand = request as MyCommand;
    if (myCommand?.Value == "first")
        return [typeof(MyImplicitHandlerAsync)];
    
    return [typeof(MyCommandHandlerAsync)];
}), 
    [typeof(MyImplicitHandlerAsync), typeof(MyCommandHandlerAsync)]
);

```

### Request Context Improvements

The CommandProcessor now lets you set the `RequestContext` explicitly when calling `Send`, `Publish`, `DepositPost` etc. This allows you to set properties of the `RequestContext` for transmission to the `RequestHandler` instead of having a new context created by the `RequestContextFactory` for that pipeline.

For consumers, we now add a property to the `RequestContext` that provides the `OriginatingMessage` which allows you to examine properties of the message that was received.

**Breaking Change**: The `IRequestContext` interface has been enhanced to support:

* **Partition Key**: Set message partition keys dynamically 
* **Custom Headers**: Add headers via request context
* **Resilience Context**: Integration with Polly Resilience Pipeline

```csharp
// Set partition key and custom headers via request context
public class MyHandler : RequestHandler<MyCommand>
{
    public override MyCommand Handle(MyCommand command)
    {
        Context.Span.SetAttribute("custom.header", "value");
        Context.PartitionKey = command.TenantId;
        
        return base.Handle(command);
    }
}
```

### Proactor and Reactor

We have made significant changes to Brighter's concurrency models. We now use terminology that derives from the Reactor and Proactor patterns, replacing the previous "blocking" and "non-blocking" terminology with clearer semantic meaning.

* **Reactor Model**: Uses blocking I/O for optimal performance in single-threaded scenarios
* **Proactor Model**: Uses non-blocking I/O for improved throughput when sharing resources across multiple threads

We now have a complete async pipeline for the Proactor and a complete sync pipeline for the Reactor, whereas previously only dispatch was async in the Proactor pipeline. Our synchronization context has been updated to use Stephen Cleary's AsyncEx approach instead of Stephen Toub's original article, providing better error handling and more reliable continuation management.

**Breaking Change**: The `runAsync` flag on Subscription has been renamed to `MessagePumpType` for clarity. Update your subscriptions:

```csharp
// V9
var subscription = new Subscription(typeof(MyHandler), isAsync: true);

// V10
var subscription = new Subscription(typeof(MyHandler), messagePumpType: MessagePumpType.Proactor);
```

### Scheduled Requests/Messaging

The CommandProcessor now supports using a scheduler to delay sending, publishing or posting messages. We support a range of schedulers, such as Quartz.NET, Hangfire and AWS Scheduler.

```csharp

 _commandProcessor.Send(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);

```

```csharp
var schedulerFactory = SchedulerBuilder.Create(new NameValueCollection())
    .UseDefaultThreadPool(x => x.MaxConcurrency = 5)
    .UseJobFactory<BrighterResolver>()
    .Build();

var scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();
scheduler.Start().GetAwaiter().GetResult();

_scheduler = new QuartzSchedulerFactory(scheduler);

```

### InMemory Options

Brighter has a range of in-memory types that can replace key dependencies such as producers, consumers, schedulers, outboxes and inboxes. Whilst we do not recommend this for production usage, they are robust and can be used for local development and testing.

```csharp
UseScheduler(new InMemorySchedulerFactory())

```

### Nullable Reference Types

**Breaking Change**: Nullable reference types are now enabled across all projects. You may need to update your code to handle nullable warnings:


### Simplified configuration

**Breaking Change**: Builder methods have been renamed for clarity. We used names that historically had value, but are no longer meaningful to most users of Brighter, so we have reverted to a simpler naming convention:

```csharp
// V9
services.AddBrighter()
    .UseExternalBus(...)
    .AddServiceActivator(...);

// V10  
services.AddBrighter()
    .AddProducers(...)
    .AddConsumers(...);
```

**Connection Provider Registration**: Improved registration of connection and transaction provider interfaces

### Polly Resilience Pipeline

**Breaking Change**: New resilience pipeline attributes replace legacy timeout policies 

```csharp
// V9 - Deprecated
[TimeoutPolicy(milliseconds: 5000, step: 1)]
public override MyResult Handle(MyCommand command) { }

// V10 - New approach
[UseResiliencePipeline(policy: "MyPipeline", step: 1)]
public override MyResult Handle(MyCommand command) { }
```

The `TimeoutPolicyAttribute` is now marked as obsolete.

The new approach provides:

* **Full Polly v8 Support**: Access to all Polly resilience strategies
* **CancellationToken Integration**: Proper cancellation token flow from resilience pipelines 
* **Enhanced Context**: Request context integration with Polly's resilience context

### AWS SDK v4 Support

Complete AWS SDK v4 support has been added:

* **SNS/SQS**: Standard and FIFO queue support
* **DynamoDB**: Inbox, Outbox, and Distributed Lock implementations  
* **S3**: Luggage store for claim check pattern

You can now use the latest AWS SDK v4 while maintaining backwards compatibility with v3.

### Transport Improvements

**PostgreSQL Message Broker**: Added support for using PostgreSQL as a message broker ([PR #3612](https://github.com/BrighterCommand/Brighter/pull/3612)), enabling pub/sub messaging patterns directly with PostgreSQL's LISTEN/NOTIFY functionality.

**RabbitMQ Enhancements**:

* **Quorum Queues**: Support for RabbitMQ quorum queues for improved consistency and availability.
* **RabbitMQ 7.x**: We have support for the older RabbitMQ v6 to support synchronous RMQ pipelines and support for the asynchronous pipelines of RabbitMQ client library v7
* **Connection Stability**: Improved connection handling and error recovery.

```csharp
// Configure Quorum queues
var subscription = new RmqSubscription<MyMessage>(
    queueType: QueueType.Quorum,
    isDurable: true,         // Required for quorum queues
    highAvailability: false  // Must be false for quorum queues
);
```

**Kafka Improvements**:

* **Configuration Callback**: Enhanced configuration support through KafkaSubscription callback 
* **Updated Defaults**: Improved default configuration values for better out-of-the-box experience

**AWS Improvements**:

* **SQS Publication Enhancement**: Allow publishing directly to an SQS queue without SNS
* **S3 Claim-Check**: Fixed AWS S3 claim-check implementation

### Sweeper Circuit Breaking

Topic-level circuit breaking has been added to prevent cascade failures:

* **Failure Tracking**: Automatic tracking of dispatch failures per topic
* **Configurable Thresholds**: Set failure thresholds and cooldown periods  
* **Automatic Recovery**: Topics automatically recover after cooldown period
* **Bulk Dispatch Support**: Circuit breaking now properly supports bulk dispatch operations 
* **Per-Transport Integration**: Circuit breaking is integrated with MongoDB 

The bulk dispatch implementation brings circuit breaking inline with single dispatch, allowing individual batches to be retried and providing better control over transport-specific batching behavior.

### Performance Improvements

* **GUID v7**: Support for GUID v7 on .NET 9+ for better database performance
* **Sealed Classes**: Internal classes sealed to reduce virtual dispatch overhead
* **Optimized Collections**: Reduced dictionary lookups and improved collection usage
* **Memory Optimization**: Better memory usage in SQL data readers and stream handling 
* **Source-Generated Logging**: Migrated to source-generated logging for superior performance and stronger typing 
* **Reduced Allocations**: Optimized string comparisons and reduced unnecessary allocations 

GUID v7 provides better database clustering and performance characteristics compared to GUID v4, especially beneficial for high-throughput scenarios with database-backed outboxes and inboxes.

### Test Infrastructure and Developer Experience

**Enhanced Testing**:

* **Colorful Test Output**: Improved test runner with colorful output and GitHub Actions logger support 
* **Better Test Infrastructure**: Enhanced test reliability and coverage across all transport implementations


### Command Processor Dispatching Strategy

Enhanced command processor with support for content-based routing using specification patterns ([PR #3652](https://github.com/BrighterCommand/Brighter/pull/3652)). This enables routing requests based on content rather than just type, supporting more sophisticated message routing scenarios.

### Additional Bug Fixes and Improvements

* **Outbox Sweeper**: Fixed NullReference exception in outbox sweeper ([PR #3683](https://github.com/BrighterCommand/Brighter/pull/3683))
* **ASB Defer Exception**: Fixed issue where Azure Service Bus defer exception caused attempted reject then complete ([PR #3619](https://github.com/BrighterCommand/Brighter/pull/3619))
* **Scheduler Tests**: Fixed scheduler tests for long scheduling windows with proper EntryTimeToLive configuration ([PR #3582](https://github.com/BrighterCommand/Brighter/pull/3582))
* **Quorum Queue Tests**: Enhanced quorum queue testing to properly validate queue creation ([PR #3642](https://github.com/BrighterCommand/Brighter/pull/3642))

### Breaking Changes Summary

For users upgrading from V9 to V10:

1. **Update Subscription Configuration**:
   * Replace `isAsync/runAsync` with `messagePumpType` with options of `MessagePumpType.Proactor` or `MessagePumpType.Reactor`
   * Replace `timeoutInMilliseconds` with `timeOut` which is now a `TimeSpan` type
   * Replace `requeueDelayInMs` with `requeueDelay` which is now a `TimeSpan` type

2. **Handle Nullable Reference Types**:
   * Address nullable warnings in your handlers and commands

3. **Update Builder Calls**:
   * Replace messaging builder methods with `AddProducers()`/`AddConsumers()`

4. **Migrate Policies**:
   * Replace `[TimeoutPolicy]` with `[UseResiliencePipeline]` and Polly configuration ([TimeoutPolicy is deprecated in V10 and will be removed in V11])
   * Replace `[UsePolicy]` with `[UseResiliencePipeline]`

5. **Message ID Changes**:
   * Message and Correlation IDs are now strings (defaulting to GUID strings)

6. **Generic Message Pumps**:
   * Remove generic type parameters if directly instantiating message pumps

7. **Test Framework Changes**:
   * Replace Fluent Assertions with xUnit assertions in your test projects

8. **Default Message Mappers**:
   * Review your message mappers - many can now be removed in favor of default implementations

### Database Schema Updates

If you use Inbox/Outbox patterns, you may need to update your database schemas. New DDL scripts are available in the repository for each supported database provider.

### Migration Guide

For detailed migration guidance, see the [V10 Migration Guide](https://brightercommand.github.io/Brighter/migration/v10) in our documentation.

## Release 9.X

## Binary Serialization Fixes

* MessageBody  nows store the character encoding type (defaults to UTF8) to allow correct conversion back to a string when using Value property
* Use a CharacterEncoding.Raw for binary content (will be a Base64 string for Value)
* Kafka transport payload is now byte[] and not string. This prevents corruption of Kafka 'header' of 5 bytes to store schema registry when used with schema registry support
* DynamoDb now uses a byte[] and not a string for the message body to prevent lossy conversions
* ContentType on Header is set from Body, if not set on the Header

## Kafka Fixes

* Kafka now serliases the ReplyTo Header correctly

## New Transforms

* Compression Transform now available to compress messages using Gzip (or Brotli or Deflate on .NET 6 or 7)

## Release 9.3.6

* Set correct partition key (kafka key) for Kafka messages  
* Add default option for Header bags serialisation
* Set correct span status for Send and SendAsync @easyfy-fredrik
* Note that this version pulls v7 of System.Text.Json which has breaking changes for users of System.Text.Json, see <https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-7/#breaking-changes>

## Release 9.3.0

* Bug with DynamoDb Outbox and the Outbox Sweeper fixed. The Sweeper required a topic argument supplied by a dictionary of args
  * Required adding a Dictionary<string, object> to various interfaces, which defaults to null, hence the minor version bump as these interfaces have new capabilities
* Internal change to move outstanding message box to a semaphore slim over a mutex as thread-safe. Not strictly neededm, but follows our policy of moving to semaphore slim
* Changes to the DynamoDb Outbox implementation as Outstanding Message check was not behaving as expected
* The interfaces around Outbox configuration will likely change in v10 to avoid current split and need to configure on both publication and outbox

## Release 9.1.20

- Bug with Kafka Consumer failing to commit offsets fixed. Caused by Monitor being used for a lock on one thread and released on another, which does not work. Replaced with SemaphoreSlim.
* Behavior of Kafka Consumer offset sweep changed. It now runs every x seconds, and not every x seconds since a flush. This will cause it to run more frequently, but it is easier to reason about.

## Release 9.1.14

* Fixed missing negation operator when checking for AWS resources

## Release 9.1.14

* Renamed MessageStore to Outbox and CommandStore to Inbox for clarity with well-known pattern names outside this team
  * Impact is wide, namespaces, class names and project names, so this is a ***BREAKING CHANGE***
  * Mostly you can search and replace to fix
* Added support for a global inbox via a UseInbox configuration parameter to the Command Processor
  * Will insert an Inbox in all pipelines
  * Can be overridden by a NoGlobalInbox attribute for don't add to pipeline, or an alternative UseInbox attribute to vary config
* The goal here is to be clearer than our own internal names, which don't help folks who were not part of this team
* The Outbox now fills up if a producer fails to send. You can set an upper limit on your producer, which is the maximum outstanding messages that you want in the Outbox before we throw an exception. This is not the same as Outbox size limits or sweeper, which is separate and mainly intended if you don't want the Outbox limit to fail-fast on hitting a limit but keep accumulating results  
* Added caching of attributes on target handlers in the pipeline build step
  * This means we don't do reflection every time we build the pipeline for a request
  * We do still always call the handler factory to instantiate as we don't own handler lifetime, implementer does
  * We added a method to clear the pipeline cache, particularly for testing where you want to test configuration scenarios
* Added ability to persist RabbitMQ messages
* Added subscription to blocked/unblocked RMQ channel events. A warning log is created when a channel becomes blocked and an info log is generated when the channel becomes unblocked.
* Improved the Kafka Client. It now uses the publisher/creator model to ensure that a message is in Brighter format i.e. headers as well as body; updated configuration values; generally improved reliability. This is a breaking change with previous versions of the Kafka client.
* The class BrighterMessaging now only has a default constructor and now has setters on properties. Use the initializer syntax instead - new BrighterMessage{} to avoid having redundant constructor arguments.
* Changes to how we configure transports - renaming classes and extending their functionality
  * Connection is renamed to Subscription
  * Added a matching Publication for producers
  * Base class includes the attributes that Brighter Core (Brighter & ServiceActivator) need
  * Derived classes contain transport specific details
  * On SQSConnection, renamed VisibilityTimeout to LockTimeout to more generically describe its purpose separated from GatewayConfiguration, that now has a marker interface, used to connect to the Gateway and not about how we publish or subscribe
  * We now have the option to declare infrastructure separately and Validate or Assume it exists, still have an option to Create which is the default
  * We think it will be most useful for environments like AWS where there is a price to checking (HTTP call, and often looping through results)  
  * Added support for a range of parameters that we did not have before such as dead letter queues, security etc via these platform specific configuration files  
* Provided a short form of the BrighterMessaging constructor, that queries object provided for async versions of interfaces
* Changed IsAsync to RunAsync on a Subscription for clarity
* Supports an async pipeline: callbacks should happen on the same thread as the handler (and the pump), avoiding thread pool threads
* Fixed issue in SQlite with SQL to mark a message as dispatched

## Release 8.1.1399

* Update nuget libs
* RabbitMQ 6.*
* Fix correlationid no been sent correctly when using SqlCommandStore

## Release 8.1.1036

* Fixes issue when a rabbitmq connection is dropped it sometimes ends up with 2 connections and then does not dispose the ghost connection.
* Fix for System.InvalidOperationException: You cannot enqueue more items than the buffer length #846
* fix for Suppress and log BrokerUnreachableException during ResetConnection #502

## Release 8.0.*

* Added SourceLink debugging and are shipping .pdb files in the nuget package.
* Strong Name in line with Open Source guidance <https://docs.microsoft.com/en-us/dotnet/standard/library-guidance/strong-naming>. Where libraries we rely on are not strong named we don't strong name our code.
* Removed `IAmAPolicyRegistry` and replaced it with `IPolicyRegistry<string>` from Polly, it is a drop in replacement but in a the Polly namespace.
* Removed our `PolicyRegistry` and now use the `PolicyRegistry` from Polly, it is a drop in replacement but in a the Polly namespace.
* Support for Feature Switches on handlers
* Switch Command Sourcing Handler to using an Exists method when checking for duplicate messages
* Rewritten AWS SQS + SNS transport
* Support for DynamoDB Message and Command Stores (Jonny Olliff-Lee @DevJonny)
* Added a Call() method to CommandProcessor to support Request-Reply
* Add a context field to the command store, to allow identification of a context, and share a table across multiple handlers. Note that this is a breaking schema change for users of the command store
* Command Sourcing handler now writes to store only once the handler has successfully completed
* Renamed InputChannelFactory to ChannelFactory as we don't have an OutputChannelFactory any more (and not for some time)
* Channel buffer now only source for message pump, populated via consumer when empty
* Consumers now return an array of messages, default size of 1 but can be up to 10
* Switch RMQ Consumers back to basic consume to support batch delivery
* RMQ now supports batch sizes of up to 10 for consuming messages
* SNS+SQS now supports batch sizes of up to 10 for consuming messages
* Added support for the Outbox pattern via DepositPost and ClearPostBox
* Fixed <https://github.com/BrighterCommand/Brighter/issues/156> to allow different exchange types to be set (was broken by support of delayed exchange)
  
## Release 7.4.0

* Updated to signed version of Polly, works with netcore2.1.
* Fix for Sql CommandStore.
* Fixes to make flaky tests stable.
  
## Release 7.3.0

* Added beta Support for a Redis transport
* Support for Binding a channel to multiple topics
* RMQ Transport: Fixed handling of socket timeout where node we are connected to (not master) partitions from cluster and is paused under the pause minority strategy. Now resets connection successfully.
* RMQ Transport: Fixed issue with OperationInterrupted exception when master node partitions and we are connected to it
* Overall improved reliability of Brighter RMQ transport when connecting to a cluster that experiences a partition
* Fixed an issue where multiple performers did not have distinct names and so could not be tracked
* RMQ changed from push rabbit consumer to just simple pull based.

## Release 7.2.0

* Support for PostgreSql Message Store (Tarun Pothulapati @Pothulapati)
* Support for MySql Message and Command Stores (Derek Comartin @dcomartin)
* Support for Kafka Messaging Gateway - Beta (Wayne Hunsley @whunsley)
* Support for MSSql Messaging Gateway - Beta (Fred Hoogduin @Red-F)

## Release 7.1.0

* Fixes issue with high CPU when failing to connect to RabbitMQ.
* Fixes missing High Availability setting, had to make changes to IAmAChannelFactory.

## Release 7.0.137 - 7.0.143

* Support for .NET Core (NETSTANDARD 1.5)

### **Breaking Changes**

* Configuration no longer supports XML based config sections. We use data structures instead, and expect you to configure mostly in code, initializing those data structures from your config system of choice yourself. We recommend following 12-Factor Apps guidelines and preferring environment variables for items that vary by environment over XML or JSON based configuration files. (We may consider providing config sections in Contrib again, please feedback if this is a critical issue for you. PRs welcome.)
* Dropped CommandProcessor from namespaces and folder names, to shorten, and remove semantic issue that it is not just a Command Processor
* Changed namespaces and folders to be CamelCase
* As a result, your using statements will need revision with this release
* Some namespaces i.e Paramore.Brighter.Policy changed to avoid clashes now CamelCase (has become Paramore.Brighter.Policies)

## Release 6.1.0

* Support for binary message payloads i.e. not just text/plain for JSON or XML. Current support is modelled around use of protobuf over RMQ

## Release 6.0.28

Fix issue with encoding of non-string types and transmission of correlation id <https://github.com/BrighterCommand/Brighter/pull/180>

## Release 6.0.6

- Increase logging level when we stop reading from a queue that cannot be readhttps://github.com/BrighterCommand/Brighter/pull/179
* Peformance issue caused by creation of a logger per requesthandler instance. The logger is now static, but is initialized lazily and can be overridden for TDD or legacy compatibility

## Release 6.0.0

**Breaking Changes**
* CommandProcessorBuilder no longer takes .Logger(logger)
* In the abstract RequestHandler `logger` is now `Logger`
* `RequestLogging` has moved namespace to `paramore.brighter.commandprocessor.logging.Attributes`

### **Bug fixes**:

* Fixed issue #132: concurrent usages of the RabbitMQ messaging gateway would sometimes throw an exception
* Fixed issue #134: We no longer use async/await in the command processor. This caused issues with ASP.NET synchronization contexts, resulting in a deadlock when waiting on the thread that was also being used to run the completion. See <http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html> We wil revisit async when we write *Async versions of the CommandProcesor APIs suitable for using in hosts that can run async code without deadlocking their synchronization context.
* Fixed issue 110: Where we want to log we have two constructors. A constructor that directly takes an iLog that you provide either directly or via your ioC container; a constructor that defaults that to LogProvider.GetCurrentClassLogger
 	* In Production code you should set up your log provider and use the constructors that do not take an ILog reference.
 	* In Test code you should inject the ILog using a fake logger. We don't recommend testing log output, its an implementation detail, unless its an important part of your acceptance criteria for that behavior.
 	* This means that your production code should not need to take a direct dependency on Paramore's ILog implementation.
 	* This is a BREAKING CHANGE because we remove the ability to inject the constructor via the *Builder objects, so as to remove the temptation to do that when you should rely on the LibLog framework to wrap your current logger.

### **Features:**
* Huge feature, Async; added support for SendAsync and PublishAsync to an IHandleRequestsAsync pipeline.
* Basic support for publishing to Azure Service Bus with `paramore.brighter.commandprocessor.messaginggateway.azureservicebus`.

## Release 5

### **Bug Fixes:**

* #100 `CommandProcessor.Post` fails with Object reference not set to an instance of an object.
* Fix RequeueMessage exhaustion to log ERROR.
* #101 Updated `Requeue` method to send a message to a specific queue as opposed to a topic.
* Added a message store write timeout and message gateway timeout on a post; perviously we wait indefinitely (bad Brighter team, no biscuit).
* Replace `Successor` write-only property with `SetSuccessor` method.
* Message Viewer, fixed startup issues.
* Removed a few unused interfaces.
* Correct exceptions namespace to actions.

### **Features:**

* A connection can now be flagged as isDurable in the configuration. Choosing isDurable when using RMQ as the broker will create a durable channel (i.e. does not die if no one is consuming it, and thus continues to subscribe to messages that match it's topic). We think there are sufficient trade-offs with a message store that allows replay to make this setting false by default, but have configured to allow users to make this choice dependent on the characteristics of their consumers (i.e. sufficiently intermittent that messages would be lost).
* #92 Added [Event Store](https://geteventstore.com/ "Event Store") Message Store implementation
* #30 Changed RabbitMQ Messaging Gateway to support multiple performers per connection, fixing the pipeline errors from RabbitMQ Client
* Added a UseCommandSourcing attribute that stores commands to a command store. This is the Event Sourcing paradigm described by Martin Fowler in <http://martinfowler.com/eaaDev/EventSourcing.html> The term Command Sourcing refers to the fact that as described the pattern stores commands (instructions to change state) not events (the results of applying those commands).
 	* This may result in a breaking change that the Id on IRequest requires a setter to allow it to be deserialized
* Added MS SQL Command Store implementation
* Added monitoring attribute, which fires message onto control bus
* Cleaning up code so working with dnx and Portable will be easier
* Message Viewer, Add paging
* Update Code of Conduct to Contributor Covenant 1.1.0
* Add DDL scripts to help create SQL based schemes

**Remove and Depreciated:**

* Flag the method `Repost` on `IAmACommandProcessor` as obsolete, We will probably drop this in the next release. We suggest that you use the message store directly to retrieve a message and then call Post.
* Dropped support for RavenDb as a message store, we feel EventStore covers this scenario better where non-relational stores are an option
* Removed release branch. We just tag a release on master now, so this only existed to support an older version of the library that was pre the tagging strategy. Removed now as confusing to new users of the library.

## Release 4.0.215

1. Fixed an issue where you could not have multiple UsePolicy or FallbackPolicy attributes on a single handler.#
2. We pool connections now, to prevent clients with large number of channels overwhelming servers.
3. Add concept of delayed (deferred) message sending.
4. Implement delayed requeuing using gateway support (when supported).
5. Delayed message provider support for RabbitMQ using [rabbitmq_delayed_message_exchange plugin (3.5+)](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/).
6. Renamed RequeueException to DeferMessageAction and moved it into the command processor project.
7. Fixed and issues with unhandled exceptions from handlers when an event is published not been logged correctly
8. The first early version of a Message Store Viewer has been release as a zip file download

## Release 3.0.129

1. We now support a Fallback method on IHandleRequests<TRequest> which is intended to be used for compensating or emergency action when a Handle method cannot be executed. The [FallbackPolicy] attribute supports the pipeline calling the Fallback method for you, in the event of either any exception bubbling into the handler, or a broken circuit exception bubbling into the handler.
2. Fix issue with RabbitMQ consumers running on a High Availability cluster not cancelling properly after cluster failover.
3. Fixed bug with config section duplication <https://github.com/BrighterCommand/Brighter/issues/52>
4. Added functionality so after a specified number of unacceptable message (unable to read from queue or map message) a connection is shutdown, by default unacceptable message are acked and dropped. <https://github.com/BrighterCommand/Brighter/issues/51>
5. Move RequeueException to paramore.brighter.commandprocessor.exceptions (breaking change).

## Release 3

1. Refactored **IAmAMessagingGateway** into a **IAmAMessageConsumer** and **IAmAMessageProducer** to support differing approaches to producing and consuming messages for a particular flavour of Message-Oriented-Middleware. *These changes are a breaking binary change for users of earlier versions.*
 1. NOTE: IF YOU USE TASK QUEUES PLEASE SAVE YOUR SERVICEACTIVATORCONNECTIONS IN YOUR APP.CONFIG AS THE V2.0.1 BRIGHTER.SERVICEACTIVATOR UNINSTALL WILL DELETE THEM (FIXED FOR V3)
2. Created an **IAmAChannel** abstraction to allow differing Application Layer dependencies for the Work Queue
2. Upgraded Packages we depend on, including RabbitMQ. *There is still an issue with our having a hard dependency on a RabbitMQ client that might vary from your RabbitMQ client version, but as a NuGet package there are few workarounds. We suggest building from source where this issue is problematic, for now.*
3. Significant stability improvements on the RabbitMQ client
 1. Fixed issues around re-connection of the client leading to lost messages.
 2. Fixed issues when explicitly closing and re-opening connections
 3. Provided support for a **RequeueException** to requeue messages that are 'out-of-time' to help with resequencing.
 1. We now dispose of channels aggressively on closure, instead of waiting for garbage collection
2. Moved from [Common.Logging](https://github.com/net-commons/common-logging) to [LibLog](https://github.com/damianh/LibLog)  *These changes are a breaking binary change for users of earlier versions.*
3. We now call Release for all **RequestHandler<>** derived handlers that we construct from an **IAmAHandlerFactory**, not just those that implement IDisposable.
4. Note that the RestMS server is **NOT** ready for production usage. It's primary value, as of today, is an alternative to RabbitMQ for design purposes. It is hoped to produce a stable version for use as a ControlBus in a future release.

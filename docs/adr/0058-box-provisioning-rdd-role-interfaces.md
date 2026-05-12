# 0058. Box Provisioning RDD Role Interfaces and Template-Method Runner

Date: 2026-05-07
Amendment Date: 2026-05-12 (sub-phase A appended — §B.5 + §B.4 row 5 + "Adding a new backend" step 6 amended)

## Status

Accepted

> **Approval state**: §A / §B.1–§B.4 (rows 1–4) / "Adding a new BoxProvisioning backend" (pre-amendment) were Accepted 2026-05-07. Sub-phase A appendix (§B.5, §B.4 row 5, step 6 amendment) was Accepted 2026-05-12 after a 4-round adversarial review (round 4 PASS, 0 findings ≥60); see `specs/0028-box-provisioning-rdd-role-interfaces/review-design.md` for the round-4 record. The whole ADR is now Accepted.

## Context

**Parent Requirement**: [specs/0028-box-provisioning-rdd-role-interfaces/requirements.md](../../specs/0028-box-provisioning-rdd-role-interfaces/requirements.md)

**Scope**: This ADR is the single design document for spec 0028. It addresses two related architectural concerns in one document because they share the same goal — surfacing RDD roles across the BoxProvisioning packages — and a single decision narrative reads more clearly than two parallel ones:

- **§A — Role-based instance interfaces** for the per-backend BoxProvisioning surfaces introduced by spec 0027. Addresses requirements F2 (detection helper role), F3 (migration factory role), F4 (payload-mode validator role), and F8 (documentation deliverable). The DI-extension and provisioner-sub-role concerns from the originating review are out of scope per the requirements.
- **§B — Unit-of-work role and template-method runner abstract base class** for the relational migration runners introduced by spec 0027. Addresses requirements F5 (provisioning UoW role), F6 (abstract base across the four relational backends), F7 (harmonised UoW lifecycle and cancellation contract), and F9 (open-closed sweep across the rest of the BoxProvisioning surface).

**Related ADRs**:
- [ADR 0057 Box Schema Versioning and Migrations](0057-box-schema-versioning-and-migrations.md) — defines the per-backend runners, the degenerate-Spanner exemption, the per-backend advisory-lock abstractions (Items D / M / N), and the MySQL transaction-less DDL model (§5a).
- [ADR 0053 Box Database Migration](0053-box-database-migration.md) — predecessor; defines `IAmABoxProvisioner` and the BoxProvisioning DI shape.

### The architectural problem

Spec 0027 ships several families of per-backend classes that fulfill the same role across the five supported backends (MSSQL, Postgres, MySQL, SQLite, Spanner) but lack a role-based interface unifying them. The four relational migration runner classes additionally implement substantially-similar try/catch/finally + lock + transaction lifecycle code. Today's design works correctly — but it falls short of the project's stated Responsibility-Driven Design (RDD) discipline (per `.agent_instructions/design_principles.md`) in the following concrete ways:

1. **Detection helpers are static classes that lack a role interface.** Five `*BoxDetectionHelpers` classes, each with the same five universal methods plus (on the relational four) a sixth `DetectCurrentVersionAsync`. A contributor surveying the BoxProvisioning packages would have to deduce by side-by-side comparison that these fulfil the same role, and that "the role" is "I know how to interrogate a backend's schema state to plan a migration".
2. **Migration factories share an identical signature with zero divergence** but no interface naming the role. Eight relational factories (4 backends × outbox/inbox) each with `static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration)`. Spanner is exempt by design (ADR 0057 §6).
3. **Payload-mode validators are static classes with the same intent but no interface.** Five `*PayloadModeValidator` classes; `string schemaName` taken (and used) on MSSQL/PG/MySQL, no schema parameter at all on SQLite/Spanner; connection type per backend.

In addition:

4. **The four relational migration runners share ~70% of their algorithm** — try/catch/finally, advisory-lock acquire/release (or SQLite's writer slot), transaction begin/commit/rollback (where applicable), history-table ensure, fresh / bootstrap / normal path dispatch, logging on lock-release anomalies — but each backend's runner is a free-standing class. Future cross-backend changes (e.g. a new logging convention, a new disposal contract) must be made N times in N places.

5. **The lock+transaction pairing varies per backend in ways that have leaked into runner code**. MSSQL's lock requires the transaction (acquire AFTER `BeginTransaction` because `@LockOwner='Transaction'` releases on commit); PG acquires lock BEFORE the (optional) transaction (`pg_advisory_lock` is session-scoped); MySQL acquires lock with no transaction (DDL auto-commits); SQLite folds lock+tx into one operation (`BEGIN IMMEDIATE` reserves the writer slot). Today this variation is encoded as ordering convention inside each runner — there is no role for "the atomic scope of a migration on backend X".

### The forces at play

- **RDD discipline** (per `.agent_instructions/design_principles.md`): role-based interfaces named `IAmA*`; responsibilities of "knowing", "doing", and "deciding" allocated to roles; objects have *roles* not just data; preserve flexibility through interface abstraction. **Object-oriented over procedural** — behaviour belongs on instances that can hold related state, not on free-standing static methods.
- **Spec 0027's surface has not yet shipped** — the entire BoxProvisioning family is still on `database_migration` awaiting PR #4039 merge. Source-breaking the spec 0027 surface during this ADR is permissible (no released configuration breaks).
- **`IAmA*` naming convention** is documented and consistent across Brighter (`IAmAProducerRegistry`, `IAmABoxProvisioner`, `IAmABoxMigrationRunner`, `IAmABoxMigration`, `IAmARelationalDatabaseConfiguration`).
- **Multi-targeting matters**: the shared assembly `Paramore.Brighter.BoxProvisioning` targets `netstandard2.0;net8.0;net9.0;net10.0` (per `src/Directory.Build.props`); the MSSQL package targets `net462;net8.0;net9.0;net10.0`. Any role interface in the shared assembly must compile and run on netstandard2.0; any feature added to MSSQL must compile on net462. This rules out static virtual interface members (a .NET 7+ feature) and `IReadOnlySet<T>` (.NET 5+) in the public role contracts. Existing precedent: `IAmABoxMigration.LogicalColumns` is typed `IReadOnlyCollection<string>` rather than `IReadOnlySet<string>` for exactly this reason.
- **Spanner is intentionally degenerate** per ADR 0057 §6 — fresh-install only, no V_k chain, no advisory lock, no transaction. The role interfaces and the template-method base must accommodate Spanner's degeneracy without contorting the relational design to fit.
- **MySQL is transaction-less** per ADR 0057 §5a — DDL auto-commits per statement; transactions are not used in the migration flow. Any "atomic scope" abstraction in the runner base class must accommodate MySQL's degenerate transaction.

### Why this decision is important

If spec 0028 ships a clean RDD design now, the BoxProvisioning packages become a documented extensibility point. A future Oracle / DB2 / CockroachDB backend implements a list of role interfaces and (optionally) derives from the abstract runner base — guided by ADR 0058 §A.4 "How to add a new backend". If spec 0028 ships shallow or inconsistent role-naming, the design opportunity is lost: the `IAmA*` convention diverges, future contributors deduce from sibling backends, and the next refactor pass becomes harder.

## Decision

This ADR makes **eight load-bearing decisions** across the two sections — four in §A (three new role interfaces plus the documentation deliverable) and four in §B (a new role interface for the unit-of-work pairing, the runner abstract base, the harmonised lifecycle + cancellation contract, and the open-closed sweep result).

---

## §A — Role-based instance interfaces

### A.1 Detection helpers — `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>`

**Decision**: Introduce an instance role interface for detection helpers, generic on **both the connection and transaction types**. Spanner is exempt from a sibling extension interface that adds version-detection.

```csharp
namespace Paramore.Brighter.BoxProvisioning;

public interface IAmABoxMigrationDetectionHelper<TConnection, TTransaction>
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    Task<bool> DoesTableExistAsync(
        TConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);

    Task<bool> DoesHistoryExistAsync(
        TConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);

    Task<int> GetMaxVersionAsync(
        TConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);

    Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
        TConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);

    string DiscriminatorFor(BoxType boxType);
}

public interface IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> :
    IAmABoxMigrationDetectionHelper<TConnection, TTransaction>
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    Task<int> DetectCurrentVersionAsync(
        TConnection connection, string tableName, string? schemaName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);
}
```

**Rationale**:

- **Instance methods, not static virtual** — see Forces at play. The shared assembly targets netstandard2.0; static abstract / static virtual interface members require .NET 7+. Instance interfaces compile and run on every supported TFM.
- **OO over procedural** — instance classes can hold related state (a logger, a configuration reference) and substitute for testing. The original spec 0027 detection helpers were `static class` only because they began as a quick refactor; the role they fulfill is real and benefits from being modelled as a proper service.
- **Two interfaces, not one** — Spanner legitimately lacks `DetectCurrentVersionAsync` (per ADR 0057 §6 fresh-install-only). A single interface forcing all five backends to implement six methods would either (a) require Spanner to throw `NotSupportedException` from a method it has no business advertising, or (b) drop the universal subset to five methods, hiding the fact that the relational four expose more capability. The two-interface split honestly reflects the role boundary.
- **`schemaName` is `string?` everywhere** — backends without schema concept (MySQL has one, SQLite/Spanner do not) accept `null` or empty and ignore it on the impls that don't use it. Justified by the alternative being a separate non-schema interface (ceremony for one method shape) or `string` non-nullable forcing artificial values like `""` (leaky).
- **Transaction parameter on every relational method** — every method (except the pure `DiscriminatorFor`) takes a nullable transaction with a default of `null`. This avoids transaction-bearing overloads on MSSQL/PG/SQLite. The `TTransaction` generic on the interface forces MySQL and Spanner to declare a transaction-typed slot they do not consume — accepted as an honest cost of one uniform shape that the runner base class can depend on without per-backend overload sprawl. Backends that do not use the transaction document the parameter as ignored on the implementing class's XML-doc.
- **`GetTableColumnsAsync` returns `IReadOnlyCollection<string>`** (not `IReadOnlySet<string>`) for symmetry with `IAmABoxMigration.LogicalColumns` (`src/Paramore.Brighter.BoxProvisioning/IAmABoxMigration.cs`) and because `IReadOnlySet<T>` is not available on netstandard2.0.

**Backend-by-backend assignments**:

| Backend  | Class becomes                                  | Implements                                                                                                                                              |
|----------|------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------|
| MSSQL    | `public class MsSqlBoxDetectionHelper`         | `IAmAVersionDetectingMigrationHelper<SqlConnection, SqlTransaction>`                                                                                    |
| Postgres | `public class PostgreSqlBoxDetectionHelper`    | `IAmAVersionDetectingMigrationHelper<NpgsqlConnection, NpgsqlTransaction>`                                                                              |
| MySQL    | `public class MySqlBoxDetectionHelper`         | `IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction>` (transaction parameter accepted but ignored — see XML-doc)                     |
| SQLite   | `public class SqliteBoxDetectionHelper`        | `IAmAVersionDetectingMigrationHelper<SqliteConnection, SqliteTransaction>`                                                                              |
| Spanner  | `public class SpannerBoxDetectionHelper`       | `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` — base only, no `DetectCurrentVersionAsync`; transaction parameter accepted but ignored per ADR 0057 §6 |

**Source-breaks introduced by this conversion** (per NF1; enumerated in `release_notes.md`):
- All five backends: `static class {Backend}BoxDetectionHelpers` becomes `public class {Backend}BoxDetectionHelper` (singular). Existing call-sites must construct an instance (or receive it via DI) instead of calling the static methods.
- `SpannerBoxDetectionHelpers` was `internal`; the new instance class is `public`.
- MSSQL/PG/MySQL methods: `string schemaName` widens to `string? schemaName` (existing parameter). Each impl is responsible for null-substitution to its backend default — see "Null-handling for `schemaName`" below.
- SQLite/Spanner methods: gain a `string? schemaName` parameter (was absent entirely on the static helpers). Each impl ignores the parameter; XML-doc states this explicitly.
- All four relational backends: `GetTableColumnsAsync` return type changes from `HashSet<string>` to `IReadOnlyCollection<string>` (looser).
- **Provisioner ctor cascade** — all ten existing provisioner classes (`{Backend}{Box}Provisioner` × 4 relational backends × 2 box-types, plus the Spanner pair) gain three new ctor parameters reflecting the §A.1, §A.2, and §A.3 conversions:
  - **Detection helper** — typed at `IAmAVersionDetectingMigrationHelper<{Backend}Connection, {Backend}Transaction>` for the relational eight (provisioners call `DetectCurrentVersionAsync` during the bootstrap branch); typed at the base interface `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` for the Spanner pair.
  - **Migration catalogue** — typed at `IAmABoxMigrationCatalog`; one instance per provisioner (Outbox provisioners receive the Outbox catalogue, Inbox provisioners receive the Inbox catalogue). Spanner's pair: omitted per ADR 0057 §6 (Spanner has no migrations).
  - **Payload-mode validator** — typed at `IAmABoxPayloadModeValidator<{Backend}Connection>` (single-generic; no `TTransaction`).
  Existing positional construction at every call-site changes; existing static-method calls (`{Backend}BoxDetectionHelpers.{Method}(...)`, `{Backend}{Box}Migrations.All(config)`, `{Backend}PayloadModeValidator.ValidateAsync(...)`) become instance calls on the injected fields. DI extensions register all three roles as singletons and pass them through — see §A.4 step 6 for the canonical wiring.
- All five backends: every relational method gains an optional `TTransaction? transaction = null` parameter as the LAST positional slot (after `cancellationToken`), matching the existing per-helper convention (`MsSqlBoxDetectionHelpers.cs:42-45`, `SqliteBoxDetectionHelpers.cs:42-45`). Re-ordering rules differ by backend:
  - **MSSQL, Postgres, MySQL** call-sites do NOT need re-ordering. The existing static methods already declare `(connection, tableName, schemaName, cancellationToken, transaction)` — the new instance methods preserve that positional layout (with `string` widened to `string?` for the schemaName slot — see "Null-handling" below). Positional argument lists unchanged.
  - **SQLite, Spanner** call-sites MUST insert an explicit `null` argument. Their existing static methods declare `(connection, tableName, cancellationToken, transaction)` with NO schemaName slot; the new instance methods declare `(connection, tableName, schemaName, cancellationToken, transaction)`. Every existing positional call-site that passed `(connection, tableName, cancellationToken, transaction)` must become `(connection, tableName, null, cancellationToken, transaction)` (or use the implementing class's backend-specific default).

**Null-handling for `schemaName`** (binding contract on every implementation):
- Each impl whose SQL query binds `@SchemaName` (or equivalent — MSSQL, PG, MySQL) MUST substitute the backend's default schema when `schemaName` is null:
  - `MsSqlBoxDetectionHelper` — `schemaName ?? "dbo"`
  - `PostgreSqlBoxDetectionHelper` — `schemaName ?? "public"`
  - `MySqlBoxDetectionHelper` — `schemaName ?? connection.Database`
- Impls whose backend has no schema concept (`SqliteBoxDetectionHelper`, `SpannerBoxDetectionHelper`) accept `schemaName` and ignore it.
- This contract is documented as XML-doc on each method on the role interface (`/// <param name="schemaName">Optional. Null is substituted with the backend default by each implementation — see implementing class for the substitution rule.</param>`) and called out on each implementing class. The runner base passes `schemaName` through verbatim — null-substitution is the helper's responsibility, not the runner's.

### A.2 Migration factories — `IAmABoxMigrationCatalog`

**Decision**: Introduce an instance role interface for the migration-factory family.

```csharp
namespace Paramore.Brighter.BoxProvisioning;

public interface IAmABoxMigrationCatalog
{
    IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration);
}
```

**Rationale**:

- **Cleanest single-method interface in the spec** — eight existing factory classes (`{MsSql,PostgreSql,MySql,Sqlite} × {Outbox,Inbox}Migrations`) have **identical signatures with zero divergence**. The interface is one method, no generic, no schema, no transaction.
- **Naming**: `Catalog` because the role is "I know the catalogue of migrations for THIS backend's THIS box-type". `Factory` was rejected (sounds like "I build individual migrations"). `Chain` was rejected (chain implies linearity, which is correct but less obvious to a contributor surveying the surface). `Set` was rejected (set is unordered).
- **Spanner is exempt** per ADR 0057 §6 — no migration catalogue exists; the runner ignores the migrations parameter. The exemption is documented in §A.4.
- **No generic on box-type** — a `IAmABoxMigrationCatalog<TBox>` adds no value; the impls are one per (backend, box-type), eight classes total, each implementing the simple interface.

**Backend-by-backend assignments**:

| Backend  | `Outbox` impl                                                          | `Inbox` impl                                                          |
|----------|------------------------------------------------------------------------|-----------------------------------------------------------------------|
| MSSQL    | `public class MsSqlOutboxMigrationCatalog : IAmABoxMigrationCatalog`   | `public class MsSqlInboxMigrationCatalog : IAmABoxMigrationCatalog`   |
| Postgres | `public class PostgreSqlOutboxMigrationCatalog : IAmABoxMigrationCatalog` | `public class PostgreSqlInboxMigrationCatalog : IAmABoxMigrationCatalog` |
| MySQL    | `public class MySqlOutboxMigrationCatalog : IAmABoxMigrationCatalog`   | `public class MySqlInboxMigrationCatalog : IAmABoxMigrationCatalog`   |
| SQLite   | `public class SqliteOutboxMigrationCatalog : IAmABoxMigrationCatalog`  | `public class SqliteInboxMigrationCatalog : IAmABoxMigrationCatalog`  |
| Spanner  | (exempt per ADR 0057 §6)                                               | (exempt per ADR 0057 §6)                                              |

**Source-break**: existing classes are renamed from `{Backend}{Box}Migrations` (static) to `{Backend}{Box}MigrationCatalog` (instance). Call-sites change from `MsSqlOutboxMigrations.All(config)` to `new MsSqlOutboxMigrationCatalog().All(config)` (or receive via DI). Per NF1.

### A.3 Payload-mode validators — `IAmABoxPayloadModeValidator<TConnection>`

**Decision**: Introduce an instance role interface, generic on the connection type. Single method.

```csharp
namespace Paramore.Brighter.BoxProvisioning;

public interface IAmABoxPayloadModeValidator<TConnection>
    where TConnection : DbConnection
{
    Task ValidateAsync(
        TConnection connection,
        string tableName,
        string? schemaName,
        string columnName,
        bool binaryMessagePayload,
        CancellationToken cancellationToken = default);
}
```

**Rationale**:

- **Mirrors `IAmABoxMigrationDetectionHelper` in spirit** — instance interface, generic on connection, schema is `string?`. Differs in that the validator is single-generic (no `TTransaction`): validation is a read-only check of column type metadata, independent of any in-flight migration transaction. If a future caller needs to validate inside a transaction scope, a `TTransaction` parameter can be added without changing the role's intent.
- **Naming**: `IAmABoxPayloadModeValidator` — the role is "I decide whether the payload column type matches the configured payload mode" (decider per RDD stereotypes). Aligns with requirement F4 (payload-mode validator role).

**Backend-by-backend assignments**:

| Backend  | Class becomes                                | Implements                                       |
|----------|----------------------------------------------|--------------------------------------------------|
| MSSQL    | `public class MsSqlPayloadModeValidator`     | `IAmABoxPayloadModeValidator<SqlConnection>`     |
| Postgres | `public class PostgreSqlPayloadModeValidator`| `IAmABoxPayloadModeValidator<NpgsqlConnection>`  |
| MySQL    | `public class MySqlPayloadModeValidator`     | `IAmABoxPayloadModeValidator<MySqlConnection>`   |
| SQLite   | `public class SqlitePayloadModeValidator`    | `IAmABoxPayloadModeValidator<SqliteConnection>`  |
| Spanner  | `public class SpannerPayloadModeValidator`   | `IAmABoxPayloadModeValidator<SpannerConnection>` |

Spanner is **not** exempt: although Spanner's payload column type is fixed (binary), the existing `SpannerPayloadModeValidator.ValidateAsync` method already has the same shape — it checks `STARTS_WITH("BYTES" / "STRING")` rather than `bytea`/`text`. Implementing the role contract is a one-line declaration change plus the schema-parameter addition.

**Source-breaks** (per NF1; enumerated in `release_notes.md`):
- All five backends: `static class` becomes `public class` (instance). Call-sites construct or DI-receive.
- MSSQL, Postgres, MySQL: `string schemaName` widens to `string?` (existing parameter — these three backends actively use it in the SQL query). Each impl substitutes the backend default (MSSQL → `"dbo"`, PG → `"public"`, MySQL → `connection.Database`) when null is passed, matching the §A.1 detection-helper null-handling contract. Existing call-sites do NOT need re-ordering — these backends already declare a `string schemaName` slot in the same position; the widening to `string?` is positional-compatible.
- SQLite, Spanner: gain a `string? schemaName` parameter inserted between `tableName` and `columnName`. Every existing positional call-site that passed `(connection, tableName, columnName, binaryMessagePayload, cancellationToken)` must become `(connection, tableName, null, columnName, binaryMessagePayload, cancellationToken)` (or pass the implementing class's backend-specific default — see §A.1 null-handling). Each impl ignores the parameter; XML-doc states this explicitly.

### A.4 Documentation — "How to add a new BoxProvisioning backend"

**Decision**: Add a section to this ADR (below the Decision section) titled "Adding a new BoxProvisioning backend" listing the role interfaces and the optional abstract base. This document becomes the canonical entry point for contributors.

The section enumerates the new role interfaces introduced by this spec, plus references to the existing infrastructure (provisioner interface from ADR 0053, advisory-lock interfaces from ADR 0057) and the conventions a new backend follows. See the "Adding a new BoxProvisioning backend" section below for the full checklist.

This satisfies requirement F8 (documentation deliverable).

---

## §B — Unit-of-work role and template-method runner

### B.1 `IAmAProvisioningUnitOfWork<TTransaction>` — the lock+transaction pairing as a role

**Decision**: Introduce a new role interface that encapsulates the per-backend pairing of advisory lock and transaction (where present) for a single migration run. Each relational backend ships an implementing class that owns its specific lock+tx ordering and lifecycle.

```csharp
namespace Paramore.Brighter.BoxProvisioning;

public interface IAmAProvisioningUnitOfWork<TTransaction> : IAsyncDisposable
    where TTransaction : DbTransaction
{
    /// <summary>
    /// The active transaction, if this backend uses one. Null for transaction-less
    /// backends (MySQL — see ADR 0057 §5a). Detection helpers and path methods
    /// receive this value via the runner so they can participate in the atomic scope.
    /// </summary>
    TTransaction? Transaction { get; }

    /// <summary>
    /// Opens the atomic scope: acquires the advisory lock and begins the transaction
    /// (or both, in whichever order this backend requires). Throws on lock acquisition
    /// timeout or transaction-begin failure. The runner declares the UoW with
    /// `await using`, so <see cref="IAsyncDisposable.DisposeAsync"/> is invoked by the
    /// language on every exit path — including when <see cref="BeginAsync"/> itself
    /// throws. Implementations MUST therefore be safe for `DisposeAsync` after a
    /// failed/skipped `BeginAsync`: the dispose path runs as a no-op or partial
    /// cleanup and never throws. After a thrown <see cref="BeginAsync"/>, the runner
    /// does NOT call <see cref="CommitAsync"/> or <see cref="RollbackAsync"/>.
    /// </summary>
    Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken);

    /// <summary>
    /// Commits the atomic scope. Releases the lock (where lock release is explicit
    /// rather than transaction-scoped). After CommitAsync, the only valid call
    /// is DisposeAsync.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rolls back the atomic scope. Releases the lock (where lock release is
    /// explicit rather than transaction-scoped). After RollbackAsync, the only
    /// valid call is DisposeAsync. RollbackAsync MUST NOT throw — disposal-style
    /// semantics — see Harmonised lifecycle contract (§B.3).
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
```

**Per-backend implementations** (encapsulating the per-backend ordering investigated against the existing runners):

| Backend  | Class                                | `BeginAsync` order                                               | Commit/Rollback lock release                  |
|----------|--------------------------------------|------------------------------------------------------------------|-----------------------------------------------|
| MSSQL    | `MsSqlProvisioningUnitOfWork`        | `BeginTransaction → AcquireLock(@LockOwner='Transaction')`       | Implicit on commit/rollback (lock owned by tx) |
| Postgres | `PostgreSqlProvisioningUnitOfWork`   | `AcquireLock(pg_advisory_lock) → BeginTransaction`               | Explicit `pg_advisory_unlock` then commit/rollback |
| MySQL    | `MySqlProvisioningUnitOfWork`        | `AcquireLock(GET_LOCK)` only (no tx — DDL auto-commits)          | Explicit `RELEASE_LOCK`; commit/rollback are no-ops |
| SQLite   | `SqliteProvisioningUnitOfWork`       | `BEGIN IMMEDIATE` (the writer-slot reservation IS the lock)      | Commit/rollback releases the writer slot (no separate lock) |

**Rationale**:

- **Eliminates the lock-vs-transaction ordering contradiction**. The original spec 0028 design tried to expose `AcquireLockAsync` and `BeginUnitOfWorkAsync` as separate ordered hooks on the runner base, prescribing the order `AcquireLock → BeginUnitOfWork`. This contradicted MSSQL, where `sp_getapplock @LockOwner='Transaction'` requires the transaction to exist before the lock is acquired. The UoW abstraction hides the ordering inside each backend's class — the runner base never observes "first this, then that" on lock vs tx.
- **Eliminates leaky no-op hooks**. The original design had MySQL implementing no-op `BeginTransaction/Commit/Rollback` and SQLite implementing a no-op `AcquireLock`. Under UoW, MySQL's UoW commits/rolls-back as no-op internally; SQLite's UoW folds lock+tx into one operation. The runner base sees a uniform UoW lifecycle.
- **Per-backend diagnostic preserved**. Each backend's UoW handles its own lock-release diagnostic in DisposeAsync (or in CommitAsync/RollbackAsync). MySQL's UoW preserves the existing `RELEASE_LOCK` tri-state distinction (`1`/`0`/`NULL`) in its own LogWarning — no information loss, no tri-state-collapsed-to-bool concern. The harmonised contract (§B.3) is about the UoW lifecycle, not about a uniform return type.
- **Naming**: "ProvisioningUnitOfWork" rather than "MigrationUnitOfWork" because the role serves both `IAmABoxMigrationRunner` (during a migration) and could serve other provisioning-time atomic scopes if a future ADR introduces them. "Unit of work" is the established name for "the atomic scope that wraps a coherent piece of work" — used widely in EF Core, NHibernate, etc.
- **Generic on `TTransaction`** — so the runner base can pass `uow.Transaction` to the typed detection helper without casting. MySQL's UoW returns `null` for `Transaction`; the detection helper's transaction parameter is nullable with a default of `null`, so this composes cleanly.

### B.2 `RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` shape

**Decision**: Introduce one abstract base class — `RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` — implementing `IAmABoxMigrationRunner` for the four relational backends. The base owns the algorithm; derived classes supply only the irreducibly-backend-specific hooks. Spanner stays free-standing per ADR 0057 §6.

```csharp
namespace Paramore.Brighter.BoxProvisioning;

public abstract class RelationalBoxMigrationRunnerBase<TConnection, TTransaction>
    : IAmABoxMigrationRunner
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    private readonly IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> _detectionHelper;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly TimeSpan _lockTimeout;

    /// <summary>Logger for the runner base AND for derived classes to forward into per-backend UoW construction.</summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// The detection helper, exposed to derived classes so the bootstrap-path hook can call
    /// <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}.DetectCurrentVersionAsync"/>.
    /// The base itself uses this helper internally for <see cref="RedetectStateAsync"/> (which only
    /// requires the base interface methods); derived classes use it for version inference during bootstrap.
    /// </summary>
    protected IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> DetectionHelper => _detectionHelper;

    /// <summary>
    /// The relational database configuration, exposed to derived classes for use inside their
    /// <c>OpenConnectionAsync</c> hook (typically to read <c>ConnectionString</c>) and any other
    /// hook that needs configuration metadata (e.g. <c>OutBoxTableName</c>, <c>InBoxTableName</c>,
    /// payload-mode flags). The base itself does not currently read this property; it is held on
    /// the base so that derived runners do not duplicate the field.
    /// </summary>
    protected IAmARelationalDatabaseConfiguration Configuration => _configuration;

    protected RelationalBoxMigrationRunnerBase(
        IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> detectionHelper,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        ILogger? logger = null)
    {
        _detectionHelper = detectionHelper;
        _configuration = configuration;
        _lockTimeout = lockTimeout;
        Logger = logger ?? NullLogger.Instance;
    }

    public async Task MigrateAsync(
        string tableName, string? schemaName, BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        // tableState is a stale hint — re-detected under the UoW.
        ValidateMigrationsMonotonic(tableName, migrations);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var lockResource = LockResourceFor(schemaName, tableName);
        await using var uow = await CreateUnitOfWorkAsync(connection, cancellationToken);
        await uow.BeginAsync(lockResource, _lockTimeout, cancellationToken);

        try
        {
            await EnsureHistoryTableAsync(connection, uow.Transaction, schemaName, cancellationToken);

            // Re-detect under the UoW — TOCTOU defence per ADR 0057 §3.
            // (Subclasses with a different detection model can override RedetectStateAsync; see hooks below.)
            var (tableExists, historyExists) = await RedetectStateAsync(
                connection, uow.Transaction, schemaName, tableName, cancellationToken);

            if (!tableExists)
                await RunFreshPathAsync(connection, uow.Transaction, schemaName, tableName, migrations, cancellationToken);
            else if (!historyExists)
                await RunBootstrapPathAsync(connection, uow.Transaction, schemaName, tableName, boxType, migrations, cancellationToken);
            else
                await RunNormalPathAsync(connection, uow.Transaction, schemaName, tableName, migrations, cancellationToken);

            await uow.CommitAsync(cancellationToken);
        }
        catch
        {
            // Pass CancellationToken.None: if the caller's token is already signalled, we still
            // need to release the lock and unwind the transaction. See B.3 cancellation contract.
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    // Abstract hooks (each derived class implements):
    protected abstract Task<TConnection> OpenConnectionAsync(CancellationToken cancellationToken);

    protected abstract Task<IAmAProvisioningUnitOfWork<TTransaction>> CreateUnitOfWorkAsync(
        TConnection connection, CancellationToken cancellationToken);

    protected abstract string LockResourceFor(string? schemaName, string tableName);

    protected abstract Task EnsureHistoryTableAsync(
        TConnection connection, TTransaction? transaction, string? schemaName,
        CancellationToken cancellationToken);

    protected abstract Task RunFreshPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken);

    protected abstract Task RunBootstrapPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken);

    protected abstract Task RunNormalPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken);

    /// <summary>
    /// TOCTOU re-detection under the UoW. Default implementation calls the injected
    /// detection helper; derived classes whose backend has a different detection model
    /// (e.g. a lock primitive that guarantees no concurrent provisioner can interleave,
    /// making re-detection redundant) can override.
    /// </summary>
    protected virtual async Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, tableName, schemaName, cancellationToken, transaction);
        var historyExists = tableExists && await _detectionHelper.DoesHistoryExistAsync(
            connection, tableName, schemaName, cancellationToken, transaction);
        return (tableExists, historyExists);
    }

    // Shared protected helper — input validation lifted from spec 0027 Items H/I/Q.
    protected void ValidateMigrationsMonotonic(string tableName, IReadOnlyList<IAmABoxMigration> migrations) { /* ... */ }
}
```

**Rationale**:

- **The base owns the algorithm** — open connection, create UoW, begin UoW, ensure history, re-detect under UoW, dispatch on detection, commit, dispose. This is what was 70% duplicated across the four runners. Future cross-backend changes (logging conventions, disposal contracts) are made once in the base.
- **`TConnection` AND `TTransaction` generics on the base** — to forward the typed transaction from `uow.Transaction` to the detection helper and the path methods without casting. This mirrors the detection helper's two-generic shape.
- **Detection helper injected into the base, typed at the version-detecting interface** — the base requires `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` (the extended interface), not the bare base interface. This is principled because every relational backend in the matrix performs version detection during the bootstrap path (`MsSqlBoxMigrationRunner.cs:164-165` and the equivalent on the other three relational runners) — the type system enforces that no relational runner can be constructed without the capability it needs. The base interface is preserved for Spanner's free-standing path (per ADR 0057 §6 — Spanner is degenerate and bypasses this base entirely). The base does its own re-detection under the UoW via the `RedetectStateAsync` virtual hook; the default implementation calls only the base-interface methods (`DoesTableExistAsync`, `DoesHistoryExistAsync`), so the field's narrower type is compatible. The helper is exposed to derived classes via `protected DetectionHelper { get; }` so each backend's `RunBootstrapPathAsync` hook can call `DetectCurrentVersionAsync` without re-injecting the same instance — preserving the existing per-runner version-inference behaviour. Derived classes whose backend has a different detection model can override `RedetectStateAsync` (e.g. a future relational backend whose lock primitive guarantees no concurrent provisioner could skip re-detection by overriding to a constant-true return). The injection-plus-virtual-hook shape gives backends three escape hatches in increasing severity: (1) accept the default; (2) override `RedetectStateAsync` to use a different model; (3) bypass the base entirely by implementing `IAmABoxMigrationRunner` directly (Spanner pattern).
- **`LockResourceFor` is a hook** — each backend computes its lock resource string differently (MSSQL/PG: `BrighterMigration_{schema}.{table}`; MySQL: `MySqlMigrationLockName.For(schema, table)` with 64-char hash fallback; SQLite: empty string or sentinel — passed to UoW which ignores it for SQLite). Centralising this in the base would force a lowest-common-denominator string format; hooking it out preserves backend specificity.
- **`EnsureHistoryTableAsync` is a hook** — each backend's history-table DDL differs; the base orchestrates the call but doesn't own the DDL.
- **Path methods are hooks** — fresh / bootstrap / normal path implementations stay per-backend; the DDL execution is irreducibly backend-specific.
- **The `RunNormalPathAsync` hook absorbs spec 0027 Item L** — the redundant `IsMigrationAppliedAsync` call removed in spec 0027 Item L stays removed; each backend's normal-path impl walks the migrations list with the `migration.Version <= maxVersion` filter as it does today.

### B.3 Harmonised UoW lifecycle, cancellation, and disposal contract

**Decision**: The base class and the `IAmAProvisioningUnitOfWork<TTransaction>` interface together prescribe a uniform contract that every relational backend conforms to:

| Concern | Contract |
|---------|----------|
| **Order of operations** (success path) | OpenConnection → CreateUnitOfWork → uow.BeginAsync → EnsureHistoryTable → re-detect existence → dispatch to one of Run{Fresh,Bootstrap,Normal}Path → uow.CommitAsync → (await using unwinds) uow.DisposeAsync → connection.DisposeAsync |
| **Order of operations** (failure path) | …on any exception thrown from EnsureHistoryTable / re-detect / Run\* path: catch → uow.RollbackAsync(CancellationToken.None) → rethrow → (await using unwinds) uow.DisposeAsync → connection.DisposeAsync. |
| **`BeginAsync` throws** | The runner does NOT call CommitAsync or RollbackAsync. The exception propagates out of the `try`-less `await uow.BeginAsync(...)` line; `await using` still calls uow.DisposeAsync (which MUST tolerate dispose-after-failed-begin per the BeginAsync XML doc) and connection.DisposeAsync. The exception is then rethrown from MigrateAsync. |
| **`CommitAsync` throws** | The catch path runs: `uow.RollbackAsync(CancellationToken.None)` is called even though commit was attempted. Each UoW impl makes RollbackAsync best-effort after a thrown CommitAsync — it inspects the underlying transaction state (e.g. MSSQL `Zombied`, PG `TransactionStatus.Closed`) and skips rollback if the transaction is already finalised. Lock release is similarly best-effort: if the lock was already released (MSSQL `@LockOwner='Transaction'` releases on the failed COMMIT) the UoW logs a Warning at most, never throws. The MUST-NOT-throw rule on RollbackAsync still applies. |
| **Cancellation** | The token flows into every hook AND into uow.BeginAsync / uow.CommitAsync. Cancellation behaviour depends on the window: (a) **OCE during `OpenConnectionAsync`, `CreateUnitOfWorkAsync`, or `BeginAsync`** is treated identically to any other exception thrown from those calls — the runner does NOT call CommitAsync or RollbackAsync; `await using` disposes whatever was constructed (per the BeginAsync-throws and dispose-after-failed-begin rules above). (b) **OCE during EnsureHistoryTable / RedetectStateAsync / Run\* path / uow.CommitAsync** enters the catch path: `uow.RollbackAsync(CancellationToken.None)` runs (NOT the caller's cancelled token), then the exception is rethrown. `await using` then disposes the UoW and the connection. The `CancellationToken.None` on rollback is load-bearing — if the caller's token is already signalled, passing it would cause `RollbackAsync` itself to throw `OperationCanceledException` and abandon the unwind. |
| **Rollback contract** | `uow.RollbackAsync` MUST NOT throw. If the underlying transaction-rollback or lock-release fails internally, the UoW logs a Warning (with backend-specific diagnostic — e.g. MySQL preserves its `RELEASE_LOCK` tri-state) and returns. Disposal then proceeds normally. |
| **Disposal contract** | `uow.DisposeAsync` MUST NOT throw. Implementations MUST tolerate Dispose after a failed/skipped `BeginAsync` as a no-op or partial cleanup. If a disposal step throws internally, the UoW logs an Error and continues (or returns) — disposal exceptions are never propagated to the caller of `MigrateAsync`. The same MUST-NOT-throw rule applies to `connection.DisposeAsync` (already true for `DbConnection.DisposeAsync`). |
| **Logger plumbing** | The runner base accepts `ILogger?` in the ctor and exposes it as `protected ILogger Logger { get; }` (defaulting to `NullLogger.Instance` when null). Each derived runner forwards `Logger` to the per-backend UoW it constructs in `CreateUnitOfWorkAsync`, e.g. `new MsSqlProvisioningUnitOfWork(connection, advisoryLock, Logger)`. This avoids cluttering the `CreateUnitOfWorkAsync` hook signature with a logger parameter while making the logger flow explicit and overridable per backend. UoW classes accept `ILogger` in their ctor and use it for the per-backend lock-release diagnostic in `CommitAsync` / `RollbackAsync` / `DisposeAsync`. |
| **Logging diagnostics** | Per-backend lock-release diagnostics live in each UoW's `RollbackAsync`/`CommitAsync`/`DisposeAsync`. The base class does NOT collapse return types or log on the UoW's behalf — UoW logs its own concerns. This preserves MySQL's tri-state diagnostic (`1`/`0`/`NULL` distinction per spec 0027 Item M / ADR 0057 §5b) without information loss. |

**Rationale**: the contract is the *whole point* of the abstract base + UoW pairing. Without uniform rollback/disposal/cancellation behaviour, the abstraction is just a duplication-removal tool; with it, the base + UoW become the documented contract for "what running a migration means" in Brighter. Cancellation is specified at every boundary (not just one), addressing the gap that two implementers would otherwise resolve differently. Spec 0027 Items D/M's per-backend diagnostics consolidate inside per-backend UoWs (no information loss); Item N's `MigrationLockDeadlockException` still propagates from `uow.BeginAsync` to the caller (it's an acquire-path concern, surfaces during BeginAsync).

### B.4 Open-closed sweep — feedback item 8

**Decision** (addressing requirement F9): After surveying spec 0027's surface beyond the families addressed in §A and the runners + UoW in §B.1–§B.3, **no further open-closed candidates earn their keep**. The candidates considered:

| Candidate                                                                | Decision | Reason                                                                                                                                                                                                              |
|--------------------------------------------------------------------------|----------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `I*AdvisoryLock` family (Items D/M/N) — could share a base?              | **No**   | Per-backend lock primitives have fundamentally different return semantics (PG bool, MySQL `bool?`, MSSQL throws). The UoW abstraction in §B.1 already hides this variation behind a uniform lifecycle. Sharing a *lock* base would force lowest-common-denominator semantics where the UoW already provides the right level of unification. |
| `*Outbox/InboxMigrations` row data — V_n × backend cross product?        | **No**   | Each migration row is unique DDL. Rows have nothing to share.                                                                                                                                                       |
| `BoxProvisioningHostedService` — open-closed via abstract base?          | **No**   | Single class with no per-backend variants. Open-closed is satisfied by the existing `IAmABoxProvisioner` collection injection.                                                                                      |
| `Identifiers.AssertSafe` (spec 0027 Item Q) helper — interface?          | **No**   | Already a single static method in the shared `Paramore.Brighter.BoxProvisioning` assembly. No polymorphic role.                                                                                                     |
| `*BoxProvisioner` (relational 8) — could share an abstract base? *(amended 2026-05-12)* | **Yes** — see §B.5 | **Post-acceptance amendment.** The original 2026-05-07 sweep treated provisioners at the role-interface level only (item 4 — already met by `IAmABoxProvisioner`) and did not consider implementation-side body duplication. After Phase 8 cleaned the eight relational provisioners onto the canonical 5-arg ctor with instance-dispatched dependencies, the duplication became structurally visible (~80 lines × 8 = ~640 lines of substantially identical body). See §B.5 for the design. |

Decision recorded in this ADR. If implementation surfaces new candidates, the spec 0028 tasks list folds them in or defers them with documented reason.

**Post-acceptance amendment (2026-05-12)**: row 5 added. Discovered during a code-review pass of the ten shipped `*BoxProvisioner.cs` files on 2026-05-12 (HEAD `efdced78e`, post-Phase-12 acceptance, before PR #4039 merge). The closing-paragraph clause above is invoked: the candidate is folded into sub-phase A (per parent requirements §F10–F13 / §AC12 / `sweep-result.md` Amendment); the design lives at §B.5 below.

---

### B.5 `SqlBoxProvisioner<TConnection, TTransaction>` — pulling the eight relational provisioners up (sub-phase A, post-acceptance)

> **Status**: Proposed (sub-phase A, 2026-05-12) — pending `/spec:approve design`. Approval re-stamps the top-of-file ADR Status to Accepted.

**Decision** (addressing requirements F10, F11, F12, F13): Introduce one abstract base class — `SqlBoxProvisioner<TConnection, TTransaction>` — implementing `IAmABoxProvisioner` for the eight relational provisioners (MSSQL/PG/MySQL/SQLite × Outbox/Inbox). The base owns the orchestration; derived classes supply only the irreducibly-backend-specific hooks. Spanner's pair (`SpannerOutboxProvisioner`, `SpannerInboxProvisioner`) stays free-standing per the same exemption shape as `RelationalBoxMigrationRunnerBase` (§B.2 / ADR 0057 §6).

```csharp
namespace Paramore.Brighter.BoxProvisioning;

public abstract class SqlBoxProvisioner<TConnection, TTransaction>
    : IAmABoxProvisioner
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    private readonly IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> _detectionHelper;
    private readonly IAmABoxMigrationCatalog _catalog;
    private readonly IAmABoxPayloadModeValidator<TConnection> _payloadValidator;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    protected SqlBoxProvisioner(
        IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<TConnection> payloadValidator,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner,
        BoxType boxType)
    {
        _detectionHelper = detectionHelper;
        _catalog = catalog;
        _payloadValidator = payloadValidator;
        _configuration = configuration;
        _migrationRunner = migrationRunner;
        BoxType = boxType;
    }

    public BoxType BoxType { get; }

    public string BoxTableName => BoxType == BoxType.Outbox
        ? _configuration.OutBoxTableName
        : _configuration.InBoxTableName;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = _catalog.All(_configuration);
        var tableState = await DetectTableStateAsync(migrations, cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await _migrationRunner.MigrateAsync(
            BoxTableName,
            _configuration.SchemaName,
            BoxType,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        // Sync `using` for the connection: DbConnection does not implement IAsyncDisposable
        // on netstandard2.0, so `await using` would not compile across the shared-assembly
        // TFM matrix. Mirrors the §B.2 precedent at RelationalBoxMigrationRunnerBase.cs:112-116.
        using var connection = CreateConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, BoxTableName, EffectiveSchemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await _detectionHelper.DoesHistoryExistAsync(
            connection, BoxTableName, EffectiveSchemaName, cancellationToken);
        if (!historyExists)
        {
            // Pre-lock detection is a hint for the caller; the runner re-detects under the lock.
            // Negative or zero return values are not gated here — the runner is the single source
            // of truth for discriminator violations and unknown-schema rejections.
            var detectedVersion = await _detectionHelper.DetectCurrentVersionAsync(
                connection, BoxTableName, EffectiveSchemaName,
                BoxType, migrations, cancellationToken);
            return new BoxTableState(
                TableExists: true, HistoryExists: false,
                CurrentVersion: ClampDetectedVersion(detectedVersion));
        }

        var maxVersion = await _detectionHelper.GetMaxVersionAsync(
            connection, BoxTableName, EffectiveSchemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        // Sync `using` for the connection: DbConnection does not implement IAsyncDisposable
        // on netstandard2.0, so `await using` would not compile across the shared-assembly
        // TFM matrix. Mirrors the §B.2 precedent at RelationalBoxMigrationRunnerBase.cs:112-116.
        using var connection = CreateConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await _payloadValidator.ValidateAsync(
            connection, BoxTableName, EffectiveSchemaName,
            PayloadColumnName, _configuration.BinaryMessagePayload, cancellationToken);
    }

    // Abstract hooks (each derived class implements):

    /// <summary>
    /// Backend-specific connection factory. Implementations typically return
    /// <c>new {Backend}Connection(connectionString)</c>.
    /// </summary>
    protected abstract TConnection CreateConnection(string connectionString);

    /// <summary>
    /// The payload column name to validate against the configured payload mode.
    /// Outbox: <c>"Body"</c> (most backends) or <c>"body"</c> (Postgres lower-case convention).
    /// Inbox: <c>"CommandBody"</c> / <c>"commandbody"</c>.
    /// </summary>
    protected abstract string PayloadColumnName { get; }

    // Virtual hooks (each derived class may override):

    /// <summary>
    /// The schema name to pass to the detection helper and payload validator. Defaults to the
    /// configured schema; SQLite overrides to <c>null</c> (no schema concept per ADR 0057 §6).
    /// The runner call inside <see cref="ProvisionAsync"/> always uses <c>_configuration.SchemaName</c>
    /// directly — only the detection-helper and payload-validator calls observe this property.
    /// </summary>
    protected virtual string? EffectiveSchemaName => _configuration.SchemaName;

    /// <summary>
    /// Post-process the version inferred by <c>DetectCurrentVersionAsync</c>. Default clamps negative
    /// values (e.g. spec 0027's <c>-1</c> "discriminator missing" sentinel) to zero — the pre-lock
    /// value is a hint and the runner is authoritative.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Transitional — removed in Phase 13.B.</b> This hook exists solely to host MySQL's
    /// no-clamp override during Phase 13.A (NF9 — bit-for-bit behavioural neutrality of the
    /// structural pull-up). Phase 13.B (F11) unifies MySQL's behaviour with the other three
    /// relational backends and, in the same commit, removes both <c>MySql*Provisioner</c>'s
    /// override AND this hook — inlining <c>detectedVersion &lt; 0 ? 0 : detectedVersion</c>
    /// directly into <see cref="DetectTableStateAsync"/>. After 13.B no derivation overrides
    /// this method, so the hook earns no keep; preserving it post-13.B would be speculative
    /// generality. A future backend with a richer sentinel set can re-introduce a hook in
    /// a follow-up spec without regret.
    /// </para>
    /// </remarks>
    protected virtual int ClampDetectedVersion(int detectedVersion) =>
        detectedVersion < 0 ? 0 : detectedVersion;
}
```

**Rationale**:

- **The base owns the algorithm.** `ProvisionAsync` + `DetectTableStateAsync` + `ValidatePayloadModeAsync` were ~80 lines duplicated across the eight relational provisioners, differing only on five deltas (see hook table below). Pulling them up makes the orchestration a documented contract for "what provisioning a box means" — and means future cross-backend changes are made once.
- **`TConnection` AND `TTransaction` generics on the base.** The injected role-interface dependencies are typed on the backend-specific connection (`NpgsqlConnection`, `MySqlConnection`, etc.). A non-generic base typed at `DbConnection` would not compile — the detection helper's methods require the narrowed `TConnection`. The two-generic shape matches §B.2 `RelationalBoxMigrationRunnerBase`.
- **Detection helper injected at the version-detecting interface.** Same reasoning as §B.2: every relational provisioner performs version detection during the bootstrap pre-check; the type system enforces that no relational provisioner can be constructed without the capability it needs. Spanner is degenerate (per ADR 0057 §6) and bypasses this base — Spanner's pair takes the base `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` instead and stays free-standing.
- **`PayloadColumnName` is abstract** (not virtual with a default). The four relational backends each pick a casing convention (`"Body"` Pascal vs `"body"` lower) per their column-naming style; there is no neutral default that fits all four. Forcing an explicit declaration on each derivation prevents a copy-paste mistake from a sibling backend.
- **`EffectiveSchemaName` is virtual.** Three of four relational backends use `_configuration.SchemaName` directly; SQLite passes `null` to detection and validation calls because SQLite has no schema concept. The default-with-override shape matches the existing behaviour and keeps the SQLite divergence local to the SQLite pair. The runner call inside `ProvisionAsync` propagates `_configuration.SchemaName` independently — only detection/validation observes `EffectiveSchemaName`.
- **`ClampDetectedVersion` is a *transitional* virtual hook with a clamp default.** Three of four relational backends clamp `detectedVersion < 0 ? 0 : v`; MySQL does not. Picking clamp as the default matches the three-of-four majority and the principled rationale already documented inline in the existing MSSQL/PG/SQLite code (*"the pre-lock value is a hint; the runner is authoritative"*). MySQL's transitional override during Phase 13.A returns identity to preserve current behaviour bit-for-bit (NF9). **Phase 13.B removes both MySQL's override AND this hook in one commit** — inlining the clamp directly into `DetectTableStateAsync`. The hook exists solely to host the transient 13.A divergence; after 13.B every derivation would inherit the same default, so retaining the hook would be speculative generality (no surveyed second-derivation scenario justifies it; CLAUDE.md *"Do NOT change defaults or make changes beyond what was explicitly requested"* applies). A future backend with a different clamp policy can re-introduce a hook at the point of need without regret. The hook's lifecycle is therefore: **introduced in Phase 13.A → exercised by MySQL's override in 13.A → removed (along with MySQL's override and the clamp inlined into `DetectTableStateAsync`) in Phase 13.B**.
- **Sync `using` for the connection — mirroring the §B.2 precedent.** The base preserves the existing sync-disposal shape used by three of four relational provisioners (MSSQL/MySQL/SQLite) and adopted by `RelationalBoxMigrationRunnerBase` (the sibling base in the same shared assembly) for the same reason: the shared `Paramore.Brighter.BoxProvisioning` assembly targets `netstandard2.0;net8.0;net9.0;net10.0` per C6, and `DbConnection` does not implement `IAsyncDisposable` on netstandard2.0 — so a base-class `await using` declaration over `TConnection : DbConnection` does not compile across the shared-assembly TFM matrix. The existing `PostgreSqlOutboxProvisioner` can use `await using` only because its package targets `$(BrighterCoreTargetFrameworks)` (= `net8.0;net9.0;net10.0`) and not netstandard2.0; the base does not have that latitude. The §B.2 sibling base at `src/Paramore.Brighter.BoxProvisioning/RelationalBoxMigrationRunnerBase.cs:112-116` already documents this constraint verbatim — *"// Sync `using` for the connection: DbConnection does not implement IAsyncDisposable on netstandard2.0, so `await using` would not compile across the shared-assembly TFM matrix. The UoW does implement IAsyncDisposable (see IAmAProvisioningUnitOfWork) and is declared with `await using` below."* — and sub-phase A mirrors that decision for the same reason. (The UoW-vs-connection asymmetry the §B.2 comment closes with is informative: per-instance `await using` IS available for types that declare `IAsyncDisposable` directly, just not for `DbConnection` itself via the constrained-type-parameter `TConnection`. `SqlBoxProvisioner` does not introduce a UoW or other disposable; the only disposable in §B.5 is the connection, which therefore stays sync.) F12's IAsyncDisposable probe is therefore moot: the answer is already in the codebase. `baseline.md` records the disposition (sync `using` per §B.2 precedent) rather than running the probe. PG's free-standing provisioner can keep its existing `await using` shape only outside the base — but with PG's derived class now inheriting the base's method bodies, the per-call disposal pattern is sync-uniform across all four backends. (A future TFM bump that drops netstandard2.0 from the shared assembly may revisit; out of scope here.)
- **`BoxType` is a ctor parameter, not abstract.** Outbox vs Inbox differs only in which catalogue is injected, which table name is read from configuration, and which payload column is named. Encoding `BoxType` as a ctor parameter (not an abstract property) keeps the eight derivations symmetric — each derived class has the same shape, differing only in which `BoxType` value it forwards to the base ctor. `BoxTableName` follows from `BoxType` via a sealed base property.

#### Hook table (F10.1)

| Hook | Kind | Default | Per-backend override expectation |
|---|---|---|---|
| `CreateConnection(string) → TConnection` | abstract | — | All eight derivations: `new {Backend}Connection(connectionString)` |
| `PayloadColumnName : string` | abstract | — | All eight derivations: backend-specific casing (`"Body"` / `"body"` / `"CommandBody"` / `"commandbody"`) |
| `EffectiveSchemaName : string?` | virtual | `_configuration.SchemaName` | SQLite pair overrides to `null`; MSSQL/PG/MySQL pairs inherit default |
| `ClampDetectedVersion(int) → int` *(transitional — removed in Phase 13.B)* | virtual | `v < 0 ? 0 : v` | MySQL pair overrides to identity during Phase 13.A; Phase 13.B removes both the MySQL override AND this hook (clamp inlined into `DetectTableStateAsync`) per F11 |

#### Variance reconciliation (the five deltas surveyed 2026-05-12)

| # | Delta observed in the eight pre-sub-phase-A provisioners | Resolution |
|---|---|---|
| a | Connection factory (per-backend `new {Backend}Connection(cs)`) | `CreateConnection` abstract hook (one line per derivation) |
| b | Payload column casing (`"Body"` / `"body"` / `"CommandBody"` / `"commandbody"`) | `PayloadColumnName` abstract property (one line per derivation) |
| c | Schema-name argument (relational `_configuration.SchemaName`; SQLite `null`) | `EffectiveSchemaName` virtual; SQLite pair overrides |
| d | Negative-version clamp (MSSQL/PG/SQLite clamp; MySQL no-clamp) | `ClampDetectedVersion` virtual with clamp default — transitional hook. MySQL transitional override in Phase 13.A; F11 removes BOTH the MySQL override AND the hook itself in Phase 13.B (clamp inlined into `DetectTableStateAsync`). Post-13.B the base has no hook for this delta. |
| e | Disposal pattern (`await using` Postgres only; `using` others) | Base uses sync `using` for the connection per §B.2 precedent (`RelationalBoxMigrationRunnerBase.cs:112-116`) — `DbConnection` lacks `IAsyncDisposable` on netstandard2.0, so a base-class `await using` over `TConnection : DbConnection` does not compile. No backend-specific override hook is needed — the sync shape is uniform across all four derivations. F12 disposition recorded in `baseline.md`; no probe required. |

#### Spanner exemption (NF11, OoS11)

`SpannerOutboxProvisioner` and `SpannerInboxProvisioner` cannot derive from `SqlBoxProvisioner` because the base's ctor requires `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` — the §A.1 extension that adds `DetectCurrentVersionAsync` — and Spanner's pair receives the base interface `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` only (no version detection, per ADR 0057 §6 / §A.1 Backend assignments). That compile-time mismatch is the operative reason for the exemption; the surrounding differences are independent symptoms of the same Spanner-degeneracy (fresh-install only, no V_k chain, no migration catalogue per §A.2, custom column-subset version inference `V1Columns.IsSubsetOf(actualColumns)` rather than the per-migration column-set walk the relational helpers use). The same exclusion shape that keeps Spanner out of `RelationalBoxMigrationRunnerBase` (§B.2) keeps it out of `SqlBoxProvisioner`. Two alternative shapes were considered and rejected: (a) a Spanner-specific abstract layer above `SqlBoxProvisioner` that knew about both the detection-helper-interface and catalogue exemptions — adding a class for one derivation, and (b) hooks on `SqlBoxProvisioner` that nullify themselves under a flag — Liskov-hostile. Neither earns its keep. The right place for Spanner's degeneracy is at the `IAmABoxProvisioner` level, free-standing — same shape as the runner exemption from §B.2.

#### Naming (NF8)

The base class is `SqlBoxProvisioner` — **not** `BoxProvisionerBase` and **not** `RelationalBoxProvisionerBase`. The choice is justified on one current, concrete ground and the asymmetry with §B.2 (`RelationalBoxMigrationRunnerBase`) is acknowledged honestly with a defined follow-up:

1. **Precision of the implementation contract.** The base's qualifier is `where TConnection : DbConnection` — i.e. the ADO.NET `DbConnection` lineage with its `IDbCommand`/`DbCommand`-shaped query surface. "Sql" honestly names that contract. "Relational" names a broader *category* — Spanner IS a relational/SQL backend per Google's documentation and per ADR 0057 §6, yet Spanner is exempt from this base (see Spanner exemption above). A `RelationalBoxProvisionerBase` would therefore name a category that includes one of its own exempt backends, muddling its scope; `SqlBoxProvisioner` names the implementation lineage cleanly. The `*Base` suffix is dropped because §A's role interfaces (`IAmA*`) deliberately eschew the `*Base` suffix and the new base mirrors that style.

2. **Asymmetry with §B.2 is acknowledged — and is reconciled by a defined follow-up, not deferred indefinitely.** §B.2's `RelationalBoxMigrationRunnerBase` was authored 2026-05-07 when only one abstraction layer existed in the migration-runner family; the `Relational*Base` name reflects that earlier choice. Sub-phase A could either propagate §B.2's name (entrenching the imprecision identified in point 1) or rename §B.2 in the same sub-phase (out of scope per parent OoS3 — *"No changes to existing public role interfaces"* — and the sibling abstract base is part of the spec 0028 public surface accepted on 2026-05-07; reopening it post-acceptance is out of bounds). The third option, chosen here, is to ship the right name for §B.5 now and commit to a follow-up that renames §B.2 for symmetry. The follow-up is recorded as a "Post-merge follow-up" bullet on the PR #4039 description (added 2026-05-12 in the same edit pass as this ADR amendment) — that bullet commits to authoring a successor ADR renaming `RelationalBoxMigrationRunnerBase` to a `Sql*`-prefixed equivalent (e.g. `SqlBoxMigrationRunner`) before any third-party adopter takes a hard dependency on either base. The asymmetry is therefore time-bounded by the same PR that introduces it, not deferred to an indefinite "future spec if symmetric naming becomes load-bearing".

The deviation from `*Base` suffix is documented here to satisfy parent C4 / AC9 (naming-convention reconciliation). The follow-up commitment is recorded under Consequences → Risks and Mitigations (see the new "Naming asymmetry, time-bounded" risk).

#### Source-break and TFM-matrix notes

- **Source-break**: none. Both ctors on every derived class (5-arg canonical from Phase 8, 2-arg back-compat) are preserved. Both delegate to `base(...)`. No call-site change required — the DI extensions wired in Phase 9 continue to construct the 5-arg ctor (per NF10).
- **TFM matrix unchanged** (NF11 / parent C6 / C7). The base uses plain generic class declaration; both type constraints (`where TConnection : DbConnection` and `where TTransaction : DbTransaction`) are pre-net5 compatible. No static virtual / static abstract / `IReadOnlySet<T>` introduced.
- **Public surface delta**: +1 type (`SqlBoxProvisioner<TConnection, TTransaction>` — abstract) in the shared `Paramore.Brighter.BoxProvisioning` assembly. Per parent AC8 / NF6 this is a runtime production type, not testability-motivated — derived classes are the production surface; the base is the algorithm host.

---

## Adding a new BoxProvisioning backend

(Section satisfying §A.4 / requirement F8.)

To add a new BoxProvisioning backend (e.g. Oracle), implement the following in a new package `Paramore.Brighter.BoxProvisioning.{Backend}`:

1. **Detection helper**: `public class {Backend}BoxDetectionHelper : IAmAVersionDetectingMigrationHelper<{Backend}Connection, {Backend}Transaction>`. Implement the six methods. The transaction parameter is nullable with a default of `null`; if your backend has no in-flight-transaction concern, accept and ignore it (document on the implementing class). If your backend is degenerate (fresh-install only — no version inference), implement `IAmABoxMigrationDetectionHelper<{Backend}Connection, {Backend}Transaction>` only (omitting `DetectCurrentVersionAsync`) and treat your runner as Spanner-style per ADR 0057 §6. Document the null-handling rule for `schemaName` on the implementing class (substitute the backend default — see §A.1).
2. **Migration catalogues**: `public class {Backend}OutboxMigrationCatalog : IAmABoxMigrationCatalog` and `public class {Backend}InboxMigrationCatalog : IAmABoxMigrationCatalog`. Each implements `IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration)`. Skip if degenerate.
3. **Payload-mode validator**: `public class {Backend}PayloadModeValidator : IAmABoxPayloadModeValidator<{Backend}Connection>`. Apply the same `schemaName` null-substitution rule as the detection helper.
4. **Advisory lock primitive** (if your backend has one): `public interface I{Backend}AdvisoryLock` and a default `public class {Backend}AdvisoryLock : I{Backend}AdvisoryLock` per ADR 0057 §5b. Acquire returns; release returns `bool` (or backend-specific richer type — MySQL's tri-state `bool?` is the precedent for richer return types). Skip if your backend has no advisory lock concept (SQLite/Spanner pattern — fold the lock into the UoW's transaction-begin or omit entirely).
5. **Provisioning UoW**: `public class {Backend}ProvisioningUnitOfWork : IAmAProvisioningUnitOfWork<{Backend}Transaction>`. The ctor receives the connection, the advisory-lock primitive from step 4 (where applicable), and an `ILogger`. Encapsulate your backend's lock+transaction pairing (and ordering — see §B.1 for the existing four for reference). If your backend has no transaction (like MySQL), return `null` for `Transaction` and make Commit/Rollback no-ops on the transaction side; your UoW is the lock-only scope. If your backend has no advisory lock (like SQLite), fold the lock into your transaction-begin (e.g. `BEGIN IMMEDIATE`-style) and make the lock release implicit.
6. **Provisioners**: for a **relational backend** (SQL-shaped, with a `DbConnection`-derived connection type), derive from the abstract base `SqlBoxProvisioner<{Backend}Connection, {Backend}Transaction>` (§B.5 — sub-phase A, post-acceptance amendment). The base owns the `ProvisionAsync` / `DetectTableStateAsync` / `ValidatePayloadModeAsync` orchestration. Post-Phase-13.B the base exposes three hooks: `CreateConnection` (factory — abstract), `PayloadColumnName` (`"Body"` / `"CommandBody"` casing — abstract), and optional `EffectiveSchemaName` override (return `null` if your backend has no schema concept — SQLite pattern). Post-13.B the base inlines `detectedVersion < 0 ? 0 : detectedVersion` into `DetectTableStateAsync` directly; there is no per-backend clamp hook. (If you are reading this between merge of the spec 0028 PR and the Phase 13.B commit, the base has four hooks — `ClampDetectedVersion` is present as a transitional virtual hook with a clamp default; MySQL's pair overrides it to identity during this window only. See the §B.5 Hook table for the lifecycle; Phase 13.B removes both MySQL's override AND the hook itself by inlining the clamp into `DetectTableStateAsync`.) For a **degenerate or non-SQL backend** (Spanner pattern — fresh-only, no V_k chain, no catalogue, or non-`DbConnection` connection model), implement `IAmABoxProvisioner` directly without deriving from the base — same exemption shape as the runner exemption from `RelationalBoxMigrationRunnerBase` (§B.2). Either way, the public surface is `public class {Backend}OutboxProvisioner` and `public class {Backend}InboxProvisioner`, both implementing (directly or via the base) the existing `IAmABoxProvisioner` interface (ADR 0053; no new role interface introduced by spec 0028). Each provisioner ctor receives three new instance-typed parameters (the static-to-instance cascade from §A.1, §A.2, §A.3 — see the §A.1 source-break "Provisioner ctor cascade" bullet for the parallel summary):
   - **Detection helper** — `IAmAVersionDetectingMigrationHelper<{Backend}Connection, {Backend}Transaction>` for the relational four (provisioners call both base-interface methods AND `DetectCurrentVersionAsync` during their bootstrap branch, so the version-detecting interface is required at compile time). Spanner's pair receives the base-interface-typed helper (`IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>`) — Spanner's provisioner does no version inference per ADR 0057 §6.
   - **Migration catalogue** — `IAmABoxMigrationCatalog` (one instance: Outbox provisioner receives the `{Backend}OutboxMigrationCatalog`; Inbox provisioner receives the `{Backend}InboxMigrationCatalog`). Existing static `{Backend}{Box}Migrations.All(config)` call-sites become `_catalog.All(config)` on the injected field. Spanner's pair: omit (no migrations).
   - **Payload-mode validator** — `IAmABoxPayloadModeValidator<{Backend}Connection>` (single-generic, no `TTransaction`). Existing static `{Backend}PayloadModeValidator.ValidateAsync(...)` call-sites become `_payloadValidator.ValidateAsync(...)` on the injected field.

   The DI registration in `Add{Backend}{Box}` registers all three roles as singletons (each role-impl is stateless after construction; singleton lifetime is correct) and supplies them to the provisioner ctor; see the existing `AddMsSqlOutbox`/`AddMsSqlInbox` shape (after spec 0028 lands) for the canonical pattern. The relational four register a singleton per role per backend — the catalogues register two (one Outbox, one Inbox) for a total of four registrations per backend.
7. **Migration runner**: `public class {Backend}BoxMigrationRunner : RelationalBoxMigrationRunnerBase<{Backend}Connection, {Backend}Transaction>`. The base ctor requires an `IAmAVersionDetectingMigrationHelper<{Backend}Connection, {Backend}Transaction>`, an `IAmARelationalDatabaseConfiguration`, a lock-timeout `TimeSpan`, and an optional `ILogger` — pass your `{Backend}BoxDetectionHelper` instance from step 1 plus the configuration through. The lock timeout is per-runner-instance (a deployment-time tuning knob, not a per-migration knob): each `Add{Backend}Outbox`/`Add{Backend}Inbox` extension supplies it from configuration at DI registration, and a deployment that needs a different timeout for one backend constructs that backend's runner with the desired `TimeSpan`. There is no per-call override; if a deployment ever needs one, a future ADR can add an overload to `MigrateAsync`. Override the abstract hooks. In your `OpenConnectionAsync` override, read `Configuration.ConnectionString` (the protected property exposed by the base) to construct your `{Backend}Connection`. In your `CreateUnitOfWorkAsync` override, construct your UoW with the advisory-lock primitive and `Logger` (the protected property exposed by the base — see §B.3 logger plumbing). Your derived runner ctor adds an `I{Backend}AdvisoryLock` parameter (or your backend's lock-primitive interface from step 4) and forwards `(detectionHelper, configuration, lockTimeout, logger)` to the base ctor; store the lock primitive as a private field for use inside `CreateUnitOfWorkAsync`. Backends without an advisory-lock primitive (SQLite/Spanner pattern) skip this addition — their UoW folds the lock into transaction-begin per step 5. In your `RunBootstrapPathAsync` override, call `DetectionHelper.DetectCurrentVersionAsync(...)` to infer the in-place version (the protected property is exposed by the base, typed at the version-detecting interface — no separate field or parameter required). Use the existing four relational runners as references (after spec 0028 lands). If your backend's detection model is non-standard, override the virtual `RedetectStateAsync` hook. If degenerate (no version inference — Spanner pattern), implement `IAmABoxMigrationRunner` directly and skip this base.

DI extensions follow the existing per-backend `static class {Backend}BoxProvisioningExtensions` convention (`Add{Backend}Outbox` / `Add{Backend}Inbox` plus connection-name overloads); spec 0028 makes no new design decisions about that surface. Survey sibling backends for the canonical shape.

A minimum-viable backend implementing this checklist will compose with `BoxProvisioningHostedService` and the rest of the BoxProvisioning DI graph without further wiring.

---

## Consequences

### Positive

- **RDD discipline visibly applied**. Each per-backend class names the role it plays via an `IAmA*` interface (or, in the case of provisioners, the existing `IAmABoxProvisioner`). Contributors surveying the surface see the roles, not just the implementations.
- **OO over procedural**. Detection helpers, catalogues, validators, and the new UoW are instance classes that hold related state and substitute for testing. The shift away from `static class` aligns with the project's broader OO orientation.
- **Test substitutability returns**. Instance interfaces can be substituted with fakes in unit tests. The original spec 0027 design assumed integration-only testing for these surfaces; spec 0028 reopens the option of unit-level tests where useful (e.g. exercising a runner against a fake detection helper to test edge cases without a live database).
- **Multi-target compatible**. All new role interfaces compile and run on netstandard2.0, net462, net8.0, net9.0, and net10.0 — the existing TFM matrix is unchanged (per C6).
- **Lock+tx ordering encapsulated**. The MSSQL-vs-PG ordering difference is hidden inside per-backend UoW classes. The runner base class never observes lock-vs-tx ordering — eliminating a contradiction the static-virtual design tried unsuccessfully to express in a single hook order.
- **Documented extensibility**. The "How to add a new BoxProvisioning backend" section becomes the entry point for future backends. A contributor implementing Oracle / DB2 / CockroachDB has a checklist, not a survey of sibling files.
- **Algorithm centralised**. The four relational runners' shared try/catch/finally + UoW + dispatch + history-table-ensure code lives in `RelationalBoxMigrationRunnerBase` once. Future cross-backend changes (logging conventions, disposal contracts, cancellation handling) are made once.
- **Diagnostic information preserved**. MySQL's `RELEASE_LOCK` tri-state (`1`/`0`/`NULL`) distinction stays inside `MySqlProvisioningUnitOfWork` — no harmonisation regression. The harmonised contract is at the UoW lifecycle level, not at the lock-return-type level.
- **TOCTOU re-detection promoted to base**. The "re-read state under the lock" pattern that ADR 0057 §3 prescribes is now part of the base algorithm, not per-backend convention.
- **Spec 0027's surface improvements compose cleanly**. Items A (MySQL 64-char guard), E (MSSQL `(int)TotalMilliseconds` overflow), Q (`Identifiers.AssertSafe`), L (redundant `IsMigrationAppliedAsync` removal) all sit naturally in the new base class without rework.

### Negative

- **More public types**. Five new interfaces (`IAmABoxMigrationDetectionHelper<TConnection, TTransaction>`, `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>`, `IAmABoxMigrationCatalog`, `IAmABoxPayloadModeValidator<TConnection>`, `IAmAProvisioningUnitOfWork<TTransaction>`) + one new abstract base class (`RelationalBoxMigrationRunnerBase<TConnection, TTransaction>`) + one new instance class per backend per role (~25 new classes total). Each carries an XML-doc + adversarial-review burden.
- **Source-breaks across spec 0027 surface**. Detection helpers, catalogues, payload validators all change from `static class` to `public class` (instance). Call-sites must construct or DI-receive instead of calling static methods. Detection helper methods on SQLite/Spanner gain a `string?` schema parameter; on MSSQL/PG/MySQL widen `string` schema to `string?`. Each backend's runner ctor changes (now derived from a base class taking the detection helper as a dependency). Acceptable per NF1 (spec 0027 unshipped) but enumerated in `release_notes.md`.
- **MySQL/Spanner declare a `TTransaction` slot they ignore**. Per A.1, MySQL and Spanner implement the detection helper with `MySqlTransaction` / `SpannerTransaction` respectively but never consume the parameter. Mitigation: XML-doc on each implementing class documents the parameter as ignored; the default of `null` keeps non-transactional call-sites ceremony-free. The same applies to MySQL's UoW returning `null` for `Transaction`.
- **MySQL UoW Commit/Rollback are no-op transactionally**. The UoW does still acquire/release the MySQL `GET_LOCK` advisory lock; the no-op is only on the transaction side. XML-doc on `MySqlProvisioningUnitOfWork` explicitly states "Commit/Rollback are no-op for the transaction (per ADR 0057 §5a MySQL DDL auto-commits); they release the GET_LOCK advisory lock and log per spec 0027 Item M".
- **Two detection-helper interfaces for one role**. `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` + `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` is two interfaces where one might suffice. Justified by the Spanner exemption being load-bearing — Spanner can't implement `DetectCurrentVersionAsync` honestly.
- **DI registration overhead**. Each backend adds 4–6 instance classes to register (detection helper, catalogue × 2, payload validator, UoW, runner). The existing `Add{Backend}Outbox` / `Add{Backend}Inbox` extensions absorb this — call-sites are unaffected.
- **Adversarial-review burden**. Six new types (interfaces + base) all carry the XML-doc and review cost typical for new public API.

### Risks and Mitigations

- **Risk: the abstract base class becomes a bottleneck for backend-specific quirks.** A future backend may have a quirk that doesn't fit the UoW-lifecycle shape.
  - **Mitigation**: that backend can implement `IAmABoxMigrationRunner` directly (Spanner pattern) and skip the base class. The base class is *a* path, not *the* path.
- **Risk: two-interface detection-helper split breeds inconsistency.** A future helper method that applies to relational-only might add a third interface.
  - **Mitigation**: the inheritance relationship (`IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` extends `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>`) sets the precedent; future extensions follow the same pattern. Documented in §A.4.
- **Risk: refactor breaks behaviour silently.** Spec 0027 has 198+ tests across the BoxProvisioning packages; a refactor of the runners + introduction of UoW could regress without test failure if a hook order is wrong or a UoW lifecycle step is missed.
  - **Mitigation**: refactor lands as Tidy First — structural-only commits with the existing tests passing before and after; behavioural changes (the diagnostic-contract harmonisation in §B.3, the TOCTOU re-detection promotion to base) are separate commits with new `/test-first` tests. Every UoW class gets its own integration-test coverage.
- **Risk: PR #4039 diff explodes**. Spec 0028 is a substantial refactor on top of spec 0027's already-large diff; instance-based interfaces touch more files than the static-virtual approach would have.
  - **Mitigation**: spec 0028 is treated as review feedback on the same PR rather than greenfield work (per requirements C1) — re-opening the diff is the accepted cost of catching the RDD gap before merge rather than after. Clarity for reviewers is the responsibility of the PR description maintained during the implementation phase.
- **Risk: Naming asymmetry between §B.2 (`RelationalBoxMigrationRunnerBase`) and §B.5 (`SqlBoxProvisioner`)** — a contributor pattern-matching from one base to find the sibling will not find them adjacent in the namespace.
  - **Mitigation**: time-bounded by PR #4039. The PR description carries a "Post-merge follow-up" bullet committing to a successor ADR that renames `RelationalBoxMigrationRunnerBase` to a `Sql*`-prefixed equivalent for symmetry, authored before any third-party adopter ships against either base. The asymmetry exists for the duration of PR #4039 only; once merged, the follow-up ADR is opened immediately. Sub-phase A introduces the right §B.5 name now (per Naming §NF8) and refuses to entrench the imprecision in §B.2's name by propagating it; the symmetric end-state is `SqlBoxProvisioner` + `SqlBoxMigrationRunner` (or whichever canonical name the follow-up ADR settles on).

## Alternatives Considered

### Alternative: static virtual interface members (C# 11+)

**Considered for**: detection helpers, migration catalogues, payload-mode validators (A.1, A.2, A.3).

**Rejected because**: static virtual / static abstract interface members require .NET 7+. The shared assembly `Paramore.Brighter.BoxProvisioning` targets `netstandard2.0;net8.0;net9.0;net10.0` and the MSSQL package targets `net462;net8.0;net9.0;net10.0` (per `src/Directory.Build.props`). Static-virtual interfaces would not compile on netstandard2.0 or net462 — forcing either a TFM change (prohibited by C6) or a `#if NET8_0_OR_GREATER` gate that fragments the public surface across runtimes.

There is precedent for this constraint in the existing codebase: `IAmABoxMigration.LogicalColumns` is typed `IReadOnlyCollection<string>` rather than the tighter `IReadOnlySet<string>` because the latter is unavailable on netstandard2.0. Adding static-virtual interfaces would have broken the same constraint.

**Trade-off**: instance methods carry slightly more overhead per call (virtual dispatch through an instance) than static abstract dispatch. Given that detection helpers and validators run a handful of times per migration, this is unmeasurable.

### Alternative: instance interfaces with DI-registered default implementations

**Considered for**: as a *style* choice within instance interfaces — register each impl as a singleton in DI vs. construct directly.

**Decision**: each per-backend `Add{Backend}Outbox` / `Add{Backend}Inbox` extension registers the instance classes as singletons (they are stateless after construction, so singleton lifetime is correct). The runner constructor receives the detection helper via DI; it instantiates its own UoW per call to `MigrateAsync` (UoW is per-migration, not singleton).

**Trade-off accepted**: 4 DI registrations per backend (one for the detection helper, two for catalogues, one for validator). The UoW is deliberately not registered — it is per-migration, not singleton, and the runner instantiates it directly inside `CreateUnitOfWorkAsync`. Hidden inside the existing DI extensions; call-sites are unaffected.

### Alternative: single detection-helper interface with `DetectCurrentVersionAsync` throwing on Spanner

**Rejected because**: forces Spanner to advertise a method it has no business advertising. Throwing `NotSupportedException` from a method declared in the role is a leak of implementation detail into the contract.

### Alternative: single-generic detection helper `IAmABoxMigrationDetectionHelper<TConnection>` (no `TTransaction`)

**Considered for**: A.1 — keeping the interface simple by omitting the transaction generic.

**Rejected because**: relational backends (MSSQL, Postgres, SQLite) commonly run detection methods inside the migration's transaction scope. Without a `TTransaction` slot on the role contract, those backends would need transaction-bearing overloads on the implementing class, and callers that hold a transaction would have to dispatch through implementation-typed references rather than through the role interface — defeating polymorphism. Critically, the runner base in §B.2 needs to forward `uow.Transaction` (typed) to the detection helper without casting; the two-generic shape makes this work.

**Trade-off accepted**: MySQL and Spanner declare a `TTransaction` slot they don't use. Documented under Negative consequences and on each implementing class's XML-doc.

### Alternative: separate `AcquireLock` and `BeginUnitOfWork` hooks on the runner base (no UoW interface)

**Considered for**: B.1 — exposing the lock and transaction as two ordered hooks rather than encapsulating them inside a UoW.

**Rejected because**: the four backends do not share an order. MSSQL requires `BeginTransaction → AcquireLock` (its lock is transaction-scoped); PG and MySQL acquire the lock first (their locks are session/connection-scoped); SQLite folds both into `BEGIN IMMEDIATE` (no separate lock at all). Any prescribed order on the base would either contradict MSSQL or force MSSQL into a different scoping (`@LockOwner='Session'` instead of `'Transaction'`) — which would invent a new lock primitive, violating OoS6. The UoW abstraction sidesteps this by hiding the ordering inside the per-backend UoW class.

A simpler alternative — **swap the order universally to `BeginUnitOfWork → AcquireLock`** — works on the merits (MSSQL becomes natural, PG just changes idiom, MySQL's `BeginUnitOfWork` is a no-op, SQLite's `AcquireLock` is a no-op because the lock was taken in BEGIN IMMEDIATE) but leaves two no-op hooks visible on the base class. The UoW abstraction collapses the two no-ops into a single `BeginAsync` per backend that does whatever each backend actually needs.

### Alternative: two abstract base classes for the runner — transactional + non-transactional

**Rejected** per §B.2: requirement F6 specifies a single abstract base across the four relational backends; a two-tier hierarchy forces MySQL to re-implement the lock/dispatch/history-table machinery in the non-transactional sibling base, defeating the unification goal. The UoW abstraction in §B.1 absorbs MySQL's transaction-less degeneracy at the right level — inside a per-backend UoW class — without dragging it into the base class signature.

### Alternative: fold MySQL into a separate non-abstract free-standing runner (not derived from the base)

**Considered for**: MySQL's transaction-less awkwardness.

**Rejected because**: MySQL's runner shares the lock-acquire / dispatch / history-table-ensure / fresh-bootstrap-normal-path-selection logic with the other three. Excluding it would leave a 75%-shared-50%-of-the-runners base — dilute and unconvincing. Under the UoW design, MySQL's UoW class is the small honest place where its no-op transaction lives; the runner base treats MySQL identically to the others.

### Alternative: defer the open-closed sweep (F9) to a follow-up spec

**Rejected**: the survey of "other open-closed sweep candidates" is small, fits in one §B.4 table, and recording the empty-result decision in the same ADR is honest. Punting to a future spec creates an open thread that may never close.

## References

- **Requirements**: [specs/0028-box-provisioning-rdd-role-interfaces/requirements.md](../../specs/0028-box-provisioning-rdd-role-interfaces/requirements.md)
- **Spec README**: [specs/0028-box-provisioning-rdd-role-interfaces/README.md](../../specs/0028-box-provisioning-rdd-role-interfaces/README.md)
- **Predecessor ADR**: [0057 Box Schema Versioning and Migrations](0057-box-schema-versioning-and-migrations.md) — defines the per-backend runners, the Spanner degenerate-runner exemption (§6), and the per-backend advisory-lock abstractions (§5b).
- **Predecessor ADR**: [0053 Box Database Migration](0053-box-database-migration.md) — defines `IAmABoxProvisioner` and the BoxProvisioning DI shape.
- **Design principles**: [.agent_instructions/design_principles.md](../../.agent_instructions/design_principles.md) — RDD; `IAmA*` naming convention; responsibilities of "knowing", "doing", and "deciding".
- **External**: [Michael Nygard, *Documenting Architecture Decisions*](http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions) — ADR template.
- **PR**: [#4039](https://github.com/BrighterCommand/Brighter/pull/4039) — review feedback dated 2026-05-07 motivating this ADR.

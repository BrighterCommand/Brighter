#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Template-method base class implementing <see cref="IAmABoxMigrationRunner"/> for the
/// four relational backends (MSSQL, Postgres, MySQL, SQLite). The base owns the
/// success/failure orchestration; derived classes supply only the irreducibly-backend-specific
/// hooks. Spanner does NOT derive from this base — its degenerate fresh-install-only model
/// (per ADR 0057 §6) bypasses the bootstrap branch entirely and implements
/// <see cref="IAmABoxMigrationRunner"/> directly.
/// </summary>
/// <remarks>
/// The success-path order is documented in ADR 0058 §B.3: open connection → create UoW →
/// <c>BeginAsync</c> → ensure history table → re-detect state under the UoW (TOCTOU defence
/// per ADR 0057 §3) → dispatch to one of <c>RunFreshPath</c> / <c>RunBootstrapPath</c> /
/// <c>RunNormalPath</c> → <c>CommitAsync</c>. On any exception thrown between
/// <c>BeginAsync</c> and <c>CommitAsync</c> the runner calls
/// <c>uow.RollbackAsync(CancellationToken.None)</c> and rethrows.
/// </remarks>
/// <typeparam name="TConnection">The backend-specific <see cref="DbConnection"/> subtype.</typeparam>
/// <typeparam name="TTransaction">The backend-specific <see cref="DbTransaction"/> subtype.</typeparam>
public abstract class SqlBoxMigrationRunner<TConnection, TTransaction>
    : IAmABoxMigrationRunner
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    private readonly IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> _detectionHelper;
    private readonly IAmABoxMigrationCatalog _catalog;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly TimeSpan _lockTimeout;
    private readonly MigrationHistoryScope _scope;

    /// <summary>Logger for the runner base AND for derived classes to forward into per-backend UoW construction.</summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Optional tracer for OpenTelemetry instrumentation. When supplied,
    /// <see cref="MigrateAsync"/> wraps each call in a single <see cref="Activity"/> tagged
    /// with backend / table / box-type / chosen-path, and emits child events for the
    /// ensure-history step and whichever of fresh / bootstrap / normal was dispatched.
    /// Null is the default — call-sites that have not opted into instrumentation pay no cost.
    /// </summary>
    protected IAmABrighterTracer? Tracer { get; }

    /// <summary>
    /// The detection helper, exposed to derived classes so the bootstrap-path hook can call
    /// <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}.DetectCurrentVersionAsync"/>.
    /// The base itself uses this helper for the default <see cref="RedetectStateAsync"/> implementation.
    /// </summary>
    protected IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> DetectionHelper => _detectionHelper;

    /// <summary>
    /// The relational database configuration, exposed to derived classes for use inside their
    /// <c>OpenConnectionAsync</c> hook (typically to read <c>ConnectionString</c>) and any other
    /// hook that needs configuration metadata.
    /// </summary>
    protected IAmARelationalDatabaseConfiguration Configuration => _configuration;

    /// <summary>
    /// Per-backend <see cref="Observability.DbSystem"/> classifier surfaced on the migration
    /// span's <see cref="BrighterSemanticConventions.DbSystem"/> tag. Derived production
    /// runners override with the concrete enum value for their backend
    /// (e.g. <see cref="Observability.DbSystem.MsSql"/>). The default
    /// <see cref="Observability.DbSystem.OtherSql"/> keeps test-only subclasses that do not
    /// emit telemetry from having to override; it surfaces as <c>"othersql"</c> on the span.
    /// </summary>
    protected virtual DbSystem DbSystem => DbSystem.OtherSql;

    /// <summary>
    /// Initialises the base runner.
    /// </summary>
    /// <param name="detectionHelper">The version-detecting helper used for the default
    /// <see cref="RedetectStateAsync"/> implementation and exposed to derived classes for
    /// version inference during bootstrap.</param>
    /// <param name="catalog">The migration catalog whose <see cref="IAmABoxMigrationCatalog.All"/>
    /// chain feeds the bootstrap/normal paths and whose
    /// <see cref="IAmABoxMigrationCatalog.FreshInstallDdl"/> feeds the fresh-install fast path.
    /// One catalog per (backend, box-type) — the runner instance is therefore bound to a single
    /// box-type at construction.</param>
    /// <param name="configuration">The relational database configuration, exposed to derived
    /// classes via the <see cref="Configuration"/> property and used to materialise the catalog
    /// chain and fresh-install DDL on each <see cref="MigrateAsync"/> call.</param>
    /// <param name="lockTimeout">How long the per-backend UoW waits for the advisory lock
    /// before throwing.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger.Instance"/>.</param>
    /// <param name="tracer">Optional <see cref="IAmABrighterTracer"/>. When supplied,
    /// <see cref="MigrateAsync"/> emits a migration span on the tracer's
    /// <see cref="System.Diagnostics.ActivitySource"/>. Defaults to null (no instrumentation).</param>
    /// <param name="scope">Controls where the migration-history table is physically placed.
    /// Defaults to <see cref="MigrationHistoryScope.Global"/> (today's behaviour). Threaded on
    /// the same call path as <paramref name="lockTimeout"/>.</param>
    protected SqlBoxMigrationRunner(
        IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        ILogger? logger = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
    {
        _detectionHelper = detectionHelper;
        _catalog = catalog;
        _configuration = configuration;
        _lockTimeout = lockTimeout;
        _scope = scope;
        Logger = logger ?? NullLogger.Instance;
        Tracer = tracer;
    }

    /// <summary>
    /// The configured placement scope for the migration-history table. Exposed to derived classes
    /// for the misconfiguration guard, schema resolution and observability.
    /// </summary>
    protected MigrationHistoryScope Scope => _scope;

    /// <summary>
    /// The backend default schema in which history lives under
    /// <see cref="MigrationHistoryScope.Global"/>: MSSQL <c>"dbo"</c>, PostgreSQL <c>"public"</c>,
    /// or <c>null</c> for backends with no distinct schema concept (MySQL — history lives in the
    /// connection-bound database; SQLite). This is the value the runner used unconditionally
    /// prior to this feature.
    /// </summary>
    protected abstract string? DefaultHistorySchema { get; }

    /// <summary>
    /// Whether this backend honours <see cref="MigrationHistoryScope.PerSchema"/> by placing
    /// history in the configured schema. Overridden to <c>true</c> on MSSQL and PostgreSQL; stays
    /// <c>false</c> on MySQL and SQLite, where <see cref="MigrationHistoryScope.PerSchema"/> is a
    /// no-op (no exception). Spanner does not derive from this base (ADR 0057 §6).
    /// </summary>
    protected virtual bool SupportsPerSchemaHistory => false;

    /// <summary>
    /// Resolves the physical schema that holds the migration-history table for this run. Under
    /// <see cref="MigrationHistoryScope.PerSchema"/> on a placement backend it is the configured
    /// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/> (guaranteed non-null by the
    /// misconfiguration guard); otherwise it is <see cref="DefaultHistorySchema"/> — i.e. today's
    /// behaviour. This is the single source of truth handed to both the write side (CREATE/INSERT)
    /// and the read side (detection helper), so they cannot diverge.
    /// </summary>
    protected string? ResolveHistorySchema() =>
        _scope == MigrationHistoryScope.PerSchema && SupportsPerSchemaHistory
            ? Configuration.SchemaName
            : DefaultHistorySchema;

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        // Defence-in-depth at the framework chokepoint. Catalogs gate AssertSafe at the entry to
        // All(...), but the runner is independently reachable (callers can construct a migration
        // list and invoke MigrateAsync directly), so the public entry point must validate too.
        // schemaName is nullable: SQLite has no schema concept per ADR 0057 §6, so a null value
        // is legitimate and must not be rejected as "missing".
        Identifiers.AssertSafe(tableName, nameof(tableName));
        if (schemaName is not null)
        {
            Identifiers.AssertSafe(schemaName, nameof(schemaName));
        }

        // D3 misconfiguration guard (FR1a/AC1a): on a placement backend, PerSchema needs a schema
        // to place history in. A null SchemaName is an operator misconfiguration — reject it at the
        // entry point rather than silently falling back to Global. Backends that do not support
        // PerSchema (MySQL/SQLite) never trip this; for them PerSchema is a no-op.
        if (Scope == MigrationHistoryScope.PerSchema && SupportsPerSchemaHistory && Configuration.SchemaName is null)
        {
            throw new ConfigurationException(
                "MigrationHistoryScope.PerSchema requires a non-null SchemaName; there is no schema to place history in.");
        }

        // The runner sources its migration chain and fresh-install DDL
        // from the injected catalog rather than via parameter. A catalog returning null for
        // either would surface inside ValidateMigrationsMonotonic / ExecuteFreshInstallAsync as
        // an opaque NRE; replace with a descriptive operator-facing diagnostic. Empty migration
        // lists are intentionally permitted: relational catalogs always return ≥V1, and
        // internal tests use Array.Empty<IAmABoxMigration>() as a "don't care" payload when
        // exercising hook-ordering, re-detection, or failure-path contracts.
        var migrations = _catalog.All(_configuration);
        if (migrations is null)
        {
            throw new ConfigurationException(
                $"Migration list for '{(schemaName is null ? tableName : $"{schemaName}.{tableName}")}' was null. The injected IAmABoxMigrationCatalog must return a non-null list from All(...).");
        }

        ValidateMigrationsMonotonic(schemaName, tableName, migrations);

        // Span name follows OTel DB convention "{operation} {target}" — the table name is the
        // operator-meaningful target. Tags carry backend/schema/box-type so a multi-table
        // startup trace can be filtered without re-parsing the display name.
        using var activity = StartMigrationActivity(tableName, schemaName, boxType);

        // Bootstrap resources are held in locals so the outer try/finally can guarantee
        // disposal whether the body succeeds, the body throws, or the bootstrap itself
        // fails before any disposer would have run. The inner bootstrap try/catch records
        // bootstrap failures (connection refused, lock timeout from BeginAsync per
        // ADR 0058 §B.3) onto the migration activity — without this narrow handler those
        // failures escape with ActivityStatusCode.Unset because the success-path catch
        // only wraps the body.
        // Sync Dispose for the connection: DbConnection does not implement IAsyncDisposable
        // on netstandard2.0, so DisposeAsync is unavailable across the shared-assembly TFM
        // matrix. The UoW implements IAsyncDisposable (see IAmAProvisioningUnitOfWork) and
        // is awaited explicitly in the finally.
        TConnection? connection = null;
        IAmAProvisioningUnitOfWork<TTransaction>? uow = null;
        try
        {
            try
            {
                connection = await OpenConnectionAsync(cancellationToken);
                var lockResource = LockResourceFor(schemaName, tableName);
                uow = await CreateUnitOfWorkAsync(connection, schemaName, tableName, cancellationToken);
                await uow.BeginAsync(lockResource, _lockTimeout, cancellationToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                throw;
            }

            try
            {
                activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventEnsureHistory));
                await EnsureHistoryTableAsync(connection, uow.Transaction, schemaName, cancellationToken);

                var (tableExists, historyExists) = await RedetectStateAsync(
                    connection, uow.Transaction, schemaName, tableName, cancellationToken);

                if (!tableExists)
                {
                    activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "fresh");
                    activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventFreshInstall));
                    // The fresh-install fast path no longer reads migrations[0].UpScript — V1 in
                    // the chain now carries its honest historical baseline DDL (spec 0027 R1).
                    // The live builder DDL comes from the catalog's FreshInstallDdl hook so the
                    // post-install column set always matches V_latest regardless of how the
                    // historical V1 looked.
                    var freshInstallDdl = _catalog.FreshInstallDdl(_configuration);
                    var latestVersion = migrations.Count == 0 ? 0 : migrations[migrations.Count - 1].Version;
                    await RunFreshPathAsync(
                        connection, uow.Transaction, schemaName, tableName,
                        freshInstallDdl, latestVersion, cancellationToken);
                }
                else if (!historyExists)
                {
                    activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "bootstrap");
                    activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventBootstrap));
                    await RunBootstrapPathAsync(
                        connection, uow.Transaction, schemaName, tableName, boxType, migrations, cancellationToken);
                }
                else
                {
                    activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "normal");
                    activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventNormalUpdate));
                    await RunNormalPathAsync(
                        connection, uow.Transaction, schemaName, tableName, migrations, cancellationToken);
                }

                await uow.CommitAsync(cancellationToken);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                // Pass CancellationToken.None: a signalled caller token must not abandon the unwind.
                // See ADR 0058 §B.3 cancellation contract.
                try
                {
                    await uow.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackEx)
                {
                    // Defense in depth: the IAmAProvisioningUnitOfWork contract says RollbackAsync
                    // MUST NOT throw, and the four per-backend impls comply (PG and MySQL tightened
                    // explicitly in 3c8417fd6). If a future regression breaks that, do NOT let the
                    // rollback exception mask the original triggering exception — the primary cause
                    // is more diagnostically valuable than a defect in our own unwind, and callers
                    // pattern-match on the type they actually threw. Log the rollback failure at
                    // Error level so operators see both: the primary failure surfaces via rethrow,
                    // the rollback defect surfaces via the log.
                    Logger.LogError(rollbackEx,
                        "Box migration unit-of-work RollbackAsync threw while unwinding from a primary failure for table '{Table}'; rollback exception is logged, primary exception is rethrown.",
                        tableName);
                }
                throw;
            }
        }
        finally
        {
            // Disposal order mirrors the previous `await using var uow` / `using var connection`
            // pair: UoW first (releases any per-backend advisory lock + transaction state),
            // then the connection. Null checks defend the bootstrap-failure-before-assignment
            // case: if OpenConnectionAsync threw, both locals stay null; if BeginAsync threw,
            // both are assigned and need disposal.
            if (uow is not null)
            {
                await uow.DisposeAsync();
            }
            connection?.Dispose();
        }
    }

    private Activity? StartMigrationActivity(string tableName, string? schemaName, BoxType boxType)
    {
        // ActivitySource.StartActivity returns null when no listener is registered or sampling
        // discards — caller handles null gracefully via the `?.` chain on every Set/AddEvent.
        var activity = Tracer?.ActivitySource.StartActivity(
            $"{BrighterSemanticConventions.BoxMigration} {tableName}",
            ActivityKind.Internal);
        if (activity is null) return null;
        activity.SetTag(BrighterSemanticConventions.DbSystem, DbSystem.ToDbName());
        activity.SetTag(BrighterSemanticConventions.DbTable, tableName);
        if (schemaName is not null)
        {
            // SQLite has no schema concept — leaving the tag out (vs. setting it to "null")
            // keeps a "filter where db.namespace is missing" query meaningful.
            activity.SetTag(BrighterSemanticConventions.DbNamespace, schemaName);
        }
        activity.SetTag(BrighterSemanticConventions.BoxType, boxType.ToString());
        return activity;
    }

    /// <summary>
    /// Opens a fresh <typeparamref name="TConnection"/> ready for use by the migration. Typical
    /// implementation reads <see cref="Configuration"/>.<c>ConnectionString</c> and calls
    /// <see cref="DbConnection.OpenAsync(CancellationToken)"/>.
    /// </summary>
    protected abstract Task<TConnection> OpenConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Constructs the per-backend <see cref="IAmAProvisioningUnitOfWork{TTransaction}"/> over
    /// <paramref name="connection"/>. The runner declares the result with <c>await using</c>
    /// and immediately calls <c>BeginAsync</c>; the implementation should NOT begin the
    /// lock+transaction here, only construct the UoW. The per-invocation
    /// <paramref name="schemaName"/> and <paramref name="tableName"/> are passed explicitly
    /// so backend UoWs that need them at construction (e.g. MySQL's tri-state RELEASE_LOCK
    /// warning emission, which surfaces the raw table name lost to MySqlMigrationLockName.For's
    /// hash-truncation) can capture them directly instead of via cross-hook state.
    /// </summary>
    protected abstract Task<IAmAProvisioningUnitOfWork<TTransaction>> CreateUnitOfWorkAsync(
        TConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken);

    /// <summary>
    /// Computes the backend-specific advisory lock resource string for the given table.
    /// Each backend's lock-resource format differs (MSSQL/PG: <c>BrighterMigration_{schema}.{table}</c>;
    /// MySQL: hashed with 64-char fallback; SQLite: ignored).
    /// </summary>
    protected abstract string LockResourceFor(string? schemaName, string tableName);

    /// <summary>
    /// Ensures the migration-history table exists. Each backend's history-table DDL differs;
    /// the base orchestrates the call but does not own the DDL.
    /// </summary>
    protected abstract Task EnsureHistoryTableAsync(
        TConnection connection, TTransaction? transaction, string? schemaName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies the fresh-install path: executes <paramref name="freshInstallDdl"/> (the live
    /// V_latest-shape DDL sourced from <see cref="IAmABoxMigrationCatalog.FreshInstallDdl"/>)
    /// and stamps history at <paramref name="latestVersion"/>. Invoked when re-detection inside
    /// the UoW reports no table. The fresh path no longer reads the migration chain — the
    /// historical V1 in <see cref="IAmABoxMigrationCatalog.All"/> may carry a pre-V_latest
    /// baseline that does not match the current builder (spec 0027 R1).
    /// </summary>
    protected abstract Task RunFreshPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the bootstrap (legacy-table) path: infers the in-place version via
    /// <c>DetectionHelper.DetectCurrentVersionAsync</c>, writes a synthesised history row, and
    /// catches up with any newer migrations. Invoked when re-detection reports a table without
    /// migration history.
    /// </summary>
    protected abstract Task RunBootstrapPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the normal-update path: walks migrations whose version exceeds the recorded
    /// max and writes a history row per migration. Invoked when re-detection reports a table
    /// with existing migration history.
    /// </summary>
    protected abstract Task RunNormalPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken);

    /// <summary>
    /// TOCTOU re-detection under the UoW. Default implementation calls the injected
    /// detection helper (<see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}.DoesTableExistAsync"/>
    /// then <see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}.DoesHistoryExistAsync"/>,
    /// short-circuiting the history check when the table is missing). Derived classes whose
    /// backend has a different detection model can override.
    /// </summary>
    protected virtual async Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, tableName, schemaName, cancellationToken, transaction);
        var historyExists = tableExists && await _detectionHelper.DoesHistoryExistAsync(
            connection, tableName, schemaName, ResolveHistorySchema(), cancellationToken, transaction);
        return (tableExists, historyExists);
    }

    /// <summary>
    /// Rejects duplicate, gap, or out-of-order migration versions before any work begins.
    /// A malformed list corrupts every path branch (PK violation on history insert, skipped
    /// ALTERs, double-applied DDL), so the validation sits at <see cref="MigrateAsync"/> entry.
    /// Lifted from spec 0027 Items H/I/Q. Private because the orchestration is owned by the
    /// base; derived classes implement only the per-backend hooks and have no reason to
    /// re-invoke (or override the algorithm of) the pre-flight monotonicity check.
    /// </summary>
    private static void ValidateMigrationsMonotonic(
        string? schemaName, string tableName, IReadOnlyList<IAmABoxMigration> migrations)
    {
        for (var i = 1; i < migrations.Count; i++)
        {
            var prev = migrations[i - 1].Version;
            var curr = migrations[i].Version;
            if (curr != prev + 1)
            {
                var qualified = schemaName is null ? tableName : $"{schemaName}.{tableName}";
                throw new ConfigurationException(
                    $"Migration list for '{qualified}' is not contiguous and ascending: " +
                    $"V{prev} followed by V{curr} (expected V{prev + 1}).");
            }
        }
    }
}

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
    /// Defaults to <see cref="MigrationHistoryScope.Global"/>.</param>
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
    /// connection-bound database; SQLite).
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
    /// misconfiguration guard); otherwise it is <see cref="DefaultHistorySchema"/>. This is the single source of truth handed to both the write side (CREATE/INSERT)
    /// and the read side (detection helper), so they cannot diverge.
    /// </summary>
    protected string? ResolveHistorySchema() =>
        _scope == MigrationHistoryScope.PerSchema && SupportsPerSchemaHistory
            ? Configuration.SchemaName
            : DefaultHistorySchema;

    /// <inheritdoc />
    public async Task MigrateAsync(
        BoxTableName tableName,
        SchemaName? schemaName,
        BoxType boxType,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        schemaName = GuardIdentifiers(tableName, schemaName);
        ThrowIfPerSchemaHasNoSchema();

        var migrations = GetMigrations(tableName, schemaName);
        ValidateMigrationsMonotonic(schemaName?.Value, tableName.Value, migrations);

        using var activity = StartMigrationActivity(tableName.Value, schemaName?.Value, boxType);

        LogHistoryPlacement(tableName);

        TConnection? connection = null;
        IAmAProvisioningUnitOfWork<TTransaction>? uow = null;
        try
        {
            try
            {
                connection = await OpenConnectionAsync(cancellationToken);
                var lockResource = LockResourceFor(schemaName?.Value, tableName.Value);
                uow = await CreateUnitOfWorkAsync(connection, schemaName?.Value, tableName.Value, cancellationToken);
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
                await EnsureHistoryTableAsync(connection, uow.Transaction, schemaName?.Value, tableName.Value, cancellationToken);

                var (tableExists, historyExists) = await RedetectStateAsync(
                    connection, uow.Transaction, schemaName?.Value, tableName.Value, cancellationToken);

                if (!tableExists)
                {
                    activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "fresh");
                    activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventFreshInstall));
                    var freshInstallDdl = _catalog.FreshInstallDdl(_configuration);
                    var latestVersion = migrations.Count == 0 ? (MigrationVersion)0 : migrations[migrations.Count - 1].Version;
                    await RunFreshPathAsync(
                        connection, uow.Transaction, schemaName?.Value, tableName.Value,
                        freshInstallDdl, latestVersion, cancellationToken);
                }
                else if (!historyExists)
                {
                    activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "bootstrap");
                    activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventBootstrap));
                    await RunBootstrapPathAsync(
                        connection, uow.Transaction, schemaName?.Value, tableName.Value, boxType, migrations, cancellationToken);
                }
                else
                {
                    activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "normal");
                    activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventNormalUpdate));
                    await RunNormalPathAsync(
                        connection, uow.Transaction, schemaName?.Value, tableName.Value, migrations, cancellationToken);
                }

                await uow.CommitAsync(cancellationToken);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                await RollbackWithoutMaskingAsync(uow, tableName);
                throw;
            }
        }
        finally
        {
            await ReleaseAsync(uow, connection);
        }
    }

    // The runner is callable without going through a catalog, so it asserts identifier safety
    // itself. A SchemaName that wraps a null or empty string is absent, the same as a null one.
    private static SchemaName? GuardIdentifiers(BoxTableName tableName, SchemaName? schemaName)
    {
        Identifiers.AssertSafe(tableName.Value, nameof(tableName));

        if (SchemaName.IsNullOrEmpty(schemaName))
        {
            return null;
        }

        Identifiers.AssertSafe(schemaName.Value, nameof(schemaName));
        return schemaName;
    }

    private void ThrowIfPerSchemaHasNoSchema()
    {
        if (Scope == MigrationHistoryScope.PerSchema && SupportsPerSchemaHistory && Configuration.SchemaName is null)
        {
            throw new ConfigurationException(
                "MigrationHistoryScope.PerSchema requires a non-null SchemaName; there is no schema to place history in.");
        }
    }

    // An empty chain is allowed; a null one is a catalog defect, reported here rather than as an
    // NRE further in.
    private IReadOnlyList<IAmABoxMigration> GetMigrations(BoxTableName tableName, SchemaName? schemaName)
    {
        var migrations = _catalog.All(_configuration);
        if (migrations is null)
        {
            throw new ConfigurationException(
                $"Migration list for '{(schemaName is null ? tableName : $"{schemaName}.{tableName}")}' was null. The injected IAmABoxMigrationCatalog must return a non-null list from All(...).");
        }

        return migrations;
    }

    // Lets an operator see where this run's migration history lives without reading the
    // provisioner configuration (ADR 0060).
    private void LogHistoryPlacement(BoxTableName tableName) =>
        Logger.LogInformation(
            "Box migration history for {BoxTable} resolved to schema {HistorySchema} (scope {Scope})",
            tableName, ResolveHistorySchema() ?? "<backend default>", _scope);

    // Rolls back with CancellationToken.None so a cancelled caller cannot abandon the unwind
    // (ADR 0058 §B.3). RollbackAsync must not throw; if it does, the failure is logged rather
    // than allowed to mask the primary exception the caller is about to see.
    private async Task RollbackWithoutMaskingAsync(IAmAProvisioningUnitOfWork<TTransaction> uow, BoxTableName tableName)
    {
        try
        {
            await uow.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackEx)
        {
            Logger.LogError(rollbackEx,
                "Box migration unit-of-work RollbackAsync threw while unwinding from a primary failure for table '{Table}'; rollback exception is logged, primary exception is rethrown.",
                tableName);
        }
    }

    // Releases the unit of work (lock and transaction) before the connection. Either may be null
    // if setup failed part way. The connection is disposed synchronously because DbConnection has
    // no DisposeAsync on netstandard2.0.
    private static async Task ReleaseAsync(IAmAProvisioningUnitOfWork<TTransaction>? uow, TConnection? connection)
    {
        if (uow is not null)
        {
            await uow.DisposeAsync();
        }
        connection?.Dispose();
    }

    // The span name follows the OTel DB convention "{operation} {target}". With no schema,
    // db.namespace is left off rather than set to null, so "namespace is missing" filters work.
    private Activity? StartMigrationActivity(string tableName, string? schemaName, BoxType boxType)
    {
        var activity = Tracer?.ActivitySource.StartActivity(
            $"{BrighterSemanticConventions.BoxMigration} {tableName}",
            ActivityKind.Internal);
        if (activity is null) return null;
        activity.SetTag(BrighterSemanticConventions.DbSystem, DbSystem.ToDbName());
        activity.SetTag(BrighterSemanticConventions.DbTable, tableName);
        if (schemaName is not null)
        {
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
    /// the base orchestrates the call but does not own the DDL. Backends that opt into
    /// <see cref="MigrationHistoryScope.PerSchema"/> placement use <paramref name="tableName"/>
    /// to seed this tenant's prior history rows from a legacy default-schema history table on
    /// every PerSchema provision run (ADR 0060 D5); the per-row <c>NOT EXISTS</c> guard makes
    /// steady-state runs a zero-row no-op while still allowing each box-type that flips later
    /// to seed its own row.
    /// </summary>
    protected abstract Task EnsureHistoryTableAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies the fresh-install path: executes <paramref name="freshInstallDdl"/> (the live
    /// V_latest-shape DDL sourced from <see cref="IAmABoxMigrationCatalog.FreshInstallDdl"/>)
    /// and stamps history at <paramref name="latestVersion"/>. Invoked when re-detection inside
    /// the UoW reports no table. The fresh path does not read the migration chain: the
    /// historical V1 in <see cref="IAmABoxMigrationCatalog.All"/> may carry an older baseline
    /// that does not match the current builder.
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

    // Rejects duplicate, gap or out-of-order versions before any work begins: a malformed chain
    // corrupts every path (PK violation on history insert, skipped ALTERs, double-applied DDL).
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

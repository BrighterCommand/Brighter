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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Runs box migrations against a MSSQL database. Derives from
/// <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/> for the
/// success/failure orchestration and supplies the per-backend hooks. Uses an injected
/// <see cref="IMsSqlAdvisoryLock"/> (default <see cref="MsSqlAdvisoryLock"/>) for
/// concurrency control via <see cref="MsSqlProvisioningUnitOfWork"/>; the dispatch into
/// fresh / bootstrap / normal paths happens in the base after re-detection under the UoW.
/// </summary>
public class MsSqlBoxMigrationRunner : SqlBoxMigrationRunner<SqlConnection, SqlTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";
    // The history table is global — one row per (SchemaName, BoxTableName, MigrationVersion)
    // tracking migrations across every box-table schema. It always lives in [dbo] regardless of
    // the connection's default schema or the configured box schema.
    private const string HISTORY_TABLE_SCHEMA = "dbo";

    // Lock-timeout validation lives inside MsSqlAdvisoryLock.AcquireAsync (per ADR 0057 §5b)
    // so any caller of the abstraction is protected. A bad timeout surfaces as
    // ArgumentOutOfRangeException on first MigrateAsync call rather than at construction.
    private readonly IMsSqlAdvisoryLock _advisoryLock;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public MsSqlBoxMigrationRunner(
        MsSqlBoxDetectionHelper detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        IMsSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : base(detectionHelper, catalog, configuration, lockTimeout ?? TimeSpan.FromSeconds(30),
            logger ?? ApplicationLogging.CreateLogger<MsSqlBoxMigrationRunner>(),
            tracer, scope)
    {
        _advisoryLock = advisoryLock ?? new MsSqlAdvisoryLock();
    }

    /// <summary>
    /// Convenience ctor used by the <c>AddMsSqlOutbox</c>/<c>AddMsSqlInbox</c> registration
    /// extensions: synthesises a default <see cref="MsSqlBoxDetectionHelper"/> so the
    /// extension method doesn't have to resolve it from the container before the catalog
    /// is in scope.
    /// </summary>
    public MsSqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IMsSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : this(new MsSqlBoxDetectionHelper(), catalog, configuration, advisoryLock, logger, lockTimeout, tracer, scope)
    {
    }

    /// <inheritdoc />
    protected override DbSystem DbSystem => DbSystem.MsSql;

    /// <inheritdoc />
    protected override string? DefaultHistorySchema => HISTORY_TABLE_SCHEMA;

    /// <inheritdoc />
    protected override bool SupportsPerSchemaHistory => true;

    // ==== Hook overrides — Phase 7.1a delegates to legacy helpers ====

    protected override async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<SqlTransaction>> CreateUnitOfWorkAsync(
        SqlConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
        => Task.FromResult<IAmAProvisioningUnitOfWork<SqlTransaction>>(
            new MsSqlProvisioningUnitOfWork(connection, _advisoryLock, Logger));

    // Include the schema name so that two same-named tables in different schemas
    // (e.g. dbo.Outbox and billing.Outbox) acquire distinct advisory locks. Without
    // the schema qualifier they would share a lock and serialize unnecessarily.
    protected override string LockResourceFor(string? schemaName, string tableName)
        => $"BrighterMigration_{schemaName ?? HISTORY_TABLE_SCHEMA}.{tableName}";

    protected override async Task EnsureHistoryTableAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Filter sys.tables by both name AND schema_id — without the schema_id filter the
        // existence check misfires when any other schema happens to contain a table by that name,
        // skipping the [dbo] create and breaking subsequent INSERT/SELECT statements.
        // The WHERE values are parameterised (matching DoesTableExistAsync); the bracketed
        // identifiers in the CREATE TABLE body must remain inline because T-SQL DDL does not
        // accept bind parameters for object names.
        // SET XACT_ABORT OFF is issued defensively so the 2714 swallow below works for callers
        // who pre-enable XACT_ABORT ON on their connection pool (some ORM defaults, startup
        // scripts). With XACT_ABORT ON, 2714 dooms the transaction rather than being statement-
        // terminating, and RedetectStateAsync would fail with state error 3930. We restore the
        // session default OFF for the runner's own connection only — scope is bounded to this
        // command. Per PR #4039 reviewer item F2-3.
        command.CommandText = $@"
SET XACT_ABORT OFF;
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = @HistoryTableName AND schema_id = SCHEMA_ID(@HistorySchema)
)
BEGIN
    CREATE TABLE [{HISTORY_TABLE_SCHEMA}].[{MIGRATION_HISTORY_TABLE}] (
        [MigrationVersion] INT NOT NULL,
        [SchemaName] VARCHAR(256) NOT NULL DEFAULT 'dbo',
        [BoxTableName] VARCHAR(256) NOT NULL,
        [Description] NVARCHAR(512) NOT NULL,
        [AppliedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_BrighterMigrationHistory
            PRIMARY KEY ([SchemaName], [BoxTableName], [MigrationVersion])
    );
END";
        command.Parameters.AddWithValue("@HistoryTableName", MIGRATION_HISTORY_TABLE);
        command.Parameters.AddWithValue("@HistorySchema", HISTORY_TABLE_SCHEMA);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 2714)
        {
            // TOCTOU on sys.tables: the per-table sp_getapplock above does not cover the shared
            // __BrighterMigrationHistory, so two concurrent provisioners with different table
            // names (e.g. outbox + inbox) can both pass the IF NOT EXISTS check and both issue
            // CREATE TABLE; the loser hits 2714 ("There is already an object named ..."). 2714
            // is a statement-terminating error with default XACT_ABORT OFF — the transaction is
            // not doomed, so we can ignore it and continue. The history table now exists with
            // the schema we intended (the racing session ran the same DDL).
            //
            // Surface the swallow so operators investigating "did two replicas race here?" have
            // a signal — Debug-level log + Activity event on the migration span (when one is
            // active). Silent swallow was the prior behaviour; per PR #4039 review item #7 the
            // race is real and the swallow is intentional, but operators got no signal that
            // two racers serialised here.
            Logger.LogDebug(ex,
                "{HistoryTable} already created by racing session (MsSql 2714)",
                MIGRATION_HISTORY_TABLE);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
    }

    protected override async Task RunFreshPathAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        // Execute the V_latest-shape DDL sourced from IAmABoxMigrationCatalog.FreshInstallDdl
        // (the live builder DDL — typically <Backend>OutboxBuilder.GetDDL(...) for outbox /
        // <Backend>InboxBuilder.GetDDL(...) for inbox). We stamp directly at V_latest with a
        // "fresh install" marker — the V2..V_latest ALTERs in the chain would be no-ops on the
        // V_latest-shape table, so we skip the chain entirely (spec 0027 R1 fresh-install fast
        // path per ADR §3).
        await ExecuteDdlAsync(connection, transaction!, freshInstallDdl, cancellationToken);

        await InsertHistoryRowAsync(
            connection, transaction!, effectiveSchema, tableName,
            latestVersion, $"fresh install at V{latestVersion}", cancellationToken);
    }

    protected override async Task RunBootstrapPathAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        var detected = await DetectionHelper.DetectCurrentVersionAsync(
            connection, tableName, effectiveSchema, boxType, migrations, cancellationToken, transaction);

        if (detected == -1)
        {
            var discriminator = DetectionHelper.DiscriminatorFor(boxType);
            throw new ConfigurationException(
                $"Table '{effectiveSchema}.{tableName}' is not a Brighter {boxType.ToString().ToLowerInvariant()}: " +
                $"missing discriminator column '{discriminator}'.");
        }

        if (detected == 0)
        {
            throw new ConfigurationException(
                $"Table '{effectiveSchema}.{tableName}' does not match any known schema version. " +
                $"Cannot bootstrap a Brighter {boxType.ToString().ToLowerInvariant()} from an unrecognised column set.");
        }

        await InsertHistoryRowAsync(
            connection, transaction!, effectiveSchema, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, transaction!, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction!, effectiveSchema, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    protected override async Task RunNormalPathAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        var maxVersion = await DetectionHelper.GetMaxVersionAsync(
            connection, tableName, effectiveSchema, ResolveHistorySchema(), cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ExecuteUpScriptAsync(connection, transaction!, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction!, effectiveSchema, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static Task ExecuteUpScriptAsync(
        SqlConnection connection, SqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
        => ExecuteDdlAsync(connection, transaction, migration.UpScript, cancellationToken);

    private static async Task ExecuteDdlAsync(
        SqlConnection connection, SqlTransaction transaction,
        string ddl, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertHistoryRowAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO [{HISTORY_TABLE_SCHEMA}].[{MIGRATION_HISTORY_TABLE}] ([MigrationVersion], [SchemaName], [BoxTableName], [Description])
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

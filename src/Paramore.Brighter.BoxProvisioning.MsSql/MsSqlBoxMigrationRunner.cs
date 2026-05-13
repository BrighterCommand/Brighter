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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

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
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public MsSqlBoxMigrationRunner(
        MsSqlBoxDetectionHelper detectionHelper,
        IAmARelationalDatabaseConfiguration configuration,
        IMsSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null)
        : base(detectionHelper, configuration, lockTimeout ?? TimeSpan.FromSeconds(30), logger)
    {
        _advisoryLock = advisoryLock ?? new MsSqlAdvisoryLock();
        _logger = logger ?? ApplicationLogging.CreateLogger<MsSqlBoxMigrationRunner>();
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises a default
    /// <see cref="MsSqlBoxDetectionHelper"/>; removed when DI cascade lands in Phase 9.
    /// </summary>
    public MsSqlBoxMigrationRunner(
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IMsSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null)
        : this(new MsSqlBoxDetectionHelper(), configuration, advisoryLock, logger, lockTimeout)
    {
    }

    // ==== Hook overrides — Phase 7.1a delegates to legacy helpers ====

    protected override async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<SqlTransaction>> CreateUnitOfWorkAsync(
        SqlConnection connection, CancellationToken cancellationToken)
        => Task.FromResult<IAmAProvisioningUnitOfWork<SqlTransaction>>(
            new MsSqlProvisioningUnitOfWork(connection, _advisoryLock, _logger));

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
        command.CommandText = $@"
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
        }
    }

    protected override async Task RunFreshPathAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        if (migrations.Count == 0) return;

        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        // V1's UpScript IS the live builder DDL (V_latest-shape per ADR §3 fresh-install fast
        // path). A list whose first entry is anything other than V1 would silently install the
        // wrong schema, so reject it before any DDL fires.
        if (migrations[0].Version != 1)
            throw new ConfigurationException(
                $"Cannot install '{effectiveSchema}.{tableName}' from a fresh state: " +
                $"the first migration must be V1, but the supplied migrations list starts at V{migrations[0].Version}.");

        // We stamp directly at V_latest with a "fresh install" marker — V2..V_latest ALTERs
        // would be no-ops on the V_latest-shape table, so we skip them.
        await ExecuteUpScriptAsync(connection, transaction!, migrations[0], cancellationToken);

        var latest = migrations[migrations.Count - 1];
        await InsertHistoryRowAsync(
            connection, transaction!, effectiveSchema, tableName,
            latest.Version, $"fresh install at V{latest.Version}", cancellationToken);
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
            connection, tableName, effectiveSchema, cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ExecuteUpScriptAsync(connection, transaction!, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction!, effectiveSchema, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static async Task ExecuteUpScriptAsync(
        SqlConnection connection, SqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = migration.UpScript;
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

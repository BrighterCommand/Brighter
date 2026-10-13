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
    // The Global history schema.
    private const string HISTORY_TABLE_SCHEMA = "dbo";

    private const int ObjectAlreadyExists = 2714;
    private const int PermissionDeniedOnObject = 229;
    private const int PermissionDeniedOnColumn = 230;

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
    /// Initialises the runner with a default <see cref="MsSqlBoxDetectionHelper"/>.
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

    /// <inheritdoc />
    /// <remarks>
    /// Includes the schema, so same-named tables in different schemas (<c>dbo.Outbox</c>,
    /// <c>billing.Outbox</c>) take different locks.
    /// </remarks>
    protected override string LockResourceFor(string? schemaName, string tableName)
        => $"BrighterMigration_{schemaName ?? HISTORY_TABLE_SCHEMA}.{tableName}";

    protected override async Task EnsureHistoryTableAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        var historySchema = ResolveHistoryTableSchema();
        var historyIsSeparateFromLegacy = !string.Equals(historySchema, HISTORY_TABLE_SCHEMA, StringComparison.OrdinalIgnoreCase);

        await CreateHistoryTableAsync(connection, transaction, historySchema, cancellationToken);

        if (historyIsSeparateFromLegacy)
        {
            await SeedHistoryFromLegacyAsync(
                connection, transaction, historySchema, schemaName ?? historySchema, tableName,
                cancellationToken);
        }
    }

    // Another box's provisioner can race this CREATE (ADR 0057 §3); the loser's 2714 is swallowed.
    // SET XACT_ABORT OFF keeps 2714 statement-terminating even if the pooled connection arrives
    // with XACT_ABORT ON, which would doom the transaction.
    private async Task CreateHistoryTableAsync(
        SqlConnection connection, SqlTransaction? transaction, string historySchema,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
SET XACT_ABORT OFF;
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = @HistoryTableName AND schema_id = SCHEMA_ID(@HistorySchema)
)
BEGIN
    CREATE TABLE [{historySchema}].[{MIGRATION_HISTORY_TABLE}] (
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
        command.Parameters.AddWithValue("@HistorySchema", historySchema);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == ObjectAlreadyExists)
        {
            Logger.LogDebug(ex,
                "{HistoryTable} already created by racing session (MsSql 2714)",
                MIGRATION_HISTORY_TABLE);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
    }

    private async Task<bool> DoesHistoryTableExistAsync(
        SqlConnection connection, SqlTransaction? transaction, string schema,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT CASE WHEN EXISTS(" +
            "SELECT 1 FROM sys.tables t " +
            "INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
            "WHERE t.name = @HistoryTableName AND s.name = @SchemaName" +
            ") THEN 1 ELSE 0 END";
        command.Parameters.AddWithValue("@HistoryTableName", MIGRATION_HISTORY_TABLE);
        command.Parameters.AddWithValue("@SchemaName", schema);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && (int)result == 1;
    }

    // Copies this box's rows from the legacy dbo history table into the per-schema one
    // (ADR 0060 D5). Runs on every PerSchema provision, not only the first: each box type that
    // flips must seed its own rows, and the NOT EXISTS guard makes a re-run copy nothing. All five
    // columns are copied, so a seeded row is indistinguishable from the original. No legacy table
    // means no Global predecessor, so nothing to copy.
    private async Task SeedHistoryFromLegacyAsync(
        SqlConnection connection, SqlTransaction? transaction,
        string perSchema, string boxSchema, string boxTableName,
        CancellationToken cancellationToken)
    {
        const string legacySchema = HISTORY_TABLE_SCHEMA;
        bool legacyExists;
        try
        {
            legacyExists = await DoesHistoryTableExistAsync(connection, transaction, legacySchema, cancellationToken);
        }
        catch (SqlException ex) when (IsLegacyHistoryReadPermissionDenied(ex))
        {
            throw BuildLegacyHistoryReadDeniedException(legacySchema, perSchema, ex);
        }
        if (!legacyExists)
        {
            return;
        }

        Identifiers.AssertSafe(boxSchema, nameof(boxSchema));

        int rowsCopied;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $@"
INSERT INTO [{perSchema}].[{MIGRATION_HISTORY_TABLE}]
    ([MigrationVersion], [SchemaName], [BoxTableName], [Description], [AppliedAt])
SELECT src.[MigrationVersion], src.[SchemaName], src.[BoxTableName], src.[Description], src.[AppliedAt]
FROM [{legacySchema}].[{MIGRATION_HISTORY_TABLE}] AS src
WHERE src.[SchemaName] = @SchemaName
  AND src.[BoxTableName] = @BoxTableName
  AND NOT EXISTS (
      SELECT 1 FROM [{perSchema}].[{MIGRATION_HISTORY_TABLE}] AS tgt
      WHERE tgt.[SchemaName] = src.[SchemaName]
        AND tgt.[BoxTableName] = src.[BoxTableName]
        AND tgt.[MigrationVersion] = src.[MigrationVersion])";
            command.Parameters.AddWithValue("@SchemaName", boxSchema);
            command.Parameters.AddWithValue("@BoxTableName", boxTableName);
            try
            {
                rowsCopied = await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex) when (IsLegacyHistoryReadPermissionDenied(ex))
            {
                throw BuildLegacyHistoryReadDeniedException(legacySchema, perSchema, ex);
            }
        }

        if (rowsCopied > 0)
        {
            Logger.LogInformation(
                "Seeded {RowCount} legacy history row(s) for {BoxTable} from {LegacySchema} to {TargetSchema}",
                rowsCopied, boxTableName, legacySchema, perSchema);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventLegacyHistorySeeded,
                default,
                new ActivityTagsCollection
                {
                    { BrighterSemanticConventions.BoxMigrationSeedRowCount, rowsCopied }
                }));
        }
    }

    // Only a missing privilege means "grant SELECT"; any other failure propagates unchanged.
    private static bool IsLegacyHistoryReadPermissionDenied(SqlException ex) =>
        ex.Number == PermissionDeniedOnObject || ex.Number == PermissionDeniedOnColumn;

    private static ConfigurationException BuildLegacyHistoryReadDeniedException(
        string legacySchema, string perSchema, Exception inner) =>
        new ConfigurationException(
            $"Brighter PerSchema migration: every provision run reads the legacy default-schema " +
            $"history table [{legacySchema}].[{MIGRATION_HISTORY_TABLE}] so any unseeded tenant " +
            $"rows can be copied into the per-schema history (the per-row NOT EXISTS guard makes " +
            $"steady-state runs a zero-row no-op, but the SELECT against the legacy table runs " +
            $"every time). Grant the runner SELECT on that table (and INSERT on " +
            $"[{perSchema}].[{MIGRATION_HISTORY_TABLE}]) for the lifetime of the PerSchema " +
            $"deployment and retry. The original provider exception is preserved as the inner " +
            $"exception.",
            inner);

    protected override async Task RunFreshPathAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

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
                migration.Version, migration.Description.Value, cancellationToken);
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
                migration.Version, migration.Description.Value, cancellationToken);
        }
    }

    private static Task ExecuteUpScriptAsync(
        SqlConnection connection, SqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
        => ExecuteDdlAsync(connection, transaction, migration.UpScript.Value, cancellationToken);

    private static async Task ExecuteDdlAsync(
        SqlConnection connection, SqlTransaction transaction,
        string ddl, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Never null here: DefaultHistorySchema is "dbo", and the base rejects PerSchema without a
    // SchemaName before migrating. Validated because it is inlined into the SQL.
    private string ResolveHistoryTableSchema()
    {
        var historySchema = ResolveHistorySchema()!;
        Identifiers.AssertSafe(historySchema, nameof(historySchema));
        return historySchema;
    }

    private async Task InsertHistoryRowAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        var historySchema = ResolveHistoryTableSchema();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO [{historySchema}].[{MIGRATION_HISTORY_TABLE}] ([MigrationVersion], [SchemaName], [BoxTableName], [Description])
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

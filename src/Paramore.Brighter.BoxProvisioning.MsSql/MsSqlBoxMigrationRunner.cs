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
/// Runs box migrations against a MSSQL database. Uses an injected
/// <see cref="IMsSqlAdvisoryLock"/> (default <see cref="MsSqlAdvisoryLock"/>) for concurrency
/// control and a single transaction for all-or-nothing semantics. After acquiring the lock
/// the runner re-reads box-table state under the lock and dispatches into one of three paths
/// (fresh / bootstrap / normal) per ADR 0057 §3 — the caller's <see cref="BoxTableState"/>
/// is treated as a stale hint to defeat TOCTOU races.
/// </summary>
public class MsSqlBoxMigrationRunner(
    IAmARelationalDatabaseConfiguration configuration,
    TimeSpan lockTimeout,
    IMsSqlAdvisoryLock? advisoryLock = null,
    ILogger? logger = null) : IAmABoxMigrationRunner
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";
    // The history table is global — one row per (SchemaName, BoxTableName, MigrationVersion)
    // tracking migrations across every box-table schema. It always lives in [dbo] regardless of
    // the connection's default schema or the configured box schema.
    private const string HISTORY_TABLE_SCHEMA = "dbo";

    // Lock-timeout validation lives inside MsSqlAdvisoryLock.AcquireAsync (per ADR 0057 §5b)
    // so any caller of the abstraction is protected. A bad timeout surfaces as
    // ArgumentOutOfRangeException on first MigrateAsync call rather than at construction.
    private readonly TimeSpan _lockTimeout = lockTimeout;
    private readonly IMsSqlAdvisoryLock _advisoryLock = advisoryLock ?? new MsSqlAdvisoryLock();
    private readonly ILogger _logger = logger ?? ApplicationLogging.CreateLogger<MsSqlBoxMigrationRunner>();

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        var effectiveSchema = schemaName ?? "dbo";

        // Reject duplicate / gap / out-of-order versions before opening a connection. Validation
        // sits at MigrateAsync entry (rather than inside one of the path branches) so the rule
        // applies uniformly across fresh / bootstrap / normal paths — a malformed list corrupts
        // any of them (PK violation on history insert, skipped ALTERs, double-applied DDL).
        ValidateMigrationsMonotonic(effectiveSchema, tableName, migrations);

        using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

#if NETFRAMEWORK
        var transaction = connection.BeginTransaction();
#else
        var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
#endif

        var lockResource = $"BrighterMigration_{tableName}";

        try
        {
            await _advisoryLock.AcquireAsync(
                connection, transaction, lockResource, _lockTimeout, cancellationToken);
            await EnsureHistoryTableAsync(connection, transaction, cancellationToken);

            var tableExistsNow = await MsSqlBoxDetectionHelpers.DoesTableExistAsync(
                connection, tableName, effectiveSchema, cancellationToken, transaction);
            var historyExistsNow = tableExistsNow && await MsSqlBoxDetectionHelpers.DoesHistoryExistAsync(
                connection, tableName, effectiveSchema, cancellationToken, transaction);

            if (!tableExistsNow)
            {
                await RunFreshPathAsync(
                    connection, transaction, effectiveSchema, tableName, migrations, cancellationToken);
            }
            else if (!historyExistsNow)
            {
                await RunBootstrapPathAsync(
                    connection, transaction, effectiveSchema, tableName, boxType, migrations, cancellationToken);
            }
            else
            {
                await RunNormalPathAsync(
                    connection, transaction, effectiveSchema, tableName, migrations, cancellationToken);
            }

            transaction.Commit();
        }
        catch
        {
            try { transaction.Rollback(); } catch { /* connection may already be closed */ }
            throw;
        }
        finally
        {
            transaction.Dispose();
        }
    }

    private async Task RunFreshPathAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        if (migrations.Count == 0) return;

        // V1's UpScript IS the live builder DDL (V_latest-shape per ADR §3 fresh-install fast
        // path). A list whose first entry is anything other than V1 would silently install the
        // wrong schema, so reject it before any DDL fires.
        if (migrations[0].Version != 1)
            throw new ConfigurationException(
                $"Cannot install '{schemaName}.{tableName}' from a fresh state: " +
                $"the first migration must be V1, but the supplied migrations list starts at V{migrations[0].Version}.");

        // We stamp directly at V_latest with a "fresh install" marker — V2..V_latest ALTERs
        // would be no-ops on the V_latest-shape table, so we skip them.
        await ExecuteUpScriptAsync(connection, transaction, migrations[0], cancellationToken);

        var latest = migrations[migrations.Count - 1];
        await InsertHistoryRowAsync(
            connection, transaction, schemaName, tableName,
            latest.Version, $"fresh install at V{latest.Version}", cancellationToken);
    }

    private async Task RunBootstrapPathAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var detected = await MsSqlBoxDetectionHelpers.DetectCurrentVersionAsync(
            connection, tableName, schemaName, boxType, migrations, cancellationToken, transaction);

        if (detected == -1)
        {
            var discriminator = MsSqlBoxDetectionHelpers.DiscriminatorFor(boxType);
            throw new ConfigurationException(
                $"Table '{schemaName}.{tableName}' is not a Brighter {boxType.ToString().ToLowerInvariant()}: " +
                $"missing discriminator column '{discriminator}'.");
        }

        if (detected == 0)
        {
            throw new ConfigurationException(
                $"Table '{schemaName}.{tableName}' does not match any known schema version. " +
                $"Cannot bootstrap a Brighter {boxType.ToString().ToLowerInvariant()} from an unrecognised column set.");
        }

        await InsertHistoryRowAsync(
            connection, transaction, schemaName, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, transaction, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction, schemaName, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private async Task RunNormalPathAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var maxVersion = await MsSqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, tableName, schemaName, cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            if (await IsMigrationAppliedAsync(
                    connection, transaction, schemaName, tableName,
                    migration.Version, cancellationToken))
                continue;

            await ExecuteUpScriptAsync(connection, transaction, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction, schemaName, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static void ValidateMigrationsMonotonic(
        string schemaName, string tableName, IReadOnlyList<IAmABoxMigration> migrations)
    {
        for (var i = 1; i < migrations.Count; i++)
        {
            var prev = migrations[i - 1].Version;
            var curr = migrations[i].Version;
            if (curr != prev + 1)
            {
                throw new ConfigurationException(
                    $"Migration list for '{schemaName}.{tableName}' is not contiguous and ascending: " +
                    $"V{prev} followed by V{curr} (expected V{prev + 1}).");
            }
        }
    }

    private static async Task EnsureHistoryTableAsync(
        SqlConnection connection, SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Filter sys.tables by both name AND schema_id — without the schema_id filter the
        // existence check misfires when any other schema happens to contain a table by that name,
        // skipping the [dbo] create and breaking subsequent INSERT/SELECT statements.
        command.CommandText = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = '{MIGRATION_HISTORY_TABLE}' AND schema_id = SCHEMA_ID('{HISTORY_TABLE_SCHEMA}')
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
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static async Task<bool> IsMigrationAppliedAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        int version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
SELECT COUNT(1) FROM [{HISTORY_TABLE_SCHEMA}].[{MIGRATION_HISTORY_TABLE}]
WHERE [SchemaName] = @SchemaName AND [BoxTableName] = @BoxTableName AND [MigrationVersion] = @Version";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Version", version);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
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

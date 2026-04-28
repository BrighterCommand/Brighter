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
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Runs box migrations against a PostgreSQL database. Uses <c>pg_try_advisory_lock</c> for
/// concurrency control with a retry loop bounded by <c>MigrationLockTimeout</c>, and a single
/// <see cref="NpgsqlTransaction"/> for all-or-nothing semantics. After acquiring the lock the
/// runner re-reads box-table state under the lock and dispatches into one of three paths
/// (fresh / bootstrap / normal) per ADR 0057 §3 — the caller's <see cref="BoxTableState"/>
/// is treated as a stale hint to defeat TOCTOU races.
/// </summary>
public class PostgreSqlBoxMigrationRunner(
    IAmARelationalDatabaseConfiguration configuration,
    TimeSpan lockTimeout) : IAmABoxMigrationRunner
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";
    private const int BRIGHTER_LOCK_NAMESPACE = 74726;

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        _ = tableState; // Stale hint — runner re-detects under the advisory lock.
        var effectiveSchema = schemaName ?? "public";

        using var connection = new NpgsqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await AcquireLockAsync(connection, tableName, cancellationToken);

        try
        {
#if NETFRAMEWORK
            var transaction = connection.BeginTransaction();
#else
            var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
#endif

            try
            {
                await EnsureHistoryTableAsync(connection, transaction, cancellationToken);

                var tableExistsNow = await PostgreSqlBoxDetectionHelpers.DoesTableExistAsync(
                    connection, tableName, effectiveSchema, cancellationToken, transaction);
                var historyExistsNow = tableExistsNow && await PostgreSqlBoxDetectionHelpers.DoesHistoryExistAsync(
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
        finally
        {
            await ReleaseLockAsync(connection, tableName, cancellationToken);
        }
    }

    private async Task RunFreshPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        if (migrations.Count == 0) return;

        // V1's UpScript IS the live builder DDL (V_latest-shape per ADR §3 fresh-install fast
        // path). We stamp directly at V_latest with a "fresh install" marker — V2..V_latest
        // ALTERs would be no-ops on the V_latest-shape table, so we skip them.
        await ExecuteUpScriptAsync(connection, transaction, migrations[0], cancellationToken);

        var latest = migrations[migrations.Count - 1];
        await InsertHistoryRowAsync(
            connection, transaction, schemaName, tableName,
            latest.Version, $"fresh install at V{latest.Version}", cancellationToken);
    }

    private async Task RunBootstrapPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var detected = await PostgreSqlBoxDetectionHelpers.DetectCurrentVersionAsync(
            connection, tableName, schemaName, boxType, migrations, cancellationToken, transaction);

        if (detected == -1)
        {
            var discriminator = PostgreSqlBoxDetectionHelpers.DiscriminatorFor(boxType);
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
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var maxVersion = await PostgreSqlBoxDetectionHelpers.GetMaxVersionAsync(
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

    private async Task AcquireLockAsync(
        NpgsqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var lockKey = $"BrighterMigration_{tableName}";
        var deadline = DateTime.UtcNow.Add(lockTimeout);
        var delayMs = 100;

        while (true)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@ns, hashtext(@key))";
            command.Parameters.AddWithValue("@ns", BRIGHTER_LOCK_NAMESPACE);
            command.Parameters.AddWithValue("@key", lockKey);

            var result = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
            if (result) return;

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"Could not acquire migration lock for '{tableName}' within {lockTimeout.TotalSeconds}s.");
            }

            await Task.Delay(delayMs, cancellationToken);
            delayMs = Math.Min(delayMs * 2, 1000);
        }
    }

    private static async Task ReleaseLockAsync(
        NpgsqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var lockKey = $"BrighterMigration_{tableName}";

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@ns, hashtext(@key))";
        command.Parameters.AddWithValue("@ns", BRIGHTER_LOCK_NAMESPACE);
        command.Parameters.AddWithValue("@key", lockKey);

        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task EnsureHistoryTableAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS ""{MIGRATION_HISTORY_TABLE}"" (
    ""MigrationVersion"" INT NOT NULL,
    ""SchemaName"" VARCHAR(256) NOT NULL DEFAULT 'public',
    ""BoxTableName"" VARCHAR(256) NOT NULL,
    ""Description"" VARCHAR(512) NOT NULL,
    ""AppliedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (""SchemaName"", ""BoxTableName"", ""MigrationVersion"")
)";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteUpScriptAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = migration.UpScript;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        int version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
SELECT COUNT(1) FROM ""{MIGRATION_HISTORY_TABLE}""
WHERE ""SchemaName"" = @SchemaName AND ""BoxTableName"" = @BoxTableName AND ""MigrationVersion"" = @Version";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Version", version);

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    private static async Task InsertHistoryRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO ""{MIGRATION_HISTORY_TABLE}"" (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"")
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

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
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Runs box migrations against a MySQL database. Uses session-scoped <c>GET_LOCK</c> for
/// concurrency control with a configurable timeout. After acquiring the lock the runner re-reads
/// box-table state under the lock and dispatches into one of three paths (fresh / bootstrap /
/// normal) per ADR 0057 §3 — the caller's <see cref="BoxTableState"/> is treated as a stale hint
/// to defeat TOCTOU races.
/// </summary>
/// <remarks>
/// Unlike MSSQL/Postgres, MySQL DDL has implicit per-statement commit (ADR 0057 §5a) so the
/// migration chain does NOT run inside a single transaction — each ALTER + history INSERT
/// commits independently. Recovery from mid-chain failure is therefore "resume from MAX(V) on
/// next invocation" rather than whole-chain rollback. The history table's PK on
/// <c>(SchemaName, BoxTableName, MigrationVersion)</c> ensures concurrent racers cannot double-stamp.
/// </remarks>
public class MySqlBoxMigrationRunner(
    IAmARelationalDatabaseConfiguration configuration,
    TimeSpan lockTimeout) : IAmABoxMigrationRunner
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        _ = tableState; // Stale hint — runner re-detects under the GET_LOCK session.
        var effectiveSchema = schemaName ?? DatabaseName();

        using var connection = new MySqlConnection(EnsureAllowUserVariables(configuration.ConnectionString));
        await connection.OpenAsync(cancellationToken);

        await AcquireLockAsync(connection, tableName, cancellationToken);

        try
        {
            await EnsureHistoryTableAsync(connection, cancellationToken);

            var tableExistsNow = await MySqlBoxDetectionHelpers.DoesTableExistAsync(
                connection, tableName, effectiveSchema, cancellationToken);
            var historyExistsNow = tableExistsNow && await MySqlBoxDetectionHelpers.DoesHistoryExistAsync(
                connection, tableName, effectiveSchema, cancellationToken);

            if (!tableExistsNow)
            {
                await RunFreshPathAsync(
                    connection, effectiveSchema, tableName, migrations, cancellationToken);
            }
            else if (!historyExistsNow)
            {
                await RunBootstrapPathAsync(
                    connection, effectiveSchema, tableName, boxType, migrations, cancellationToken);
            }
            else
            {
                await RunNormalPathAsync(
                    connection, effectiveSchema, tableName, migrations, cancellationToken);
            }
        }
        finally
        {
            await ReleaseLockAsync(connection, tableName, cancellationToken);
        }
    }

    private static async Task RunFreshPathAsync(
        MySqlConnection connection, string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        if (migrations.Count == 0) return;

        // V1's UpScript IS the live builder DDL (V_latest-shape per ADR §3 fresh-install fast
        // path). We stamp directly at V_latest with a "fresh install" marker — V2..V_latest
        // ALTERs would be no-ops on the V_latest-shape table, so we skip them.
        await ExecuteUpScriptAsync(connection, migrations[0], cancellationToken);

        var latest = migrations[migrations.Count - 1];
        await InsertHistoryRowAsync(
            connection, schemaName, tableName,
            latest.Version, $"fresh install at V{latest.Version}", cancellationToken);
    }

    private static async Task RunBootstrapPathAsync(
        MySqlConnection connection, string schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var detected = await MySqlBoxDetectionHelpers.DetectCurrentVersionAsync(
            connection, tableName, schemaName, boxType, migrations, cancellationToken);

        if (detected == -1)
        {
            var discriminator = MySqlBoxDetectionHelpers.DiscriminatorFor(boxType);
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
            connection, schemaName, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, schemaName, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static async Task RunNormalPathAsync(
        MySqlConnection connection, string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var maxVersion = await MySqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, tableName, schemaName, cancellationToken);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            if (await IsMigrationAppliedAsync(
                    connection, schemaName, tableName, migration.Version, cancellationToken))
                continue;

            await ExecuteUpScriptAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, schemaName, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static async Task ExecuteUpScriptAsync(
        MySqlConnection connection, IAmABoxMigration migration,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = migration.UpScript;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AcquireLockAsync(
        MySqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var lockName = MySqlMigrationLockName.For(tableName);
        var timeoutSeconds = (int)lockTimeout.TotalSeconds;

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@LockName, @Timeout)";
        command.Parameters.AddWithValue("@LockName", lockName);
        command.Parameters.AddWithValue("@Timeout", timeoutSeconds);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result == null || Convert.ToInt32(result) != 1)
        {
            throw new TimeoutException(
                $"Could not acquire migration lock for '{tableName}' within {lockTimeout.TotalSeconds}s.");
        }
    }

    private static async Task ReleaseLockAsync(
        MySqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var lockName = MySqlMigrationLockName.For(tableName);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT RELEASE_LOCK(@LockName)";
        command.Parameters.AddWithValue("@LockName", lockName);

        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task EnsureHistoryTableAsync(
        MySqlConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS `{MIGRATION_HISTORY_TABLE}` (
    `MigrationVersion` INT NOT NULL,
    `SchemaName` VARCHAR(256) NOT NULL,
    `BoxTableName` VARCHAR(256) NOT NULL,
    `Description` VARCHAR(512) NOT NULL,
    `AppliedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SchemaName`, `BoxTableName`, `MigrationVersion`)
) ENGINE = InnoDB";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        MySqlConnection connection, string schemaName, string tableName,
        int version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM `{MIGRATION_HISTORY_TABLE}`
WHERE `SchemaName` = @SchemaName AND `BoxTableName` = @BoxTableName AND `MigrationVersion` = @Version";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Version", version);

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    private static async Task InsertHistoryRowAsync(
        MySqlConnection connection, string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
INSERT INTO `{MIGRATION_HISTORY_TABLE}` (`MigrationVersion`, `SchemaName`, `BoxTableName`, `Description`)
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string DatabaseName()
    {
        var builder = new MySqlConnectionStringBuilder(configuration.ConnectionString);
        return builder.Database;
    }

    /// <summary>
    /// Ensures the connection string sets <c>AllowUserVariables=true</c>. The V2..V7 outbox/inbox
    /// migrations (per ADR 0057 §5) use the MySQL <c>information_schema.columns</c> +
    /// prepared-statement idempotency pattern, which depends on session-scoped user variables
    /// (<c>SET @q = ...; PREPARE stmt FROM @q;</c>). MySqlConnector treats <c>@variable</c> tokens
    /// as parameter markers by default and rejects them; this flag flips that behaviour so the
    /// prepared-statement form executes correctly. Transparent to caller-supplied connection
    /// strings — adds the flag if missing, preserves it if already set.
    /// </summary>
    private static string EnsureAllowUserVariables(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true
        };
        return builder.ConnectionString;
    }
}

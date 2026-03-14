using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Runs box migrations against a PostgreSQL database. Uses pg_try_advisory_lock
/// for concurrency control with a retry loop bounded by MigrationLockTimeout.
/// </summary>
public class PostgreSqlBoxMigrationRunner : IAmABoxMigrationRunner
{
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly TimeSpan _lockTimeout;

    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";
    private const int BRIGHTER_LOCK_NAMESPACE = 74726;

    public PostgreSqlBoxMigrationRunner(
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout)
    {
        _configuration = configuration;
        _lockTimeout = lockTimeout;
    }

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        var effectiveSchema = schemaName ?? "public";

        using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await AcquireLockAsync(connection, tableName, cancellationToken);

        try
        {
            await EnsureHistoryTableAsync(connection, cancellationToken);

            if (tableState is { TableExists: true, HistoryExists: false })
            {
                await InsertSyntheticHistoryAsync(
                    connection, effectiveSchema, tableName,
                    migrations, tableState.CurrentVersion, cancellationToken);
            }

            foreach (var migration in migrations)
            {
                if (migration.Version <= tableState.CurrentVersion)
                    continue;

                if (await IsMigrationAppliedAsync(
                        connection, effectiveSchema, tableName,
                        migration.Version, cancellationToken))
                    continue;

                using var ddlCommand = connection.CreateCommand();
                ddlCommand.CommandText = migration.UpScript;
                await ddlCommand.ExecuteNonQueryAsync(cancellationToken);

                await InsertHistoryRowAsync(
                    connection, effectiveSchema, tableName,
                    migration, cancellationToken);
            }
        }
        finally
        {
            await ReleaseLockAsync(connection, tableName, cancellationToken);
        }
    }

    private async Task AcquireLockAsync(
        NpgsqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var lockKey = $"BrighterMigration_{tableName}";
        var deadline = DateTime.UtcNow.Add(_lockTimeout);
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
                    $"Could not acquire migration lock for '{tableName}' within {_lockTimeout.TotalSeconds}s.");
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
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
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

    private static async Task InsertSyntheticHistoryAsync(
        NpgsqlConnection connection, string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
        int currentVersion, CancellationToken cancellationToken)
    {
        foreach (var migration in migrations)
        {
            if (migration.Version > currentVersion) break;

            await InsertHistoryRowAsync(
                connection, schemaName, tableName,
                migration, cancellationToken);
        }
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        NpgsqlConnection connection, string schemaName, string tableName,
        int version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
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
        NpgsqlConnection connection, string schemaName, string tableName,
        IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
INSERT INTO ""{MIGRATION_HISTORY_TABLE}"" (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"")
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", migration.Version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", migration.Description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

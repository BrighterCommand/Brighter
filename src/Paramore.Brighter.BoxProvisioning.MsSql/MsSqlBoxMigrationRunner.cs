using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Runs box migrations against a MSSQL database. Uses sp_getapplock for
/// concurrency control and a single transaction for all-or-nothing semantics.
/// </summary>
public class MsSqlBoxMigrationRunner : IAmABoxMigrationRunner
{
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly TimeSpan _lockTimeout;

    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";

    public MsSqlBoxMigrationRunner(
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
        var effectiveSchema = schemaName ?? "dbo";

        using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var transaction = connection.BeginTransaction();

        try
        {
            await AcquireLockAsync(connection, transaction, tableName, cancellationToken);
            await EnsureHistoryTableAsync(connection, transaction, cancellationToken);

            if (tableState is { TableExists: true, HistoryExists: false })
            {
                await InsertSyntheticHistoryAsync(
                    connection, transaction, effectiveSchema, tableName,
                    migrations, tableState.CurrentVersion, cancellationToken);
            }

            foreach (var migration in migrations)
            {
                if (migration.Version <= tableState.CurrentVersion)
                    continue;

                if (await IsMigrationAppliedAsync(
                        connection, transaction, effectiveSchema, tableName,
                        migration.Version, cancellationToken))
                    continue;

                using var ddlCommand = connection.CreateCommand();
                ddlCommand.Transaction = transaction;
                ddlCommand.CommandText = migration.UpScript;
                await ddlCommand.ExecuteNonQueryAsync(cancellationToken);

                await InsertHistoryRowAsync(
                    connection, transaction, effectiveSchema, tableName,
                    migration, cancellationToken);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            transaction.Dispose();
        }
    }

    private async Task AcquireLockAsync(
        SqlConnection connection, SqlTransaction transaction,
        string tableName, CancellationToken cancellationToken)
    {
        var lockResource = $"BrighterMigration_{tableName}";

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "DECLARE @result INT; " +
            "EXEC @result = sp_getapplock " +
            "@Resource = @Resource, " +
            "@LockMode = 'Exclusive', " +
            "@LockTimeout = @LockTimeout, " +
            "@LockOwner = 'Transaction'; " +
            "SELECT @result;";
        command.Parameters.AddWithValue("@Resource", lockResource);
        command.Parameters.AddWithValue("@LockTimeout", (int)_lockTimeout.TotalMilliseconds);

        var result = (int)(await command.ExecuteScalarAsync(cancellationToken))!;

        if (result < 0)
        {
            throw new TimeoutException(
                $"Could not acquire migration lock for '{tableName}' within {_lockTimeout.TotalSeconds}s. " +
                $"sp_getapplock returned {result}.");
        }
    }

    private static async Task EnsureHistoryTableAsync(
        SqlConnection connection, SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{MIGRATION_HISTORY_TABLE}')
BEGIN
    CREATE TABLE [{MIGRATION_HISTORY_TABLE}] (
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

    private static async Task InsertSyntheticHistoryAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
        int currentVersion, CancellationToken cancellationToken)
    {
        foreach (var migration in migrations)
        {
            if (migration.Version > currentVersion) break;

            await InsertHistoryRowAsync(
                connection, transaction, schemaName, tableName,
                migration, cancellationToken);
        }
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        int version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
SELECT COUNT(1) FROM [{MIGRATION_HISTORY_TABLE}]
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
        IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO [{MIGRATION_HISTORY_TABLE}] ([MigrationVersion], [SchemaName], [BoxTableName], [Description])
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", migration.Version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", migration.Description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

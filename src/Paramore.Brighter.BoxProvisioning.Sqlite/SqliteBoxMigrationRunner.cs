using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Runs box migrations against a SQLite database. SQLite uses file-level locking
/// which provides implicit serialization — no advisory lock is needed.
/// </summary>
public class SqliteBoxMigrationRunner(
    IAmARelationalDatabaseConfiguration configuration) : IAmABoxMigrationRunner
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
        _ = boxType; // TODO(spec 0027 phase 4): three-path branching with discriminator gate
        using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureWalModeAsync(connection, cancellationToken);
        await EnsureHistoryTableAsync(connection, cancellationToken);
        await BootstrapExistingTableAsync(connection, tableName, migrations, tableState, cancellationToken);
        await ApplyPendingMigrationsAsync(connection, tableName, migrations, tableState, cancellationToken);
    }

    private static async Task EnsureWalModeAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task BootstrapExistingTableAsync(
        SqliteConnection connection, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, BoxTableState tableState,
        CancellationToken cancellationToken)
    {
        if (tableState is not { TableExists: true, HistoryExists: false })
            return;

        using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var migration in migrations)
            {
                if (migration.Version > tableState.CurrentVersion) break;

                await InsertHistoryRowAsync(connection, transaction, tableName, migration, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try { await transaction.RollbackAsync(cancellationToken); } catch { /* connection may already be closed */ }
            throw;
        }
    }

    private static async Task ApplyPendingMigrationsAsync(
        SqliteConnection connection, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, BoxTableState tableState,
        CancellationToken cancellationToken)
    {
        foreach (var migration in migrations)
        {
            if (migration.Version <= tableState.CurrentVersion)
                continue;

            if (await IsMigrationAppliedAsync(connection, tableName, migration.Version, cancellationToken))
                continue;

            using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await ExecuteMigrationAsync(connection, transaction, migration, cancellationToken);
                await InsertHistoryRowAsync(connection, transaction, tableName, migration, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                try { await transaction.RollbackAsync(cancellationToken); } catch { /* connection may already be closed */ }
                throw;
            }
        }
    }

    private static async Task ExecuteMigrationAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var ddlCommand = connection.CreateCommand();
        ddlCommand.Transaction = transaction;
        ddlCommand.CommandText = migration.UpScript;
        await ddlCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureHistoryTableAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS [{MIGRATION_HISTORY_TABLE}] (
    [MigrationVersion] INTEGER NOT NULL,
    [BoxTableName] TEXT NOT NULL,
    [Description] TEXT NOT NULL,
    [AppliedAt] TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY ([BoxTableName], [MigrationVersion])
)";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        SqliteConnection connection, string tableName,
        int version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM [{MIGRATION_HISTORY_TABLE}]
WHERE [BoxTableName] = @BoxTableName AND [MigrationVersion] = @Version";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Version", version);

        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    private static async Task InsertHistoryRowAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string tableName, IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO [{MIGRATION_HISTORY_TABLE}] ([MigrationVersion], [BoxTableName], [Description])
VALUES (@Version, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", migration.Version);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", migration.Description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

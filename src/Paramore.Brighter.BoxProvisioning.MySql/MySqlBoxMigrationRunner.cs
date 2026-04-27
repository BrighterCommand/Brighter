using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Runs box migrations against a MySQL database. Uses GET_LOCK
/// for concurrency control with a configurable timeout.
/// </summary>
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
        _ = boxType; // TODO(spec 0027 phase 3): three-path branching with discriminator gate
        var effectiveSchema = schemaName ?? DatabaseName();

        using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await AcquireLockAsync(connection, tableName, cancellationToken);

        try
        {
            await EnsureHistoryTableAsync(connection, cancellationToken);
            await BootstrapExistingTableAsync(connection, effectiveSchema, tableName, migrations, tableState, cancellationToken);
            await ApplyPendingMigrationsAsync(connection, effectiveSchema, tableName, migrations, tableState, cancellationToken);
        }
        finally
        {
            await ReleaseLockAsync(connection, tableName, cancellationToken);
        }
    }

    private static async Task BootstrapExistingTableAsync(
        MySqlConnection connection, string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, BoxTableState tableState,
        CancellationToken cancellationToken)
    {
        if (tableState is not { TableExists: true, HistoryExists: false })
            return;

        foreach (var migration in migrations)
        {
            if (migration.Version > tableState.CurrentVersion) break;

            await InsertHistoryRowAsync(connection, schemaName, tableName, migration, cancellationToken);
        }
    }

    private static async Task ApplyPendingMigrationsAsync(
        MySqlConnection connection, string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, BoxTableState tableState,
        CancellationToken cancellationToken)
    {
        foreach (var migration in migrations)
        {
            if (migration.Version <= tableState.CurrentVersion)
                continue;

            if (await IsMigrationAppliedAsync(connection, schemaName, tableName, migration.Version, cancellationToken))
                continue;

            await ExecuteMigrationAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(connection, schemaName, tableName, migration, cancellationToken);
        }
    }

    private static async Task ExecuteMigrationAsync(
        MySqlConnection connection, IAmABoxMigration migration,
        CancellationToken cancellationToken)
    {
        using var ddlCommand = connection.CreateCommand();
        ddlCommand.CommandText = migration.UpScript;
        await ddlCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AcquireLockAsync(
        MySqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var lockName = $"BrighterMigration_{tableName}";
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
        var lockName = $"BrighterMigration_{tableName}";

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
        IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
INSERT INTO `{MIGRATION_HISTORY_TABLE}` (`MigrationVersion`, `SchemaName`, `BoxTableName`, `Description`)
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", migration.Version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", migration.Description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string DatabaseName()
    {
        var builder = new MySqlConnectionStringBuilder(configuration.ConnectionString);
        return builder.Database;
    }
}

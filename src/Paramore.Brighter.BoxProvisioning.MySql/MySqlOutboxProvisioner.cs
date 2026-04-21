using System;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Provisions a MySQL outbox table.
/// </summary>
public class MySqlOutboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    public BoxType BoxType => BoxType.Outbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MySqlOutboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.OutBoxTableName,
            configuration.SchemaName,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? DatabaseName();

        using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await DoesTableExistAsync(connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await DoesHistoryExistAsync(connection, configuration.OutBoxTableName, schemaName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(connection, configuration.OutBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await GetMaxVersionAsync(connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? DatabaseName();

        using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await MySqlPayloadModeValidator.ValidateAsync(
            connection, configuration.OutBoxTableName, schemaName,
            "Body", configuration.BinaryMessagePayload, cancellationToken);
    }

    internal static async Task<bool> DoesTableExistAsync(
        MySqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM information_schema.tables
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToBoolean(result);
    }

    internal static async Task<bool> DoesHistoryExistAsync(
        MySqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        var historyTableExists = await DoesTableExistAsync(
            connection, "__BrighterMigrationHistory", schemaName, cancellationToken);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `SchemaName` = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    internal static Task<int> DetectCurrentVersionAsync(
        MySqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        // This method is only called when tableExists is true, so at minimum version 1.
        // When future migrations add columns, extend this to check for version-specific
        // columns and return higher version numbers accordingly.
        return Task.FromResult(1);
    }

    internal static async Task<int> GetMaxVersionAsync(
        MySqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COALESCE(MAX(`MigrationVersion`), 0) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `SchemaName` = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private string DatabaseName()
    {
        var builder = new MySqlConnectionStringBuilder(configuration.ConnectionString);
        return builder.Database;
    }

    private static async Task<bool> ColumnExistsAsync(
        MySqlConnection connection, string tableName, string schemaName,
        string columnName, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM information_schema.columns
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToBoolean(result);
    }
}

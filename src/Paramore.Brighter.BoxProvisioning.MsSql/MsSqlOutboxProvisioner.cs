using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Provisions a MSSQL outbox table. Creates the table if it doesn't exist,
/// detects existing tables, and applies migrations.
/// </summary>
public class MsSqlOutboxProvisioner : IAmABoxProvisioner
{
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    public BoxType BoxType => BoxType.Outbox;

    public MsSqlOutboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
    {
        _configuration = configuration;
        _migrationRunner = migrationRunner;
    }

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MsSqlOutboxMigrations.All(_configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await _migrationRunner.MigrateAsync(
            _configuration.OutBoxTableName,
            _configuration.SchemaName,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        var schemaName = _configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await DoesTableExistAsync(connection, _configuration.OutBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await DoesHistoryExistAsync(connection, _configuration.OutBoxTableName, schemaName, cancellationToken);
        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(connection, _configuration.OutBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await GetMaxVersionAsync(connection, _configuration.OutBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = _configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await MsSqlPayloadModeValidator.ValidateAsync(
            connection, _configuration.OutBoxTableName, schemaName,
            "Body", _configuration.BinaryMessagePayload, cancellationToken);
    }

    internal static async Task<bool> DoesTableExistAsync(
        SqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = @TableName AND s.name = @SchemaName";
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    internal static async Task<bool> DoesHistoryExistAsync(
        SqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        // Check if history table exists
        var historyTableExists = await DoesTableExistAsync(connection, "__BrighterMigrationHistory", "dbo", cancellationToken);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    internal static async Task<int> DetectCurrentVersionAsync(
        SqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        // Version 1 columns include the base outbox schema
        // If the table exists at all, it's at least version 1
        var hasBody = await ColumnExistsAsync(connection, tableName, schemaName, "Body", cancellationToken);
        return hasBody ? 1 : 0;
    }

    internal static async Task<int> GetMaxVersionAsync(
        SqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT ISNULL(MAX([MigrationVersion]), 0) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<bool> ColumnExistsAsync(
        SqlConnection connection, string tableName, string schemaName,
        string columnName, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @SchemaName AND COLUMN_NAME = @ColumnName";
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }
}

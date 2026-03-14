using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Provisions a PostgreSQL outbox table.
/// </summary>
public class PostgreSqlOutboxProvisioner : IAmABoxProvisioner
{
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    public BoxType BoxType => BoxType.Outbox;

    public PostgreSqlOutboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
    {
        _configuration = configuration;
        _migrationRunner = migrationRunner;
    }

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = PostgreSqlOutboxMigrations.All(_configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        await _migrationRunner.MigrateAsync(
            _configuration.OutBoxTableName,
            _configuration.SchemaName,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        var schemaName = _configuration.SchemaName ?? "public";

        using var connection = new NpgsqlConnection(_configuration.ConnectionString);
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

    internal static async Task<bool> DoesTableExistAsync(
        NpgsqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    internal static async Task<bool> DoesHistoryExistAsync(
        NpgsqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        var historyTableExists = await DoesTableExistAsync(
            connection, "__BrighterMigrationHistory", "public", cancellationToken);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    internal static async Task<int> DetectCurrentVersionAsync(
        NpgsqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        var hasBody = await ColumnExistsAsync(connection, tableName, schemaName, "body", cancellationToken);
        return hasBody ? 1 : 0;
    }

    internal static async Task<int> GetMaxVersionAsync(
        NpgsqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COALESCE(MAX(""MigrationVersion""), 0) FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<bool> ColumnExistsAsync(
        NpgsqlConnection connection, string tableName, string schemaName,
        string columnName, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}

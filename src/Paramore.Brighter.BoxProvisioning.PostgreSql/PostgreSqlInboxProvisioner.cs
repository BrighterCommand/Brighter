using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Provisions a PostgreSQL inbox table.
/// </summary>
public class PostgreSqlInboxProvisioner : IAmABoxProvisioner
{
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    public BoxType BoxType => BoxType.Inbox;

    public PostgreSqlInboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
    {
        _configuration = configuration;
        _migrationRunner = migrationRunner;
    }

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = PostgreSqlInboxMigrations.All(_configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        await _migrationRunner.MigrateAsync(
            _configuration.InBoxTableName,
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

        var tableExists = await PostgreSqlOutboxProvisioner.DoesTableExistAsync(
            connection, _configuration.InBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await PostgreSqlOutboxProvisioner.DoesHistoryExistAsync(
            connection, _configuration.InBoxTableName, schemaName, cancellationToken);
        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, _configuration.InBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await PostgreSqlOutboxProvisioner.GetMaxVersionAsync(
            connection, _configuration.InBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private static async Task<int> DetectCurrentVersionAsync(
        NpgsqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName AND COLUMN_NAME = 'commandbody')";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);

        var exists = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
        return exists ? 1 : 0;
    }
}

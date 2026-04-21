using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Provisions a MSSQL inbox table. Creates the table if it doesn't exist,
/// detects existing tables, and applies migrations.
/// </summary>
public class MsSqlInboxProvisioner : IAmABoxProvisioner
{
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    public BoxType BoxType => BoxType.Inbox;

    public MsSqlInboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
    {
        _configuration = configuration;
        _migrationRunner = migrationRunner;
    }

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MsSqlInboxMigrations.All(_configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await _migrationRunner.MigrateAsync(
            _configuration.InBoxTableName,
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

        var tableExists = await MsSqlOutboxProvisioner.DoesTableExistAsync(
            connection, _configuration.InBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await MsSqlOutboxProvisioner.DoesHistoryExistAsync(
            connection, _configuration.InBoxTableName, schemaName, cancellationToken);
        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, _configuration.InBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await MsSqlOutboxProvisioner.GetMaxVersionAsync(
            connection, _configuration.InBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = _configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await MsSqlPayloadModeValidator.ValidateAsync(
            connection, _configuration.InBoxTableName, schemaName,
            "CommandBody", _configuration.BinaryMessagePayload, cancellationToken);
    }

    private static Task<int> DetectCurrentVersionAsync(
        SqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        // This method is only called when tableExists is true, so at minimum version 1.
        // When future migrations add columns, extend this to check for version-specific
        // columns and return higher version numbers accordingly.
        return Task.FromResult(1);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Provisions a MySQL inbox table.
/// </summary>
public class MySqlInboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    private static readonly HashSet<string> V1Columns = new(StringComparer.OrdinalIgnoreCase)
    {
        "CommandId", "CommandType", "CommandBody", "Timestamp", "ContextKey"
    };

    public BoxType BoxType => BoxType.Inbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MySqlInboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.InBoxTableName,
            configuration.SchemaName,
            BoxType.Inbox,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? DatabaseName();

        using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await MySqlBoxDetectionHelpers.DoesTableExistAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await MySqlBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, configuration.InBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await MySqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? DatabaseName();

        using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await MySqlPayloadModeValidator.ValidateAsync(
            connection, configuration.InBoxTableName, schemaName,
            "CommandBody", configuration.BinaryMessagePayload, cancellationToken);
    }

    private static async Task<int> DetectCurrentVersionAsync(
        MySqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await MySqlBoxDetectionHelpers.GetTableColumnsAsync(
            connection, tableName, schemaName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }

    private string DatabaseName()
    {
        var builder = new MySqlConnectionStringBuilder(configuration.ConnectionString);
        return builder.Database;
    }
}

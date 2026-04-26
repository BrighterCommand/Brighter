using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Provisions a MSSQL inbox table. Creates the table if it doesn't exist,
/// detects existing tables, and applies migrations.
/// </summary>
public class MsSqlInboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    private static readonly HashSet<string> V1Columns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "CommandId", "CommandType", "CommandBody", "Timestamp", "ContextKey"
    };

    public BoxType BoxType => BoxType.Inbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MsSqlInboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.InBoxTableName,
            configuration.SchemaName,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await MsSqlBoxDetectionHelpers.DoesTableExistAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await MsSqlBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, configuration.InBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await MsSqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await MsSqlPayloadModeValidator.ValidateAsync(
            connection, configuration.InBoxTableName, schemaName,
            "CommandBody", configuration.BinaryMessagePayload, cancellationToken);
    }

    private static async Task<int> DetectCurrentVersionAsync(
        SqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await MsSqlBoxDetectionHelpers.GetTableColumnsAsync(
            connection, tableName, schemaName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }
}

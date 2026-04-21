using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Provisions a SQLite inbox table.
/// </summary>
public class SqliteInboxProvisioner(
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
        var migrations = SqliteInboxMigrations.All(configuration);
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
        using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await SqliteOutboxProvisioner.DoesTableExistAsync(
            connection, configuration.InBoxTableName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await SqliteOutboxProvisioner.DoesHistoryExistAsync(
            connection, configuration.InBoxTableName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, configuration.InBoxTableName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await SqliteOutboxProvisioner.GetMaxVersionAsync(
            connection, configuration.InBoxTableName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await SqlitePayloadModeValidator.ValidateAsync(
            connection, configuration.InBoxTableName,
            "CommandBody", configuration.BinaryMessagePayload, cancellationToken);
    }

    internal static async Task<int> DetectCurrentVersionAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await SqliteOutboxProvisioner.GetTableColumnsAsync(
            connection, tableName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }
}

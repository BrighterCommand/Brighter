using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Provisions a SQLite outbox table.
/// </summary>
public class SqliteOutboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    internal static readonly HashSet<string> V1Columns = new(StringComparer.OrdinalIgnoreCase)
    {
        "MessageId", "MessageType", "Topic", "Timestamp", "CorrelationId",
        "ReplyTo", "ContentType", "PartitionKey", "WorkflowId", "JobId", "Dispatched",
        "HeaderBag", "Body", "Source", "Type", "DataSchema", "Subject",
        "TraceParent", "TraceState", "Baggage", "DataRef", "SpecVersion"
    };

    public BoxType BoxType => BoxType.Outbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = SqliteOutboxMigrations.All(configuration);
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
        using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await DoesTableExistAsync(connection, configuration.OutBoxTableName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await DoesHistoryExistAsync(connection, configuration.OutBoxTableName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(connection, configuration.OutBoxTableName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await GetMaxVersionAsync(connection, configuration.OutBoxTableName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await SqlitePayloadModeValidator.ValidateAsync(
            connection, configuration.OutBoxTableName,
            "Body", configuration.BinaryMessagePayload, cancellationToken);
    }

    internal static async Task<bool> DoesTableExistAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@TableName";
        command.Parameters.AddWithValue("@TableName", tableName);

        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    internal static async Task<bool> DoesHistoryExistAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var historyTableExists = await DoesTableExistAsync(
            connection, "__BrighterMigrationHistory", cancellationToken);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);

        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    internal static async Task<int> DetectCurrentVersionAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await GetTableColumnsAsync(connection, tableName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }

    internal static async Task<int> GetMaxVersionAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COALESCE(MAX([MigrationVersion]), 0) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    internal static async Task<HashSet<string>> GetTableColumnsAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info(@TableName)";
        command.Parameters.AddWithValue("@TableName", tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }
}

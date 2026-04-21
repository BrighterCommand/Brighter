using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Provisions a Spanner outbox table.
/// </summary>
public class SpannerOutboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    internal static readonly HashSet<string> V1Columns = new(StringComparer.Ordinal)
    {
        "MessageId", "Topic", "MessageType", "Timestamp", "CorrelationId",
        "ReplyTo", "ContentType", "PartitionKey", "Dispatched", "HeaderBag",
        "Body", "Source", "Type", "DataSchema", "Subject",
        "TraceParent", "TraceState", "Baggage", "WorkflowId", "JobId",
        "DataRef", "SpecVersion"
    };

    public BoxType BoxType => BoxType.Outbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = SpannerOutboxMigrations.All(configuration);
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
        using var connection = SpannerConnectionHelper.CreateConnection(configuration.ConnectionString);
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
        using var connection = SpannerConnectionHelper.CreateConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await SpannerPayloadModeValidator.ValidateAsync(
            connection, configuration.OutBoxTableName,
            "Body", configuration.BinaryMessagePayload, cancellationToken);
    }

    internal static async Task<bool> DoesTableExistAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateSelectCommand(
            "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName",
            new SpannerParameterCollection { { "TableName", SpannerDbType.String, tableName } });

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    internal static async Task<bool> DoesHistoryExistAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var historyTableExists = await DoesTableExistAsync(
            connection, SpannerBoxMigrationRunner.MigrationHistoryTable, cancellationToken);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName",
            new SpannerParameterCollection { { "BoxTableName", SpannerDbType.String, tableName } });

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    internal static async Task<int> DetectCurrentVersionAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await GetTableColumnsAsync(connection, tableName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }

    internal static async Task<int> GetMaxVersionAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateSelectCommand(
            @"SELECT COALESCE(MAX(`MigrationVersion`), 0) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName",
            new SpannerParameterCollection { { "BoxTableName", SpannerDbType.String, tableName } });

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    internal static async Task<HashSet<string>> GetTableColumnsAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateSelectCommand(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName",
            new SpannerParameterCollection { { "TableName", SpannerDbType.String, tableName } });

        var columns = new HashSet<string>(StringComparer.Ordinal);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }
}

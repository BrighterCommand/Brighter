using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

internal static class SpannerBoxDetectionHelpers
{
    public static async Task<bool> DoesTableExistAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateSelectCommand(
            "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName",
            new SpannerParameterCollection { { "TableName", SpannerDbType.String, tableName } });

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    public static async Task<bool> DoesHistoryExistAsync(
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

    public static async Task<int> GetMaxVersionAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateSelectCommand(
            @"SELECT COALESCE(MAX(`MigrationVersion`), 0) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName",
            new SpannerParameterCollection { { "BoxTableName", SpannerDbType.String, tableName } });

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public static async Task<HashSet<string>> GetTableColumnsAsync(
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

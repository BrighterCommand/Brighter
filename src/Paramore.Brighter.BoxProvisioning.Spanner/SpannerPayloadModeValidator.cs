using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Validates that the payload column type in an existing table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/>
/// on mismatch.
/// </summary>
public static class SpannerPayloadModeValidator
{
    public static async Task ValidateAsync(
        SpannerConnection connection, string tableName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateSelectCommand(
            @"SELECT SPANNER_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName",
            new SpannerParameterCollection
            {
                { "TableName", SpannerDbType.String, tableName },
                { "ColumnName", SpannerDbType.String, columnName }
            });

        var spannerType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (spannerType == null) return;

        var isBinary = spannerType.StartsWith("BYTES", StringComparison.OrdinalIgnoreCase);
        var isText = spannerType.StartsWith("STRING", StringComparison.OrdinalIgnoreCase);

        if (binaryMessagePayload && isText)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table `{tableName}`. " +
                $"Column `{columnName}` is {spannerType} (text) but binary mode was configured. " +
                "Either change the configuration or alter the column type.");
        }

        if (!binaryMessagePayload && isBinary)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table `{tableName}`. " +
                $"Column `{columnName}` is {spannerType} (binary) but text mode was configured. " +
                "Either change the configuration or alter the column type.");
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Validates that the payload column type in an existing table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/>
/// on mismatch.
/// </summary>
public static class MySqlPayloadModeValidator
{
    public static async Task ValidateAsync(
        MySqlConnection connection, string tableName, string schemaName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT DATA_TYPE FROM information_schema.columns
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var dataType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (dataType == null) return;

        var isBinary = dataType.Equals("longblob", StringComparison.OrdinalIgnoreCase)
                       || dataType.Equals("blob", StringComparison.OrdinalIgnoreCase);
        var isText = dataType.Equals("longtext", StringComparison.OrdinalIgnoreCase)
                     || dataType.Equals("text", StringComparison.OrdinalIgnoreCase);

        if (binaryMessagePayload && isText)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table `{schemaName}`.`{tableName}`. " +
                $"Column `{columnName}` is {dataType} (text) but binary mode was configured. " +
                "Either change the configuration or alter the column type.");
        }

        if (!binaryMessagePayload && isBinary)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table `{schemaName}`.`{tableName}`. " +
                $"Column `{columnName}` is {dataType} (binary) but text mode was configured. " +
                "Either change the configuration or alter the column type.");
        }
    }
}

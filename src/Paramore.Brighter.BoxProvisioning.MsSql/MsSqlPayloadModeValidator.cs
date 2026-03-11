using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Validates that the payload column type in an existing table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/>
/// on mismatch.
/// </summary>
public static class MsSqlPayloadModeValidator
{
    /// <summary>
    /// Validates the payload mode of an existing table column.
    /// </summary>
    /// <param name="connection">An open SQL connection.</param>
    /// <param name="tableName">The box table name.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="columnName">The payload column name (e.g. "Body" or "CommandBody").</param>
    /// <param name="binaryMessagePayload">Whether binary payload mode is configured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ValidateAsync(
        SqlConnection connection, string tableName, string schemaName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @SchemaName AND COLUMN_NAME = @ColumnName";
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var dataType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (dataType == null) return;

        var isBinary = dataType.Equals("varbinary", System.StringComparison.OrdinalIgnoreCase);
        var isText = dataType.Equals("nvarchar", System.StringComparison.OrdinalIgnoreCase);

        if (binaryMessagePayload && isText)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table [{schemaName}].[{tableName}]. " +
                $"Column [{columnName}] is {dataType} (text) but binary mode was configured. " +
                "Either change the configuration or alter the column type.");
        }

        if (!binaryMessagePayload && isBinary)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table [{schemaName}].[{tableName}]. " +
                $"Column [{columnName}] is {dataType} (binary) but text mode was configured. " +
                "Either change the configuration or alter the column type.");
        }
    }
}

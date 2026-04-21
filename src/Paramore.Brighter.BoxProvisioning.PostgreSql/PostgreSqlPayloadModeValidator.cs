using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Validates that the payload column type in an existing table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/>
/// on mismatch.
/// </summary>
public static class PostgreSqlPayloadModeValidator
{
    public static async Task ValidateAsync(
        NpgsqlConnection connection, string tableName, string schemaName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT data_type FROM information_schema.columns
WHERE table_schema = @SchemaName AND table_name = @TableName AND column_name = @ColumnName";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var dataType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (dataType == null) return;

        var isBinary = dataType.Equals("bytea", System.StringComparison.OrdinalIgnoreCase);
        var isText = dataType.Equals("text", System.StringComparison.OrdinalIgnoreCase);

        if (binaryMessagePayload && isText)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table \"{schemaName}\".\"{tableName}\". " +
                $"Column \"{columnName}\" is {dataType} (text) but binary mode was configured. " +
                "Either change the configuration or alter the column type.");
        }

        if (!binaryMessagePayload && isBinary)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table \"{schemaName}\".\"{tableName}\". " +
                $"Column \"{columnName}\" is {dataType} (binary) but text mode was configured. " +
                "Either change the configuration or alter the column type.");
        }
    }
}

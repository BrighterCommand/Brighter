using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Validates that the payload column type in an existing table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/>
/// on mismatch.
/// </summary>
public static class SqlitePayloadModeValidator
{
    public static async Task ValidateAsync(
        SqliteConnection connection, string tableName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT type FROM pragma_table_info(@TableName) WHERE name = @ColumnName";
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var dataType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (dataType == null) return;

        var isBinary = dataType.Equals("BLOB", StringComparison.OrdinalIgnoreCase);
        var isText = dataType.Equals("TEXT", StringComparison.OrdinalIgnoreCase)
                     || dataType.Equals("NTEXT", StringComparison.OrdinalIgnoreCase);

        if (binaryMessagePayload && isText)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table [{tableName}]. " +
                $"Column [{columnName}] is {dataType} (text) but binary mode was configured. " +
                "Either change the configuration or alter the column type.");
        }

        if (!binaryMessagePayload && isBinary)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table [{tableName}]. " +
                $"Column [{columnName}] is {dataType} (binary) but text mode was configured. " +
                "Either change the configuration or alter the column type.");
        }
    }
}

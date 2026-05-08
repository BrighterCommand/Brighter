#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Validates that the payload column type in an existing MSSQL box table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/> on mismatch
/// and returns quietly on match.
/// </summary>
/// <remarks>
/// Stateless service; safe to register as a DI singleton.
/// </remarks>
public class MsSqlPayloadModeValidator : IAmABoxPayloadModeValidator<SqlConnection>
{
    private const string DefaultSchemaName = "dbo";

    /// <summary>
    /// Validates the payload mode of an existing table column against
    /// <c>INFORMATION_SCHEMA.COLUMNS</c>.
    /// </summary>
    /// <param name="connection">An open SQL connection.</param>
    /// <param name="tableName">The unqualified box table name.</param>
    /// <param name="schemaName">Optional. Null is substituted with <c>"dbo"</c> per ADR 0057 §A.1.</param>
    /// <param name="columnName">The unqualified payload column name (e.g. "Body" or "CommandBody").</param>
    /// <param name="binaryMessagePayload">Whether binary payload mode is configured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ValidateAsync(
        SqlConnection connection, string tableName, string? schemaName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken = default)
    {
        var resolvedSchema = schemaName ?? DefaultSchemaName;

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @SchemaName AND COLUMN_NAME = @ColumnName";
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", resolvedSchema);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var dataType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (dataType == null) return;

        var isBinary = dataType.Equals("varbinary", System.StringComparison.OrdinalIgnoreCase);
        var isText = dataType.Equals("nvarchar", System.StringComparison.OrdinalIgnoreCase);

        if (binaryMessagePayload && isText)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table [{resolvedSchema}].[{tableName}]. " +
                $"Column [{columnName}] is {dataType} (text) but binary mode was configured. " +
                "Either change the configuration or alter the column type.");
        }

        if (!binaryMessagePayload && isBinary)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table [{resolvedSchema}].[{tableName}]. " +
                $"Column [{columnName}] is {dataType} (binary) but text mode was configured. " +
                "Either change the configuration or alter the column type.");
        }
    }
}

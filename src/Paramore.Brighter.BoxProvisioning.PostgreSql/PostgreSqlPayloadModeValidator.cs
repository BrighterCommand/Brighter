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
using Npgsql;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Validates that the payload column type in an existing PostgreSQL box table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/> on mismatch
/// and returns quietly on match.
/// </summary>
/// <remarks>
/// Stateless service; safe to register as a DI singleton.
/// </remarks>
public class PostgreSqlPayloadModeValidator : IAmABoxPayloadModeValidator<NpgsqlConnection>
{
    private const string DefaultSchemaName = "public";

    /// <summary>
    /// Validates the payload mode of an existing table column against
    /// <c>information_schema.columns</c>.
    /// </summary>
    /// <param name="connection">An open Npgsql connection.</param>
    /// <param name="tableName">The unqualified box table name.</param>
    /// <param name="schemaName">Optional. Null is substituted with <c>"public"</c> per ADR 0057 §A.1.</param>
    /// <param name="columnName">The unqualified payload column name (e.g. "body" or "commandbody").</param>
    /// <param name="binaryMessagePayload">Whether binary payload mode is configured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ValidateAsync(
        NpgsqlConnection connection, string tableName, string? schemaName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken = default)
    {
        var resolvedSchema = schemaName ?? DefaultSchemaName;

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT data_type FROM information_schema.columns
WHERE table_schema = @SchemaName AND table_name = @TableName AND column_name = @ColumnName";
        // information_schema.columns stores PG-folded (lowercase) identifiers. Normalize
        // configured values so mixed-case defaults match the stored folded form.
        command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(resolvedSchema));
        command.Parameters.AddWithValue("@TableName", PgIdentifier.Normalize(tableName));
        command.Parameters.AddWithValue("@ColumnName", PgIdentifier.Normalize(columnName));

        var dataType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (dataType == null) return;

        var isBinary = dataType.Equals("bytea", System.StringComparison.OrdinalIgnoreCase);
        var isText = dataType.Equals("text", System.StringComparison.OrdinalIgnoreCase);

        if (binaryMessagePayload && isText)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table \"{resolvedSchema}\".\"{tableName}\". " +
                $"Column \"{columnName}\" is {dataType} (text) but binary mode was configured. " +
                "Either change the configuration or alter the column type.");
        }

        if (!binaryMessagePayload && isBinary)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table \"{resolvedSchema}\".\"{tableName}\". " +
                $"Column \"{columnName}\" is {dataType} (binary) but text mode was configured. " +
                "Either change the configuration or alter the column type.");
        }
    }
}

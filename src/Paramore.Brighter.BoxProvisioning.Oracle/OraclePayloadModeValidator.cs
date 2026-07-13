// The MIT License (MIT)
// Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Validates that the payload column type in an existing Oracle box table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/> on mismatch
/// and returns quietly on match.
/// </summary>
/// <remarks>
/// Queries <c>ALL_TAB_COLUMNS</c> for the column's <c>DATA_TYPE</c>. Oracle stores column
/// names and owner names in uppercase by default; identifiers are uppercased before the
/// lookup so callers may pass mixed-case names without issue.
/// When <paramref name="schemaName"/> is null the current schema is resolved via
/// <c>SYS_CONTEXT('USERENV','CURRENT_SCHEMA')</c>.
/// Stateless service; safe to register as a DI singleton.
/// </remarks>
public class OraclePayloadModeValidator : IAmABoxPayloadModeValidator<OracleConnection>
{
    /// <summary>
    /// Validates the payload mode of an existing table column against <c>ALL_TAB_COLUMNS</c>.
    /// </summary>
    /// <param name="connection">An open Oracle connection.</param>
    /// <param name="tableName">The unqualified box table name.</param>
    /// <param name="schemaName">
    /// Optional. Null is resolved to <c>SYS_CONTEXT('USERENV','CURRENT_SCHEMA')</c>.
    /// </param>
    /// <param name="columnName">The payload column name (e.g. "Body" or "CommandBody").</param>
    /// <param name="binaryMessagePayload">Whether binary payload mode is configured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ValidateAsync(
        OracleConnection connection, string tableName, string? schemaName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken = default)
    {
        var resolvedSchema = schemaName is not null
            ? schemaName
            : await ResolveCurrentSchemaAsync(connection, cancellationToken);

        using var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"
SELECT DATA_TYPE FROM ALL_TAB_COLUMNS
WHERE OWNER = UPPER(:SchemaName) AND TABLE_NAME = UPPER(:TableName) AND COLUMN_NAME = UPPER(:ColumnName)";
        command.Parameters.Add(new OracleParameter("SchemaName", resolvedSchema));
        command.Parameters.Add(new OracleParameter("TableName", tableName));
        command.Parameters.Add(new OracleParameter("ColumnName", columnName));

        var dataType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (dataType == null) return;

        var isBinary = dataType.Equals("BLOB", StringComparison.OrdinalIgnoreCase);
        var isText   = dataType.Equals("CLOB",  StringComparison.OrdinalIgnoreCase)
                    || dataType.Equals("NCLOB", StringComparison.OrdinalIgnoreCase);

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

    private static async Task<string> ResolveCurrentSchemaAsync(
        OracleConnection connection, CancellationToken cancellationToken)
    {
        using var command = (OracleCommand)connection.CreateCommand();
        command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString()
            ?? throw new InvalidOperationException(
                "Could not resolve the current Oracle schema via SYS_CONTEXT('USERENV','CURRENT_SCHEMA').");
    }
}

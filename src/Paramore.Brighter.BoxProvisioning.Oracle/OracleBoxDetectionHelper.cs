// The MIT License (MIT)
// Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Oracle detection queries used by the box provisioners (pre-lock) and the migration
/// runner (under <c>DBMS_LOCK</c>). Oracle identifiers are stored in uppercase by the data
/// dictionary; comparisons against <c>ALL_TABLES</c> and <c>ALL_TAB_COLUMNS</c> are performed
/// with <c>UPPER()</c> so callers may pass mixed-case names. The column set comparer is
/// <see cref="StringComparer.OrdinalIgnoreCase"/> to match PascalCase <c>LogicalColumns</c>
/// stored on each migration against Oracle's uppercase column name values.
/// </summary>
/// <remarks>
/// Oracle DDL has implicit per-statement commit, so the <c>OracleTransaction</c> parameter
/// is accepted but not consumed. The runner relies on <c>DBMS_LOCK</c> for mutual exclusion
/// across instances.
/// <para>
/// When <paramref name="schemaName"/> is null, queries resolve the owner via
/// <c>SYS_CONTEXT('USERENV','CURRENT_SCHEMA')</c>. Stateless service; safe to register as
/// a DI singleton.
/// </para>
/// </remarks>
public class OracleBoxDetectionHelper :
    IAmAVersionDetectingMigrationHelper<OracleConnection, OracleTransaction>
{
    /// <summary>
    /// Returns true if a table with the given name exists in the given schema.
    /// </summary>
    /// <param name="schemaName">Optional. Null is resolved to <c>SYS_CONTEXT('USERENV','CURRENT_SCHEMA')</c>.</param>
    /// <param name="transaction">Accepted and ignored — Oracle DDL auto-commits.</param>
    public async Task<bool> DoesTableExistAsync(
        OracleConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        OracleTransaction? transaction = null)
    {
        var resolvedSchema = await ResolveSchemaAsync(connection, schemaName, cancellationToken);

        using var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"
SELECT COUNT(1) FROM ALL_TABLES
WHERE OWNER = UPPER(:SchemaName) AND TABLE_NAME = UPPER(:TableName)";
        command.Parameters.Add(new OracleParameter("SchemaName", resolvedSchema));
        command.Parameters.Add(new OracleParameter("TableName", tableName));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    /// <summary>
    /// Returns true if the migration history table exists and has at least one row for the given
    /// box table.
    /// </summary>
    /// <param name="schemaName">Optional. Null is resolved via <c>SYS_CONTEXT</c>.</param>
    /// <param name="historySchema">Accepted and ignored — Oracle keeps history in the
    /// connected user's schema; <see cref="MigrationHistoryScope.PerSchema"/> is a no-op.</param>
    /// <param name="transaction">Accepted and ignored — Oracle DDL auto-commits.</param>
    public async Task<bool> DoesHistoryExistAsync(
        OracleConnection connection, string tableName, string? schemaName, string? historySchema,
        CancellationToken cancellationToken = default,
        OracleTransaction? transaction = null)
    {
        _ = historySchema;
        var historyTableExists = await DoesTableExistAsync(
            connection, "__BRIGHTERMIGRATIONHISTORY", schemaName, cancellationToken);
        if (!historyTableExists)
            return false;

        var resolvedSchema = await ResolveSchemaAsync(connection, schemaName, cancellationToken);

        using var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"
SELECT COUNT(1) FROM __BRIGHTERMIGRATIONHISTORY
WHERE BoxTableName = :BoxTableName AND SchemaName = :SchemaName";
        command.Parameters.Add(new OracleParameter("BoxTableName", tableName));
        command.Parameters.Add(new OracleParameter("SchemaName", resolvedSchema));

        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(raw) > 0;
    }

    /// <summary>
    /// Returns the highest migration version recorded in history for the given box table, or 0
    /// if no rows exist.
    /// </summary>
    /// <param name="schemaName">Optional. Null is resolved via <c>SYS_CONTEXT</c>.</param>
    /// <param name="historySchema">Accepted and ignored — Oracle keeps history in the connected user's schema.</param>
    /// <param name="transaction">Accepted and ignored — Oracle DDL auto-commits.</param>
    public async Task<int> GetMaxVersionAsync(
        OracleConnection connection, string tableName, string? schemaName, string? historySchema,
        CancellationToken cancellationToken = default,
        OracleTransaction? transaction = null)
    {
        _ = historySchema;
        var resolvedSchema = await ResolveSchemaAsync(connection, schemaName, cancellationToken);

        using var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"
SELECT COALESCE(MAX(MigrationVersion), 0) FROM __BRIGHTERMIGRATIONHISTORY
WHERE BoxTableName = :BoxTableName AND SchemaName = :SchemaName";
        command.Parameters.Add(new OracleParameter("BoxTableName", tableName));
        command.Parameters.Add(new OracleParameter("SchemaName", resolvedSchema));

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    /// <summary>
    /// Reads the column name set for the given table from <c>ALL_TAB_COLUMNS</c>.
    /// Oracle stores column names in uppercase; the returned set uses
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> for PascalCase comparisons.
    /// </summary>
    /// <param name="schemaName">Optional. Null is resolved via <c>SYS_CONTEXT</c>.</param>
    /// <param name="transaction">Accepted and ignored — Oracle DDL auto-commits.</param>
    public async Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
        OracleConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        OracleTransaction? transaction = null)
    {
        return await GetTableColumnsAsHashSetAsync(connection, tableName, schemaName, cancellationToken);
    }

    /// <summary>
    /// Detects the current logical schema version of a box table by inspecting its column set.
    /// Returns one of three values per ADR 0057 §3:
    /// <list type="bullet">
    ///   <item><description><c>-1</c> when the discriminator column is absent (the table is not a Brighter box).</description></item>
    ///   <item><description><c>0</c> when the discriminator is present but the V1 column set is incomplete (unknown schema).</description></item>
    ///   <item><description><c>V &gt;= 1</c> for the highest version whose cumulative <c>LogicalColumns</c> is a subset of the actual columns.</description></item>
    /// </list>
    /// </summary>
    /// <param name="boxType">Selects the discriminator: <c>HeaderBag</c> for outbox, <c>CommandBody</c> for inbox.</param>
    /// <param name="schemaName">Optional. Null is resolved via <c>SYS_CONTEXT</c>.</param>
    /// <param name="transaction">Accepted and ignored — Oracle DDL auto-commits.</param>
    public async Task<int> DetectCurrentVersionAsync(
        OracleConnection connection, string tableName, string? schemaName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken = default,
        OracleTransaction? transaction = null)
    {
        var actualColumns = await GetTableColumnsAsHashSetAsync(
            connection, tableName, schemaName, cancellationToken);

        var discriminator = DiscriminatorFor(boxType);
        if (!actualColumns.Contains(discriminator))
            return -1;

        if (migrations.Count == 0 || !actualColumns.IsSupersetOf(migrations[0].LogicalColumns))
            return 0;

        var matched = migrations[0].Version;
        for (var i = 1; i < migrations.Count; i++)
        {
            if (!actualColumns.IsSupersetOf(migrations[i].LogicalColumns))
                break;
            matched = migrations[i].Version;
        }
        return matched;
    }

    /// <summary>
    /// The discriminator column that distinguishes a Brighter outbox/inbox table from any other
    /// table that happens to share its name.
    /// </summary>
    public string DiscriminatorFor(BoxType boxType) => boxType switch
    {
        BoxType.Outbox => "HeaderBag",
        BoxType.Inbox => "CommandBody",
        _ => throw new ArgumentOutOfRangeException(nameof(boxType), boxType, "Unknown BoxType")
    };

    internal async Task<HashSet<string>> GetTableColumnsAsHashSetAsync(
        OracleConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken)
    {
        var resolvedSchema = await ResolveSchemaAsync(connection, schemaName, cancellationToken);

        using var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"
SELECT COLUMN_NAME FROM ALL_TAB_COLUMNS
WHERE OWNER = UPPER(:SchemaName) AND TABLE_NAME = UPPER(:TableName)";
        command.Parameters.Add(new OracleParameter("SchemaName", resolvedSchema));
        command.Parameters.Add(new OracleParameter("TableName", tableName));

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private static async Task<string> ResolveSchemaAsync(
        OracleConnection connection, string? schemaName,
        CancellationToken cancellationToken)
    {
        if (schemaName is not null)
            return schemaName;

        using var command = (OracleCommand)connection.CreateCommand();
        command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString()
            ?? throw new InvalidOperationException(
                "Could not resolve the current Oracle schema via SYS_CONTEXT('USERENV','CURRENT_SCHEMA').");
    }
}

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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Shared PostgreSQL detection queries used by the box provisioners (pre-lock) and the
/// migration runner (under <c>pg_try_advisory_lock</c>). Each method accepts an optional
/// <see cref="NpgsqlTransaction"/> so the runner can bind queries to its lock-bearing
/// transaction; provisioners pass <c>null</c>.
/// </summary>
/// <remarks>
/// Discriminator and column-set comparisons are case-sensitive (<see cref="StringComparer.Ordinal"/>)
/// against the names returned by <c>information_schema.columns</c>, which folds unquoted
/// identifiers to lowercase per PostgreSQL semantics. <c>LogicalColumns</c> on each migration
/// must therefore be lowercase per ADR 0057 §1.
/// <para>
/// Stateless service; safe to register as a DI singleton.
/// </para>
/// </remarks>
public class PostgreSqlBoxDetectionHelper :
    IAmAVersionDetectingMigrationHelper<NpgsqlConnection, NpgsqlTransaction>
{
    /// <summary>
    /// Returns true if a table with the given name exists in the given schema.
    /// </summary>
    public async Task<bool> DoesTableExistAsync(
        NpgsqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        NpgsqlTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName!);
        command.Parameters.AddWithValue("@TableName", tableName);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>
    /// Returns true if the migration history table exists and has at least one row for the
    /// given box table.
    /// </summary>
    public async Task<bool> DoesHistoryExistAsync(
        NpgsqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        NpgsqlTransaction? transaction = null)
    {
        var historyTableExists = await DoesTableExistAsync(
            connection, "__BrighterMigrationHistory", "public", cancellationToken, transaction);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = @"
SELECT COUNT(1) FROM ""public"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName!);

        try
        {
            var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
            return count > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // TOCTOU: another connection dropped __BrighterMigrationHistory between our
            // existence check and this count query. In production this cannot happen — the
            // history table is created once and never dropped — but parallel tests do drop
            // it deliberately, and a "table dropped between two queries" outcome is
            // semantically equivalent to "no history". Returning false lets the caller fall
            // through to the runner, which re-creates the history table under its lock.
            return false;
        }
    }

    /// <summary>
    /// Returns the highest migration version recorded in history for the given box table,
    /// or 0 if no rows exist.
    /// </summary>
    public async Task<int> GetMaxVersionAsync(
        NpgsqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        NpgsqlTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = @"
SELECT COALESCE(MAX(""MigrationVersion""), 0) FROM ""public"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName!);

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>
    /// Reads the column name set for the given table from <c>information_schema.columns</c>.
    /// Comparison is case-sensitive; PostgreSQL returns unquoted identifiers folded to lowercase.
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
        NpgsqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        NpgsqlTransaction? transaction = null)
    {
        return await GetTableColumnsAsHashSetAsync(
            connection, tableName, schemaName, cancellationToken, transaction);
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
    /// <param name="boxType">Selects the discriminator: <c>headerbag</c> for outbox, <c>commandbody</c> for inbox.</param>
    public async Task<int> DetectCurrentVersionAsync(
        NpgsqlConnection connection, string tableName, string? schemaName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken = default,
        NpgsqlTransaction? transaction = null)
    {
        var actualColumns = await GetTableColumnsAsHashSetAsync(
            connection, tableName, schemaName, cancellationToken, transaction);

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
    /// The discriminator column (lowercase per PostgreSQL folding) that distinguishes a
    /// Brighter outbox/inbox table from any other table that happens to share its name.
    /// </summary>
    public string DiscriminatorFor(BoxType boxType) => boxType switch
    {
        BoxType.Outbox => "headerbag",
        BoxType.Inbox => "commandbody",
        _ => throw new ArgumentOutOfRangeException(nameof(boxType), boxType, "Unknown BoxType")
    };

    internal async Task<HashSet<string>> GetTableColumnsAsHashSetAsync(
        NpgsqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = @"
SELECT column_name FROM information_schema.columns
WHERE table_schema = @SchemaName AND table_name = @TableName";
        command.Parameters.AddWithValue("@SchemaName", schemaName!);
        command.Parameters.AddWithValue("@TableName", tableName);

        var columns = new HashSet<string>(StringComparer.Ordinal);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }
}

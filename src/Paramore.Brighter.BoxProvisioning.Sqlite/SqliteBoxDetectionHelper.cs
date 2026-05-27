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
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Shared SQLite detection queries used by the box provisioners (pre-lock) and the migration
/// runner (under lock). Each method accepts an optional <see cref="SqliteTransaction"/> so the
/// runner can bind queries to its BEGIN IMMEDIATE transaction; provisioners pass <c>null</c>.
/// </summary>
/// <remarks>
/// Stateless service; safe to register as a DI singleton.
/// <para>
/// SQLite has no schema concept, so the <c>schemaName</c> parameter on every schema-bearing
/// method is accepted and ignored — including <c>null</c>. The parameter exists only to satisfy
/// the role-interface signature shared with the relational backends that DO partition by schema
/// (MSSQL, Postgres, MySQL).
/// </para>
/// </remarks>
public class SqliteBoxDetectionHelper :
    IAmAVersionDetectingMigrationHelper<SqliteConnection, SqliteTransaction>
{
    /// <summary>
    /// Returns true if a table with the given name exists in the database.
    /// </summary>
    /// <param name="schemaName">Accepted and ignored — SQLite has no schema concept.</param>
    public async Task<bool> DoesTableExistAsync(
        SqliteConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@TableName";
        command.Parameters.AddWithValue("@TableName", tableName);

        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    /// <summary>
    /// Returns true if the migration history table exists and has at least one row for the
    /// given box table.
    /// </summary>
    /// <param name="schemaName">Accepted and ignored — SQLite has no schema concept.</param>
    public async Task<bool> DoesHistoryExistAsync(
        SqliteConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        SqliteTransaction? transaction = null)
    {
        var historyTableExists = await DoesTableExistAsync(
            connection, "__BrighterMigrationHistory", schemaName, cancellationToken, transaction);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);

        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    /// <summary>
    /// Returns the highest migration version recorded in history for the given box table,
    /// or 0 if no rows exist.
    /// </summary>
    /// <param name="schemaName">Accepted and ignored — SQLite has no schema concept.</param>
    public async Task<int> GetMaxVersionAsync(
        SqliteConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = @"
SELECT COALESCE(MAX([MigrationVersion]), 0) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    /// <summary>
    /// Reads the column name set for the given table from <c>pragma_table_info</c>,
    /// case-insensitively.
    /// </summary>
    /// <param name="schemaName">Accepted and ignored — SQLite has no schema concept.</param>
    public async Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
        SqliteConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        SqliteTransaction? transaction = null)
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
    /// <param name="boxType">Selects the discriminator: <c>HeaderBag</c> for outbox, <c>CommandBody</c> for inbox.</param>
    /// <param name="schemaName">Accepted and ignored — SQLite has no schema concept.</param>
    public async Task<int> DetectCurrentVersionAsync(
        SqliteConnection connection, string tableName, string? schemaName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken = default,
        SqliteTransaction? transaction = null)
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
    /// The discriminator column that distinguishes a Brighter outbox/inbox table from any
    /// other table that happens to share its name.
    /// </summary>
    public string DiscriminatorFor(BoxType boxType) => boxType switch
    {
        BoxType.Outbox => "HeaderBag",
        BoxType.Inbox => "CommandBody",
        _ => throw new ArgumentOutOfRangeException(nameof(boxType), boxType, "Unknown BoxType")
    };

    internal async Task<HashSet<string>> GetTableColumnsAsHashSetAsync(
        SqliteConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = "SELECT name FROM pragma_table_info(@TableName)";
        command.Parameters.AddWithValue("@TableName", tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }
}

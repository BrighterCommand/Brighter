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
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Shared MySQL detection queries used by the box provisioners (pre-lock) and the migration
/// runner (under <c>GET_LOCK</c>). MySQL identifiers are case-insensitive on lookup; the column
/// set comparer is <see cref="StringComparer.OrdinalIgnoreCase"/> so the comparisons match the
/// PascalCase <c>LogicalColumns</c> stored on each migration per ADR 0057 §1 and the MySQL
/// builder DDL.
/// </summary>
/// <remarks>
/// MySQL DDL has implicit per-statement commit (ADR 0057 §5a), so unlike MSSQL/Postgres this
/// helper accepts but does not consume the <see cref="MySqlTransaction"/> parameter — there is
/// no whole-chain transaction to bind to. The runner relies on <c>GET_LOCK</c> for mutual
/// exclusion across instances and on the unique PK on
/// <c>__BrighterMigrationHistory(SchemaName, BoxTableName, MigrationVersion)</c> to suppress
/// duplicate synthetic rows under races.
/// <para>
/// Stateless service; safe to register as a DI singleton.
/// </para>
/// </remarks>
public class MySqlBoxDetectionHelper :
    IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction>
{
    /// <summary>
    /// Returns true if a table with the given name exists in the given schema.
    /// </summary>
    /// <param name="schemaName">Optional. Null is substituted with <c>connection.Database</c> per ADR 0057 §A.1.</param>
    /// <param name="transaction">Accepted and ignored — MySQL DDL auto-commits per ADR 0057 §5a.</param>
    public async Task<bool> DoesTableExistAsync(
        MySqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        MySqlTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM information_schema.tables
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName ?? connection.Database);
        command.Parameters.AddWithValue("@TableName", tableName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToBoolean(result);
    }

    /// <summary>
    /// Returns true if the migration history table exists and has at least one row for the given
    /// box table.
    /// </summary>
    /// <param name="schemaName">
    /// Optional. Filters the history rows by their <c>SchemaName</c> column. Null is substituted
    /// with <c>connection.Database</c> Note: this parameter scopes the content lookup, NOT the
    /// existence check for the history table itself — the history table always lives in the
    /// connection's bound database, even when the box table is provisioned in a different schema 
    /// </param>
    /// <param name="historySchema">Accepted and ignored — MySQL's history table always lives in
    /// the connection-bound database; <see cref="MigrationHistoryScope.PerSchema"/> is a no-op.</param>
    /// <param name="transaction">Accepted and ignored — MySQL DDL auto-commits per ADR 0057 §5a.</param>
    public async Task<bool> DoesHistoryExistAsync(
        MySqlConnection connection, string tableName, string? schemaName, string? historySchema,
        CancellationToken cancellationToken = default,
        MySqlTransaction? transaction = null)
    {
        _ = historySchema; // MySQL keeps history in connection.Database; PerSchema is a no-op here.
        // The history table always resides in connection.Database (the MySQL runner's
        // EnsureHistoryTableAsync emits an unqualified CREATE TABLE IF NOT EXISTS targeting
        // DATABASE()). Probe its existence against connection.Database — not the
        // box-table SchemaName — so a non-default SchemaName configuration does not
        // misreport HistoryAbsent and trigger a redundant bootstrap path.
        var historyTableExists = await DoesTableExistAsync(
            connection, "__BrighterMigrationHistory", connection.Database, cancellationToken);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `SchemaName` = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName ?? connection.Database);

        var raw = await command.ExecuteScalarAsync(cancellationToken);
        var count = raw is long l
            ? l
            : throw new InvalidOperationException(
                $"DoesHistoryExistAsync: COUNT(1) over __BrighterMigrationHistory for '{schemaName ?? connection.Database}.{tableName}' returned null.");
        return count > 0;
    }

    /// <summary>
    /// Returns the highest migration version recorded in history for the given box table, or 0
    /// if no rows exist.
    /// </summary>
    /// <param name="schemaName">Optional. Null is substituted with <c>connection.Database</c></param>
    /// <param name="historySchema">Accepted and ignored — MySQL keeps history in the
    /// connection-bound database; <see cref="MigrationHistoryScope.PerSchema"/> is a no-op.</param>
    /// <param name="transaction">Accepted and ignored — MySQL DDL auto-commits.</param>
    public async Task<int> GetMaxVersionAsync(
        MySqlConnection connection, string tableName, string? schemaName, string? historySchema,
        CancellationToken cancellationToken = default,
        MySqlTransaction? transaction = null)
    {
        _ = historySchema; // MySQL keeps history in connection.Database; PerSchema is a no-op here.
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COALESCE(MAX(`MigrationVersion`), 0) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `SchemaName` = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName ?? connection.Database);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    /// <summary>
    /// Reads the column name set for the given table from <c>information_schema.columns</c>,
    /// case-insensitively (MySQL identifiers are case-insensitive on lookup).
    /// </summary>
    /// <param name="schemaName">Optional. Null is substituted with <c>connection.Database</c></param>
    /// <param name="transaction">Accepted and ignored — MySQL DDL auto-commits</param>
    public async Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
        MySqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        MySqlTransaction? transaction = null)
    {
        return await GetTableColumnsAsHashSetAsync(
            connection, tableName, schemaName, cancellationToken);
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
    /// <param name="schemaName">Optional. Null is substituted with <c>connection.Database</c></param>
    /// <param name="transaction">Accepted and ignored — MySQL DDL auto-commits</param>
    public async Task<int> DetectCurrentVersionAsync(
        MySqlConnection connection, string tableName, string? schemaName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken = default,
        MySqlTransaction? transaction = null)
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
        MySqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_NAME FROM information_schema.columns
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName";
        command.Parameters.AddWithValue("@SchemaName", schemaName ?? connection.Database);
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

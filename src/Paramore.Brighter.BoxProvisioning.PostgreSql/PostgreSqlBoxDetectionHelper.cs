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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;

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
    private const string DefaultSchemaName = "public";

    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the detection helper with an optional logger. When unspecified, falls back
    /// to <see cref="ApplicationLogging.CreateLogger{T}"/>. Existing callers that use the
    /// parameterless form continue to work — the logger is currently only consumed for the
    /// rare <c>UndefinedTable</c>-swallow Debug emission in
    /// <see cref="DoesHistoryExistAsync"/>; the helper remains a safe DI singleton.
    /// </summary>
    public PostgreSqlBoxDetectionHelper(ILogger? logger = null)
    {
        _logger = logger ?? ApplicationLogging.CreateLogger<PostgreSqlBoxDetectionHelper>();
    }

    /// <summary>
    /// Returns true if a table with the given name exists in the given schema.
    /// </summary>
    /// <param name="schemaName">Optional. Null is substituted with <c>"public"</c> per ADR 0057 §A.1.</param>
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
        // information_schema.tables stores PG-folded (lowercase) names. Normalize so a
        // mixed-case configured value (e.g. default "Outbox") matches the stored "outbox".
        // The internal __BrighterMigrationHistory system-table existence check does NOT
        // route through this method — its CREATE DDL quotes the name and therefore
        // case-preserves it in pg_class, so DoesHistoryExistAsync inlines a literal-name
        // lookup that bypasses the fold normalization done here.
        command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(schemaName ?? DefaultSchemaName));
        command.Parameters.AddWithValue("@TableName", PgIdentifier.Normalize(tableName));

        // Pattern-match rather than (bool)raw! so a driver returning null surfaces as a named
        // InvalidOperationException instead of a bare NullReferenceException — Npgsql has
        // returned null from ExecuteScalarAsync under server-side errors in the wild.
        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return raw is bool b
            ? b
            : throw new InvalidOperationException(
                $"DoesTableExistAsync: EXISTS over information_schema.tables for '{schemaName ?? DefaultSchemaName}.{tableName}' returned null.");
    }

    /// <summary>
    /// Returns true if the migration history table exists and has at least one row for the
    /// given box table.
    /// </summary>
    /// <param name="schemaName">Optional. Null is substituted with <c>"public"</c> per ADR 0057 §A.1.</param>
    public async Task<bool> DoesHistoryExistAsync(
        NpgsqlConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        NpgsqlTransaction? transaction = null)
    {
        // System-table existence: __BrighterMigrationHistory is created via quoted DDL
        // (`CREATE TABLE IF NOT EXISTS "public"."__BrighterMigrationHistory"`), so its name
        // is case-PRESERVED in pg_class. Routing through DoesTableExistAsync would lowercase
        // the lookup to `'__brightermigrationhistory'` and miss. Inline the literal check.
        using (var existsCmd = connection.CreateCommand())
        {
            if (transaction != null) existsCmd.Transaction = transaction;
            existsCmd.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'public' AND TABLE_NAME = '__BrighterMigrationHistory')";
            var existsRaw = await existsCmd.ExecuteScalarAsync(cancellationToken);
            var historyTableExists = existsRaw is bool b && b;
            if (!historyTableExists)
                return false;
        }

        using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;
        command.CommandText = @"
SELECT COUNT(1) FROM ""public"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @SchemaName";
        // History rows are stored with PG-folded (lowercase) identifiers by the runner so
        // that lookups remain consistent across mixed-case configured values; normalize
        // here too. See PostgreSqlBoxMigrationRunner.InsertHistory.
        command.Parameters.AddWithValue("@BoxTableName", PgIdentifier.Normalize(tableName));
        command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(schemaName ?? DefaultSchemaName));

        try
        {
            var raw = await command.ExecuteScalarAsync(cancellationToken);
            var count = raw is long l
                ? l
                : throw new InvalidOperationException(
                    $"DoesHistoryExistAsync: COUNT(1) over __BrighterMigrationHistory for '{schemaName ?? DefaultSchemaName}.{tableName}' returned null.");
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
            //
            // Per PR #4039 reviewer Nit (item 4485697019): Debug-level log so the swallow is
            // observable if a future contributor adds a DROP-history operation outside test
            // setup. Production should never see this; if it does, the deployment has a
            // history-table-management bug worth surfacing.
            _logger.LogDebug(
                ex,
                "PostgreSqlBoxDetectionHelper.DoesHistoryExistAsync: swallowed UndefinedTable on COUNT(1) after positive existence check — __BrighterMigrationHistory was dropped between the two queries. Returning false. In production this indicates a history-table-management bug; in parallel tests it is expected because tests drop the table deliberately.");
            return false;
        }
    }

    /// <summary>
    /// Returns the highest migration version recorded in history for the given box table,
    /// or 0 if no rows exist.
    /// </summary>
    /// <param name="schemaName">Optional. Null is substituted with <c>"public"</c> per ADR 0057 §A.1.</param>
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
        command.Parameters.AddWithValue("@BoxTableName", PgIdentifier.Normalize(tableName));
        command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(schemaName ?? DefaultSchemaName));

        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return raw is int i
            ? i
            : throw new InvalidOperationException(
                $"GetMaxVersionAsync: COALESCE(MAX(\"MigrationVersion\"), 0) for '{schemaName ?? DefaultSchemaName}.{tableName}' returned null.");
    }

    /// <summary>
    /// Reads the column name set for the given table from <c>information_schema.columns</c>.
    /// Comparison is case-sensitive; PostgreSQL returns unquoted identifiers folded to lowercase.
    /// </summary>
    /// <param name="schemaName">Optional. Null is substituted with <c>"public"</c> per ADR 0057 §A.1.</param>
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
    /// <param name="schemaName">Optional. Null is substituted with <c>"public"</c> per ADR 0057 §A.1.</param>
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
        command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(schemaName ?? DefaultSchemaName));
        command.Parameters.AddWithValue("@TableName", PgIdentifier.Normalize(tableName));

        var columns = new HashSet<string>(StringComparer.Ordinal);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }
}

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
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Spanner detection queries used by the box provisioners and the migration runner. Spanner
/// implements only the base detection-helper role — per ADR 0057 §6 the Spanner box surface
/// is degenerate (fresh-install only, no V_k chain), so this class does NOT implement
/// <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}"/> and offers
/// no <c>DetectCurrentVersionAsync</c>.
/// </summary>
/// <remarks>
/// Stateless service; safe to register as a DI singleton.
/// <para>
/// Spanner has no schema concept, so the <c>schemaName</c> parameter on every schema-bearing
/// method is accepted and ignored — including <c>null</c>. The parameter exists only to
/// satisfy the role-interface signature shared with the relational backends that DO
/// partition by schema (MSSQL, Postgres, MySQL).
/// </para>
/// <para>
/// Spanner DDL is single-statement and does not enrol in client transactions, so the
/// <see cref="SpannerTransaction"/> parameter on every method is also accepted and ignored.
/// The parameter exists only to satisfy the generic role-interface signature.
/// </para>
/// </remarks>
public class SpannerBoxDetectionHelper :
    IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>
{
    /// <summary>
    /// Returns true if a table with the given name exists in the database.
    /// </summary>
    /// <param name="schemaName">Accepted and ignored — Spanner has no schema concept.</param>
    /// <param name="transaction">Accepted and ignored — Spanner DDL is single-statement
    /// and does not enrol in client transactions.</param>
    public async Task<bool> DoesTableExistAsync(
        SpannerConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        SpannerTransaction? transaction = null)
    {
        using var command = connection.CreateSelectCommand(
            "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName",
            new SpannerParameterCollection { { "TableName", SpannerDbType.String, tableName } });

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    /// <summary>
    /// Returns true if the migration history table exists and has at least one row for the
    /// given box table.
    /// </summary>
    /// <param name="schemaName">Accepted and ignored — Spanner has no schema concept.</param>
    /// <param name="historySchema">Accepted and ignored — Spanner has no schema concept.</param>
    /// <param name="transaction">Accepted and ignored — Spanner DDL is single-statement.</param>
    public async Task<bool> DoesHistoryExistAsync(
        SpannerConnection connection, string tableName, string? schemaName, string? historySchema,
        CancellationToken cancellationToken = default,
        SpannerTransaction? transaction = null)
    {
        _ = historySchema; // Spanner has no schema concept; PerSchema is a no-op here.
        var historyTableExists = await DoesTableExistAsync(
            connection, SpannerBoxMigrationRunner.MigrationHistoryTable,
            schemaName, cancellationToken, transaction);
        if (!historyTableExists)
            return false;

        using var command = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName",
            new SpannerParameterCollection { { "BoxTableName", SpannerDbType.String, tableName } });

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    /// <summary>
    /// Returns the highest migration version recorded in history for the given box table,
    /// or 0 if no rows exist.
    /// </summary>
    /// <param name="schemaName">Accepted and ignored — Spanner has no schema concept.</param>
    /// <param name="historySchema">Accepted and ignored — Spanner has no schema concept.</param>
    /// <param name="transaction">Accepted and ignored — Spanner DDL is single-statement.</param>
    public async Task<int> GetMaxVersionAsync(
        SpannerConnection connection, string tableName, string? schemaName, string? historySchema,
        CancellationToken cancellationToken = default,
        SpannerTransaction? transaction = null)
    {
        _ = historySchema; // Spanner has no schema concept; PerSchema is a no-op here.
        using var command = connection.CreateSelectCommand(
            @"SELECT COALESCE(MAX(`MigrationVersion`), 0) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName",
            new SpannerParameterCollection { { "BoxTableName", SpannerDbType.String, tableName } });

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    /// <summary>
    /// Reads the column name set for the given table from <c>INFORMATION_SCHEMA.COLUMNS</c>,
    /// case-sensitively (Spanner identifiers are case-sensitive, unlike the relational backends).
    /// </summary>
    /// <param name="schemaName">Accepted and ignored — Spanner has no schema concept.</param>
    /// <param name="transaction">Accepted and ignored — Spanner DDL is single-statement.</param>
    public async Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
        SpannerConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        SpannerTransaction? transaction = null)
    {
        return await GetTableColumnsAsHashSetAsync(
            connection, tableName, schemaName, cancellationToken, transaction);
    }

    /// <summary>
    /// The discriminator column that distinguishes a Brighter outbox/inbox table from any
    /// other table that happens to share its name.
    /// </summary>
    public string DiscriminatorFor(BoxType boxType) => boxType switch
    {
        BoxType.Outbox => "HeaderBag",
        BoxType.Inbox => "CommandBody",
        _ => throw new ArgumentOutOfRangeException(nameof(boxType), boxType, "Unsupported box type")
    };

    internal async Task<HashSet<string>> GetTableColumnsAsHashSetAsync(
        SpannerConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken,
        SpannerTransaction? transaction = null)
    {
        using var command = connection.CreateSelectCommand(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName",
            new SpannerParameterCollection { { "TableName", SpannerDbType.String, tableName } });

        var columns = new HashSet<string>(StringComparer.Ordinal);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }
}

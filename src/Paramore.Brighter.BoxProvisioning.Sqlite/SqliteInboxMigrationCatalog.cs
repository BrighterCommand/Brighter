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
using Paramore.Brighter.Inbox.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Defines the migration history for SQLite inbox tables.
/// </summary>
/// <remarks>
/// V1's <c>UpScript</c> is the literal historical baseline DDL — the first SQLite inbox
/// builder shape (commit <c>695522367</c>, March 2019). Spec 0027 R1 split "live builder
/// DDL" away from V1.UpScript: the fresh-install fast path (ADR 0057 §3) now sources its
/// DDL from <see cref="FreshInstallDdl"/>, so V1.UpScript is free to carry the honest
/// historical shape. V2 adds the <c>ContextKey</c> column (spec version stamped 787c31c52,
/// Oct 2018) via a plain <c>ALTER TABLE ADD COLUMN</c> — SQLite's grammar lacks
/// <c>ALTER TABLE ADD COLUMN IF NOT EXISTS</c>, so the existence guard lives in
/// <see cref="IAmABoxMigration.IdempotencyCheckSql"/> per ADR 0057 §6 (probing
/// <c>pragma_table_info</c>). The <c>SqliteBoxMigrationRunner</c> evaluates this scalar
/// before running <c>UpScript</c> and skips the ALTER (still stamping history) when the
/// column is already present.
/// <para>
/// Born-past-V1 asymmetry: the SQLite inbox first shipped <em>with</em> <c>ContextKey</c>
/// already present — there is no pre-ContextKey SQLite inbox in the wild despite the
/// October-2018 spec version stamp on V2 (the catch-up exists for cross-backend
/// consistency, not because pre-ContextKey rows ever shipped on SQLite). V1.UpScript
/// therefore creates a table whose physical column set already includes <c>ContextKey</c>;
/// V2's <see cref="IAmABoxMigration.IdempotencyCheckSql"/> probe sees the column and skips
/// the ALTER on chain replay. V1.LogicalColumns remains the "logical pre-V2" set (no
/// <c>ContextKey</c>) so the bootstrap-detection contract (ADR 0057 §4) can still
/// distinguish a hypothetical pre-V2 table.
/// </para>
/// </remarks>
public class SqliteInboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2AddedColumns = ["ContextKey"];

    // Literal historical SQLite inbox DDL extracted from commit 695522367 (March 2019).
    // The original source was a concatenated string literal — preserved verbatim here. The
    // table first shipped with ContextKey already present — see the born-past-V1 note in
    // the class remarks. {0} = table name (validated).
    // The table identifier is bracket-quoted so legal-but-reserved SQLite keyword names
    // bootstrap correctly — V2 already bracket-quotes the table identifier, so V1 is the
    // only asymmetric step. Per PR #4039 reviewer item F2-1.
    private const string V1HistoricalDdl =
        "CREATE TABLE [{0}] ("
        + "CommandId uniqueidentifier CONSTRAINT PK_MessageId PRIMARY KEY,"
        + "CommandType nvarchar(256),"
        + "CommandBody ntext,"
        + "Timestamp dateTime,"
        + "ContextKey nvarchar(256)"
        + ")";

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.InBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        return SqliteInboxBuilder.GetDDL(configuration.InBoxTableName, configuration.BinaryMessagePayload);
    }

    /// <summary>
    /// Returns all migrations for the SQLite inbox, ordered by version.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V2.</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var table = configuration.InBoxTableName;

        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: string.Format(V1HistoricalDdl, table),
                LogicalColumns: Cumulative(1)),

            new BoxMigration(
                Version: 2,
                Description: "Add ContextKey column",
                UpScript: $"ALTER TABLE [{table}] ADD COLUMN [ContextKey] TEXT NULL;",
                LogicalColumns: Cumulative(2),
                SourceReference: "787c31c52",
                IdempotencyCheckSql: $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='ContextKey';")
        ];
    }

    private static IReadOnlyCollection<string> Cumulative(int upToVersion)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (upToVersion >= 1) { set.UnionWith(s_v1Columns); }
        if (upToVersion >= 2) { set.UnionWith(s_v2AddedColumns); }
        return set;
    }
}

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
/// V1 is the fresh-install baseline whose <c>UpScript</c> is the live
/// <see cref="SqliteInboxBuilder"/> DDL (per ADR 0057 §3 fresh-install fast path). V2 adds the
/// <c>ContextKey</c> column (787c31c52, Oct 2018) via a plain <c>ALTER TABLE ADD COLUMN</c>
/// — SQLite's grammar lacks <c>ALTER TABLE ADD COLUMN IF NOT EXISTS</c>, so the existence
/// guard lives in <see cref="IAmABoxMigration.IdempotencyCheckSql"/> per ADR 0057 §6
/// (probing <c>pragma_table_info</c>). The <c>SqliteBoxMigrationRunner</c> evaluates this
/// scalar before running <c>UpScript</c> and skips the ALTER (still stamping history) when
/// the column is already present.
/// </remarks>
public static class SqliteInboxMigrations
{
    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2AddedColumns = ["ContextKey"];

    /// <summary>
    /// Returns all migrations for the SQLite inbox, ordered by version.
    /// </summary>
    /// <param name="config">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V2.</returns>
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        var table = config.InBoxTableName;

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: SqliteInboxBuilder.GetDDL(table, config.BinaryMessagePayload),
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

    private static ISet<string> Cumulative(int upToVersion)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (upToVersion >= 1) { set.UnionWith(s_v1Columns); }
        if (upToVersion >= 2) { set.UnionWith(s_v2AddedColumns); }
        return set;
    }
}

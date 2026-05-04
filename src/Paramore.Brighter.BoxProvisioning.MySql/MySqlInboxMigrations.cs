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
using Paramore.Brighter.Inbox.MySql;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Defines the migration history for MySQL inbox tables.
/// </summary>
/// <remarks>
/// V1 is the pre-October-2018 baseline (before <c>ContextKey</c> was added in commit
/// <c>787c31c52</c>); V2 adds <c>ContextKey</c> via the MySQL <c>information_schema.columns</c>
/// + prepared-statement idempotency pattern from ADR 0057 §5. V1.UpScript stays the live
/// <see cref="MySqlInboxBuilder"/> DDL (per ADR §3 fresh-install fast path) — only V1's
/// <see cref="IAmABoxMigration.LogicalColumns"/> reflects the historical 4-column shape used
/// for detection.
/// <para>
/// LogicalColumns are PascalCase with <see cref="StringComparer.OrdinalIgnoreCase"/> — MySQL
/// identifiers are case-insensitive on lookup. Comparer mirrors
/// <see cref="MySqlOutboxMigrations"/>.
/// </para>
/// </remarks>
public static class MySqlInboxMigrations
{
    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2AddedColumns = ["ContextKey"];

    /// <summary>
    /// Returns all migrations for the MySQL inbox, ordered by version.
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
                UpScript: MySqlInboxBuilder.GetDDL(table, config.BinaryMessagePayload),
                LogicalColumns: Cumulative(1)),

            new BoxMigration(
                Version: 2,
                Description: "Add ContextKey column",
                UpScript: AddColumn(table, "ContextKey", "VARCHAR(256)"),
                LogicalColumns: Cumulative(2),
                SourceReference: "787c31c52")
        ];
    }

    private static IReadOnlyCollection<string> Cumulative(int upToVersion)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (upToVersion >= 1) { set.UnionWith(s_v1Columns); }
        if (upToVersion >= 2) { set.UnionWith(s_v2AddedColumns); }
        return set;
    }

    /// <summary>
    /// MySQL 5.7+ idempotent ADD COLUMN — runtime <c>information_schema.columns</c> probe drives
    /// a prepared-statement that conditionally emits the ALTER. Runs against
    /// <c>DATABASE()</c> (the connection's bound schema), matching the un-qualified ALTER target.
    /// The added column is <c>NULL</c>-able — required because MySQL ADD COLUMN against a
    /// non-empty table must permit NULL or supply a DEFAULT, and we make no assumption about
    /// emptiness during bootstrap.
    /// </summary>
    private static string AddColumn(string table, string column, string type) =>
        $@"SET @q = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = '{table}' AND column_name = '{column}') = 0,
    'ALTER TABLE `{table}` ADD COLUMN `{column}` {type} NULL',
    'SELECT 1'));
PREPARE stmt FROM @q;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;";
}

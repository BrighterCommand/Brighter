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
using System.Linq;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Defines the migration history for MySQL outbox tables.
/// </summary>
/// <remarks>
/// V1 is the fresh-install baseline whose <c>UpScript</c> is the live
/// <see cref="MySqlOutboxBuilder"/> DDL (per ADR 0057 §3 fresh-install fast path).
/// V2..V7 use the MySQL <c>information_schema.columns</c> + prepared-statement idempotency
/// pattern (ADR 0057 §5) — MySQL 5.7 lacks native <c>ALTER TABLE ADD COLUMN IF NOT EXISTS</c>
/// (it landed in MySQL 8.0.29), so each ALTER is wrapped in a runtime conditional that emits
/// the ALTER only when the column is absent. The <c>table_schema</c> filter uses
/// <see cref="https://dev.mysql.com/doc/refman/8.0/en/information-functions.html#function_database">
/// the runtime <c>DATABASE()</c> function</see> — same DB the runner's connection is bound to,
/// matching the ALTER's implicit schema target.
/// <para>
/// LogicalColumns are stored PascalCase with <see cref="StringComparer.OrdinalIgnoreCase"/> —
/// MySQL identifiers are case-insensitive on lookup. The accumulated columns across V1..V7 plus
/// MySQL housekeeping (<c>Created</c>, <c>CreatedID</c>) equal the live builder's column set,
/// verified by the drift test in <c>tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning</c>.
/// </para>
/// </remarks>
public static class MySqlOutboxMigrations
{
    private static readonly string[] s_v1Columns =
        ["MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"];

    private static readonly string[] s_v2AddedColumns = ["Dispatched"];
    private static readonly string[] s_v3AddedColumns = ["CorrelationId", "ReplyTo", "ContentType"];
    private static readonly string[] s_v4AddedColumns = ["PartitionKey"];

    private static readonly string[] s_v5AddedColumns =
        ["Source", "Type", "DataSchema", "Subject", "TraceParent", "TraceState", "Baggage"];

    private static readonly string[] s_v6AddedColumns = ["WorkflowId", "JobId"];
    private static readonly string[] s_v7AddedColumns = ["DataRef", "SpecVersion"];

    /// <summary>
    /// Returns all migrations for the MySQL outbox, ordered by version.
    /// </summary>
    /// <param name="config">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V7.</returns>
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        var table = config.OutBoxTableName;

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: MySqlOutboxBuilder.GetDDL(table, config.BinaryMessagePayload),
                LogicalColumns: Cumulative(1)),

            new BoxMigration(
                Version: 2,
                Description: "Add Dispatched column",
                UpScript: AddColumns(table, ("Dispatched", "TIMESTAMP(3)")),
                LogicalColumns: Cumulative(2),
                SourceReference: "3c30343fa"),

            new BoxMigration(
                Version: 3,
                Description: "Add CorrelationId, ReplyTo, ContentType columns",
                UpScript: AddColumns(table,
                    ("CorrelationId", "VARCHAR(255)"),
                    ("ReplyTo", "VARCHAR(255)"),
                    ("ContentType", "VARCHAR(128)")),
                LogicalColumns: Cumulative(3),
                SourceReference: "79100f509 / #1401"),

            new BoxMigration(
                Version: 4,
                Description: "Add PartitionKey column",
                UpScript: AddColumns(table, ("PartitionKey", "VARCHAR(128)")),
                LogicalColumns: Cumulative(4),
                SourceReference: "1cdc04b60 / #2560"),

            new BoxMigration(
                Version: 5,
                Description: "Add CloudEvents columns (Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage)",
                UpScript: AddColumns(table,
                    ("Source", "VARCHAR(255)"),
                    ("Type", "VARCHAR(255)"),
                    ("DataSchema", "VARCHAR(255)"),
                    ("Subject", "VARCHAR(255)"),
                    ("TraceParent", "VARCHAR(255)"),
                    ("TraceState", "VARCHAR(255)"),
                    ("Baggage", "TEXT")),
                LogicalColumns: Cumulative(5),
                SourceReference: "b740a68ed / #3633"),

            new BoxMigration(
                Version: 6,
                Description: "Add WorkflowId, JobId columns",
                UpScript: AddColumns(table,
                    ("WorkflowId", "VARCHAR(255)"),
                    ("JobId", "VARCHAR(255)")),
                LogicalColumns: Cumulative(6),
                SourceReference: "0e79332f1 / #3693"),

            new BoxMigration(
                Version: 7,
                Description: "Add DataRef, SpecVersion columns",
                UpScript: AddColumns(table,
                    ("DataRef", "VARCHAR(255)"),
                    ("SpecVersion", "VARCHAR(255)")),
                LogicalColumns: Cumulative(7),
                SourceReference: "d67dac947 / #3790")
        ];
    }

    private static ISet<string> Cumulative(int upToVersion)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (upToVersion >= 1) { set.UnionWith(s_v1Columns); }
        if (upToVersion >= 2) { set.UnionWith(s_v2AddedColumns); }
        if (upToVersion >= 3) { set.UnionWith(s_v3AddedColumns); }
        if (upToVersion >= 4) { set.UnionWith(s_v4AddedColumns); }
        if (upToVersion >= 5) { set.UnionWith(s_v5AddedColumns); }
        if (upToVersion >= 6) { set.UnionWith(s_v6AddedColumns); }
        if (upToVersion >= 7) { set.UnionWith(s_v7AddedColumns); }
        return set;
    }

    private static string AddColumns(string table, params (string Column, string Type)[] columns) =>
        string.Join(Environment.NewLine, columns.Select(c => AddColumn(table, c.Column, c.Type)));

    /// <summary>
    /// MySQL 5.7+ idempotent ADD COLUMN — runtime <c>information_schema.columns</c> probe drives
    /// a prepared-statement that conditionally emits the ALTER. Runs against
    /// <c>DATABASE()</c> (the connection's bound schema), matching the un-qualified ALTER target.
    /// All added columns are <c>NULL</c>-able — required because MySQL ADD COLUMN against a
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

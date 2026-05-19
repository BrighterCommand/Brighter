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
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Defines the migration history for SQLite outbox tables.
/// </summary>
/// <remarks>
/// V1's <c>UpScript</c> is the literal historical baseline DDL — the first SQLite outbox
/// builder shape (commit <c>695522367</c>, March 2019; pre-Dispatched). Spec 0027 R1 split
/// "live builder DDL" away from V1.UpScript: the fresh-install fast path (ADR 0057 §3) now
/// sources its DDL from <see cref="FreshInstallDdl"/>, so V1.UpScript is free to carry the
/// honest historical shape. V2..V7 are plain <c>ALTER TABLE ADD COLUMN</c> statements —
/// SQLite's grammar lacks <c>ALTER TABLE ADD COLUMN IF NOT EXISTS</c>, so the idempotency
/// guard lives in a separate <see cref="IAmABoxMigration.IdempotencyCheckSql"/> field per
/// ADR 0057 §6 (probing <c>pragma_table_info</c>). The <c>SqliteBoxMigrationRunner</c>
/// evaluates this scalar before running <c>UpScript</c> and skips the ALTER (still stamping
/// history) when the column is already present.
/// <para>
/// SQLite outbox V1 is one of three relational outboxes whose first-shipped state matches
/// the "logical pre-V2 baseline" (alongside MSSQL and MySQL). The PostgreSQL outbox is the
/// lone exception — see the born-past-V1 note in <see cref="PostgreSqlOutboxMigrationCatalog"/>.
/// The accumulated <c>LogicalColumns</c> across V1..V7 equals the live builder's column set
/// — verified by the drift test in <c>tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning</c>.
/// </para>
/// </remarks>
public class SqliteOutboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private static readonly string[] s_v1Columns =
        ["MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"];

    // Literal historical SQLite outbox DDL extracted from commit 695522367 (March 2019). The
    // exact pre-Dispatched shape that shipped as "V1" — preserved here so chain replay against
    // a legacy table sees the same starting DDL it always saw. {0} = table name (validated).
    // The table identifier is bracket-quoted so legal-but-reserved SQLite keyword names
    // bootstrap correctly — V2..V7 already bracket-quote the table identifier (and SQLite
    // accepts brackets, backticks, or double-quotes for identifier quoting), so V1 is the
    // only asymmetric step. Per PR #4039 reviewer item F2-1.
    private const string V1HistoricalDdl =
        """
        CREATE TABLE [{0}] (
            [MessageId] uniqueidentifier NOT NULL
          , [Topic] nvarchar(255) NULL
          , [MessageType] nvarchar(32) NULL
          , [Timestamp] datetime NULL
          , [HeaderBag] ntext NULL
          , [Body] ntext NULL
          , CONSTRAINT [PK_MessageId] PRIMARY KEY ([MessageId])
        );
        """;

    private static readonly string[] s_v2AddedColumns = ["Dispatched"];
    private static readonly string[] s_v3AddedColumns = ["CorrelationId", "ReplyTo", "ContentType"];
    private static readonly string[] s_v4AddedColumns = ["PartitionKey"];

    private static readonly string[] s_v5AddedColumns =
        ["Source", "Type", "DataSchema", "Subject", "TraceParent", "TraceState", "Baggage"];

    private static readonly string[] s_v6AddedColumns = ["WorkflowId", "JobId"];
    private static readonly string[] s_v7AddedColumns = ["DataRef", "SpecVersion"];

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.OutBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.OutBoxTableName));
        return SqliteOutboxBuilder.GetDDL(configuration.OutBoxTableName, configuration.BinaryMessagePayload);
    }

    /// <summary>
    /// Returns all migrations for the SQLite outbox, ordered by version.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V7.</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var table = configuration.OutBoxTableName;

        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.OutBoxTableName));

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: string.Format(V1HistoricalDdl, table),
                LogicalColumns: Cumulative(1)),

            new BoxMigration(
                Version: 2,
                Description: "Add Dispatched column",
                UpScript: AddColumns(table, "Dispatched"),
                LogicalColumns: Cumulative(2),
                SourceReference: "3c30343fa",
                IdempotencyCheckSql: ColumnExistsCheck(table, "Dispatched")),

            new BoxMigration(
                Version: 3,
                Description: "Add CorrelationId, ReplyTo, ContentType columns",
                UpScript: AddColumns(table, "CorrelationId", "ReplyTo", "ContentType"),
                LogicalColumns: Cumulative(3),
                SourceReference: "79100f509 / #1401",
                IdempotencyCheckSql: ColumnExistsCheck(table, "CorrelationId")),

            new BoxMigration(
                Version: 4,
                Description: "Add PartitionKey column",
                UpScript: AddColumns(table, "PartitionKey"),
                LogicalColumns: Cumulative(4),
                SourceReference: "1cdc04b60 / #2560",
                IdempotencyCheckSql: ColumnExistsCheck(table, "PartitionKey")),

            new BoxMigration(
                Version: 5,
                Description: "Add CloudEvents columns (Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage)",
                UpScript: AddColumns(table,
                    "Source", "Type", "DataSchema", "Subject",
                    "TraceParent", "TraceState", "Baggage"),
                LogicalColumns: Cumulative(5),
                SourceReference: "b740a68ed / #3633",
                IdempotencyCheckSql: ColumnExistsCheck(table, "Source")),

            new BoxMigration(
                Version: 6,
                Description: "Add WorkflowId, JobId columns",
                UpScript: AddColumns(table, "WorkflowId", "JobId"),
                LogicalColumns: Cumulative(6),
                SourceReference: "0e79332f1 / #3693",
                IdempotencyCheckSql: ColumnExistsCheck(table, "WorkflowId")),

            new BoxMigration(
                Version: 7,
                Description: "Add DataRef, SpecVersion columns",
                UpScript: AddColumns(table, "DataRef", "SpecVersion"),
                LogicalColumns: Cumulative(7),
                SourceReference: "d67dac947 / #3790",
                IdempotencyCheckSql: ColumnExistsCheck(table, "DataRef"))
        ];
    }

    private static IReadOnlyCollection<string> Cumulative(int upToVersion)
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

    private static string AddColumns(string table, params string[] columns) =>
        string.Join(Environment.NewLine, columns.Select(c => AddColumn(table, c)));

    private static string AddColumn(string table, string column) =>
        $"ALTER TABLE [{table}] ADD COLUMN [{column}] TEXT NULL;";

    private static string ColumnExistsCheck(string table, string column) =>
        $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}';";
}

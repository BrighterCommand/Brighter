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
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Defines the migration history for MSSQL outbox tables.
/// </summary>
/// <remarks>
/// V1's <c>UpScript</c> is the literal historical baseline DDL — the first MSSQL outbox
/// builder shape (commit <c>695522367</c>, March 2019; pre-Dispatched, pre-CloudEvents).
/// Spec 0027 R1 split "live builder DDL" away from V1.UpScript: the fresh-install fast path
/// (ADR 0057 §3) now sources its DDL from <see cref="FreshInstallDdl"/>, so V1.UpScript is
/// free to carry the honest historical shape it always represented. V2..V7 are conditional
/// <c>ALTER TABLE ADD</c> statements guarded by <c>IF COL_LENGTH(...) IS NULL</c> per ADR
/// 0057 §5, so each one is idempotent and safe to re-execute on chain replay.
/// <para>
/// The MSSQL outbox is the only one of the four relational outboxes whose first-shipped
/// state matches the "logical pre-V2 baseline" — see the asymmetry note in the
/// PostgreSQL/MySQL/SQLite outbox catalogs. The accumulated <c>LogicalColumns</c> across
/// V1..V7 plus the MSSQL housekeeping <c>Id</c> column equals the live builder's column set
/// — verified by the drift test in <c>tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning</c>.
/// </para>
/// </remarks>
public class MsSqlOutboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private const string DefaultSchema = "dbo";

    private static readonly string[] s_v1Columns =
        ["MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"];

    // Literal historical MSSQL outbox DDL extracted from commit 695522367 (March 2019). The
    // exact pre-Dispatched shape that shipped as "V1" — preserved here so chain replay against
    // a legacy table sees the same starting DDL it always saw. {0} = table name (validated).
    // The table identifier is bracket-quoted so legal-but-reserved T-SQL keyword names
    // (User, Order, Group, …) bootstrap correctly — V2..V7 already bracket-quote, so V1
    // is the only asymmetric step. Per PR #4039 reviewer item F2-1.
    private const string V1HistoricalDdl =
        """
        CREATE TABLE [{0}]
            (
              [Id] [BIGINT] NOT NULL IDENTITY ,
              [MessageId] UNIQUEIDENTIFIER NOT NULL ,
              [Topic] NVARCHAR(255) NULL ,
              [MessageType] NVARCHAR(32) NULL ,
              [Timestamp] DATETIME NULL ,
              [HeaderBag] NTEXT NULL ,
              [Body] NTEXT NULL ,
              PRIMARY KEY ( [Id] )
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
        // Pass SchemaName so the builder schema-qualifies the CREATE TABLE — otherwise the
        // table lands in the connection's default schema (typically [dbo]) regardless of
        // SchemaName, and the V2..V7 chain ALTERs (which use [schema].[table]) cannot find
        // it on a subsequent run. Per PR #4039 reviewer item M4-1 (F1a). Schema identifier
        // safety is enforced at the All(...) catalog entry below; FreshInstallDdl's contract
        // is "pass through what the configuration provides", so the identifier check is
        // re-applied here for defence in depth.
        if (configuration.SchemaName is not null)
        {
            Identifiers.AssertSafe(
                configuration.SchemaName,
                nameof(IAmARelationalDatabaseConfiguration.SchemaName));
        }
        return SqlOutboxBuilder.GetDDL(
            configuration.OutBoxTableName,
            configuration.BinaryMessagePayload,
            configuration.SchemaName);
    }

    /// <summary>
    /// Returns all migrations for the MSSQL outbox, ordered by version.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V7.</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var schema = configuration.SchemaName ?? DefaultSchema;
        var table = configuration.OutBoxTableName;

        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.OutBoxTableName));
        Identifiers.AssertSafe(schema, nameof(IAmARelationalDatabaseConfiguration.SchemaName));

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
                UpScript: AddColumns(schema, table, ("Dispatched", "DATETIME")),
                LogicalColumns: Cumulative(2),
                SourceReference: "3c30343fa"),

            new BoxMigration(
                Version: 3,
                Description: "Add CorrelationId, ReplyTo, ContentType columns",
                UpScript: AddColumns(schema, table,
                    ("CorrelationId", "NVARCHAR(255)"),
                    ("ReplyTo", "NVARCHAR(255)"),
                    ("ContentType", "NVARCHAR(128)")),
                LogicalColumns: Cumulative(3),
                SourceReference: "79100f509 / #1401"),

            new BoxMigration(
                Version: 4,
                Description: "Add PartitionKey column",
                UpScript: AddColumns(schema, table, ("PartitionKey", "NVARCHAR(255)")),
                LogicalColumns: Cumulative(4),
                SourceReference: "1cdc04b60 / #2560"),

            new BoxMigration(
                Version: 5,
                Description: "Add CloudEvents columns (Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage)",
                UpScript: AddColumns(schema, table,
                    ("Source", "NVARCHAR(255)"),
                    ("Type", "NVARCHAR(255)"),
                    ("DataSchema", "NVARCHAR(255)"),
                    ("Subject", "NVARCHAR(255)"),
                    ("TraceParent", "NVARCHAR(255)"),
                    ("TraceState", "NVARCHAR(255)"),
                    ("Baggage", "NVARCHAR(MAX)")),
                LogicalColumns: Cumulative(5),
                SourceReference: "b740a68ed / #3633"),

            new BoxMigration(
                Version: 6,
                Description: "Add WorkflowId, JobId columns",
                UpScript: AddColumns(schema, table,
                    ("WorkflowId", "NVARCHAR(255)"),
                    ("JobId", "NVARCHAR(255)")),
                LogicalColumns: Cumulative(6),
                SourceReference: "0e79332f1 / #3693"),

            new BoxMigration(
                Version: 7,
                Description: "Add DataRef, SpecVersion columns",
                UpScript: AddColumns(schema, table,
                    ("DataRef", "NVARCHAR(255)"),
                    ("SpecVersion", "NVARCHAR(255)")),
                LogicalColumns: Cumulative(7),
                SourceReference: "d67dac947 / #3790")
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

    private static string AddColumns(string schema, string table, params (string Column, string Type)[] columns) =>
        string.Join(Environment.NewLine, columns.Select(c => AddColumn(schema, table, c.Column, c.Type)));

    private static string AddColumn(string schema, string table, string column, string type) =>
        $"IF COL_LENGTH(N'[{schema}].[{table}]', N'{column}') IS NULL{Environment.NewLine}" +
        $"    ALTER TABLE [{schema}].[{table}] ADD [{column}] {type} NULL;";
}

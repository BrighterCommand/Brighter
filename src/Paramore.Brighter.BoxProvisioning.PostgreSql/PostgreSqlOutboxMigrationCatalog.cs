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
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Defines the migration history for PostgreSQL outbox tables.
/// </summary>
/// <remarks>
/// V1's <c>UpScript</c> is the literal historical baseline DDL — the PostgreSQL outbox
/// builder shape before <c>Dispatched</c> (first shipped in commit <c>b1f880367</c>, August 2017). The fresh-install fast path
/// (ADR 0057 §3) takes its DDL from <see cref="FreshInstallDdl"/> instead, so V1.UpScript
/// carries the historical shape. V2..V7 are conditional <c>ALTER TABLE ... ADD COLUMN IF NOT
/// EXISTS</c> statements per ADR 0057 §5 — Postgres's native idempotency clause keeps each
/// migration safe to re-execute on chain replay.
/// <para>
/// The PostgreSQL outbox shipped without <c>Dispatched</c> until commit <c>3c30343fa</c> added
/// it, so a legacy table may be at V1, and V2 adds the column to it.
/// </para>
/// <para>
/// <see cref="IAmABoxMigration.LogicalColumns"/> are lowercase per ADR 0057 §1 so that
/// detection-by-<c>information_schema.columns</c> (which returns Postgres's folded lowercase
/// column names) matches without case mismatch. V8 adds <c>causationid</c>
/// plus a replay index on it. The accumulated columns across V1..V8 plus
/// the Postgres housekeeping (<c>id</c>, <c>messageid</c>) equal the live builder's column
/// set — verified by the drift test in <c>tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning</c>.
/// </para>
/// </remarks>
public class PostgreSqlOutboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private const string DefaultSchema = "public";

    private static readonly string[] s_v1Columns =
        ["messageid", "topic", "messagetype", "timestamp", "headerbag", "body"];

    private static readonly string[] s_v2AddedColumns = ["dispatched"];
    private static readonly string[] s_v3AddedColumns = ["correlationid", "replyto", "contenttype"];
    private static readonly string[] s_v4AddedColumns = ["partitionkey"];

    private static readonly string[] s_v5AddedColumns =
        ["source", "type", "dataschema", "subject", "traceparent", "tracestate", "baggage"];

    private static readonly string[] s_v6AddedColumns = ["workflowid", "jobid"];
    private static readonly string[] s_v7AddedColumns = ["dataref", "specversion"];
    private static readonly string[] s_v8AddedColumns = ["causationid"];

    // The outbox DDL before Dispatched (as at 3c30343fa^).
    // {0} is the table name folded to lowercase then quoted: that names the same table the
    // original unquoted DDL created, and lets reserved words (Order, User) parse.
    private const string V1HistoricalDdl =
        """
        CREATE TABLE {0}
            (
                Id BIGSERIAL PRIMARY KEY,
                MessageId UUID UNIQUE NOT NULL,
                Topic VARCHAR(255) NULL,
                MessageType VARCHAR(32) NULL,
                Timestamp timestamptz NULL,
                HeaderBag TEXT NULL,
                Body TEXT NULL
            );
        """;

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.OutBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.OutBoxTableName));
        if (configuration.SchemaName is not null)
        {
            Identifiers.AssertSafe(
                configuration.SchemaName,
                nameof(IAmARelationalDatabaseConfiguration.SchemaName));
        }
        return PostgreSqlOutboxBuilder.GetDDL(
            configuration.OutBoxTableName,
            configuration.BinaryMessagePayload,
            configuration.SchemaName);
    }

    /// <summary>
    /// Returns all migrations for the PostgreSQL outbox, ordered by version.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V8.</returns>
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
                UpScript: string.Format(V1HistoricalDdl, PgIdentifier.Quote(table)),
                LogicalColumns: Cumulative(1)),

            new BoxMigration(
                Version: 2,
                Description: "Add dispatched column",
                UpScript: AddColumns(schema, table, ("dispatched", "timestamptz")),
                LogicalColumns: Cumulative(2),
                SourceReference: "3c30343fa"),

            new BoxMigration(
                Version: 3,
                Description: "Add correlationid, replyto, contenttype columns",
                UpScript: AddColumns(schema, table,
                    ("correlationid", "character varying(255)"),
                    ("replyto", "character varying(255)"),
                    ("contenttype", "character varying(128)")),
                LogicalColumns: Cumulative(3),
                SourceReference: "79100f509 / #1401"),

            new BoxMigration(
                Version: 4,
                Description: "Add partitionkey column",
                UpScript: AddColumns(schema, table, ("partitionkey", "character varying(128)")),
                LogicalColumns: Cumulative(4),
                SourceReference: "cff67fd5e / #3464"),

            new BoxMigration(
                Version: 5,
                Description: "Add CloudEvents columns (source, type, dataschema, subject, traceparent, tracestate, baggage)",
                UpScript: AddColumns(schema, table,
                    ("source", "character varying(255)"),
                    ("type", "character varying(255)"),
                    ("dataschema", "character varying(255)"),
                    ("subject", "character varying(255)"),
                    ("traceparent", "character varying(255)"),
                    ("tracestate", "character varying(255)"),
                    ("baggage", "text")),
                LogicalColumns: Cumulative(5),
                SourceReference: "b740a68ed / #3633"),

            new BoxMigration(
                Version: 6,
                Description: "Add workflowid, jobid columns",
                UpScript: AddColumns(schema, table,
                    ("workflowid", "character varying(255)"),
                    ("jobid", "character varying(255)")),
                LogicalColumns: Cumulative(6),
                SourceReference: "0e79332f1 / #3693"),

            new BoxMigration(
                Version: 7,
                Description: "Add dataref, specversion columns",
                UpScript: AddColumns(schema, table,
                    ("dataref", "character varying(255)"),
                    ("specversion", "character varying(255)")),
                LogicalColumns: Cumulative(7),
                SourceReference: "d67dac947 / #3790"),

            new BoxMigration(
                Version: 8,
                Description: "Add causationid column and replay index",
                UpScript: AddColumns(schema, table, ("causationid", "character varying(255)"))
                    + Environment.NewLine
                    + AddCausationIndex(schema, table),
                LogicalColumns: Cumulative(8),
                SourceReference: "#2541")
        ];
    }

    private static IReadOnlyCollection<string> Cumulative(int upToVersion)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (upToVersion >= 1) { set.UnionWith(s_v1Columns); }
        if (upToVersion >= 2) { set.UnionWith(s_v2AddedColumns); }
        if (upToVersion >= 3) { set.UnionWith(s_v3AddedColumns); }
        if (upToVersion >= 4) { set.UnionWith(s_v4AddedColumns); }
        if (upToVersion >= 5) { set.UnionWith(s_v5AddedColumns); }
        if (upToVersion >= 6) { set.UnionWith(s_v6AddedColumns); }
        if (upToVersion >= 7) { set.UnionWith(s_v7AddedColumns); }
        if (upToVersion >= 8) { set.UnionWith(s_v8AddedColumns); }
        return set;
    }

    private static string AddColumns(string schema, string table, params (string Column, string Type)[] columns) =>
        string.Join(Environment.NewLine, columns.Select(c => AddColumn(schema, table, c.Column, c.Type)));

    // Column names are emitted bare: the catalog's are already lowercase (ADR 0057 §1).
    private static string AddColumn(string schema, string table, string column, string type) =>
        $"ALTER TABLE {PgIdentifier.QuoteQualified(schema, table)} ADD COLUMN IF NOT EXISTS {column} {type} NULL;";

    // The index name is the folded table name, emitted bare to match Postgres's own case-folding.
    private static string AddCausationIndex(string schema, string table) =>
        $"CREATE INDEX IF NOT EXISTS idx_{PgIdentifier.Normalize(table)}_causationid " +
        $"ON {PgIdentifier.QuoteQualified(schema, table)} (causationid);";
}

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
/// V1's <c>UpScript</c> is the literal historical baseline DDL — the first PostgreSQL
/// outbox builder shape (commit <c>3c30343fa</c>, July 2019). Spec 0027 R1 split "live
/// builder DDL" away from V1.UpScript: the fresh-install fast path (ADR 0057 §3) now
/// sources its DDL from <see cref="FreshInstallDdl"/>, so V1.UpScript is free to carry the
/// honest historical shape. V2..V7 are conditional <c>ALTER TABLE ... ADD COLUMN IF NOT
/// EXISTS</c> statements per ADR 0057 §5 — Postgres's native idempotency clause keeps each
/// migration safe to re-execute on chain replay.
/// <para>
/// Born-past-V1 asymmetry: the PostgreSQL outbox first shipped <em>with</em>
/// <c>Dispatched</c> — no pre-Dispatched PostgreSQL outbox ever existed in the wild.
/// V1.UpScript therefore creates <c>dispatched</c> alongside the rest; V2's
/// <c>ADD COLUMN IF NOT EXISTS dispatched</c> is a no-op on chain replay. V1.LogicalColumns
/// remains the "logical pre-V2" set (no <c>dispatched</c>) so the bootstrap-detection
/// contract (ADR 0057 §4) preserves the cross-backend logical V1 shape.
/// </para>
/// <para>
/// <see cref="IAmABoxMigration.LogicalColumns"/> are lowercase per ADR 0057 §1 so that
/// detection-by-<c>information_schema.columns</c> (which returns Postgres's folded lowercase
/// column names) matches without case mismatch. V8 adds <c>causationid</c> (Spec 0027, #2541)
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

    // Literal historical PostgreSQL outbox DDL extracted from commit 3c30343fa (July 2019).
    // First-shipped state already carried Dispatched — see the born-past-V1 note in the
    // class remarks. {0} = quoted, lowercased table identifier (via PgIdentifier).
    // Lowercase-then-quote: PG case-folds unquoted identifiers to lowercase at parse time,
    // so historical tables created via the unquoted builder DDL live as e.g. `outbox` in
    // pg_class regardless of the configured `OutBoxTableName = "Outbox"`. Quoting the
    // already-lowercased form ("outbox") resolves to the same physical table as the legacy
    // unquoted form would have, AND unlocks reserved-keyword names (Order, User, Group, …)
    // that the unquoted V2..V7 ALTERs would otherwise reject as syntax errors. All catalogs
    // and builders in this chain apply the same fold-then-quote rule via PgIdentifier.
    private const string V1HistoricalDdl =
        """
        CREATE TABLE {0}
            (
                Id BIGSERIAL PRIMARY KEY,
                MessageId UUID UNIQUE NOT NULL,
                Topic VARCHAR(255) NULL,
                MessageType VARCHAR(32) NULL,
                Timestamp timestamptz NULL,
                Dispatched timestamptz NULL,
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
        // Pass SchemaName so the builder schema-qualifies the CREATE TABLE IF NOT EXISTS —
        // otherwise the table lands in the connection's search_path default (typically
        // `public`) regardless of the configured SchemaName, leaving V2..V7 ALTERs (which
        // use `{schema}.{table}`) targeting a table that does not exist on subsequent runs.
        // Per PR #4039 reviewer item M4-1 (F1b).
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

    private static string AddColumn(string schema, string table, string column, string type) =>
        // schema/table are lowercase-then-quoted via PgIdentifier so the ALTER targets the
        // same physical table that V1's CREATE produced — and so reserved-keyword names
        // (Order, User, Group, …) parse cleanly. Column names are sourced from the catalog
        // and are already ADR 0057 §1 lowercase, so they are emitted bare.
        $"ALTER TABLE {PgIdentifier.QuoteQualified(schema, table)} ADD COLUMN IF NOT EXISTS {column} {type} NULL;";

    // Idempotent replay index (Spec 0027, #2541) on causationid via Postgres's native
    // CREATE INDEX IF NOT EXISTS. The index name folds the table identifier to lowercase
    // (PgIdentifier.Normalize) and is emitted bare so it matches Postgres's natural case-fold;
    // the indexed table is lowercase-then-quoted via PgIdentifier like the ALTER targets above.
    private static string AddCausationIndex(string schema, string table) =>
        $"CREATE INDEX IF NOT EXISTS idx_{PgIdentifier.Normalize(table)}_causationid " +
        $"ON {PgIdentifier.QuoteQualified(schema, table)} (causationid);";
}

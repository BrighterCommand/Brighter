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
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Defines the migration history for PostgreSQL inbox tables.
/// </summary>
/// <remarks>
/// PostgreSQL inbox skips the <c>ContextKey</c> catch-up the other backends carry. Unlike
/// MSSQL/MySQL/SQLite — which have a V1 baseline followed by a V2 that adds <c>ContextKey</c>
/// (commit <c>787c31c52</c>, Oct 2018) — the PostgreSQL inbox was born with <c>ContextKey</c> +
/// composite primary key <c>(CommandId, ContextKey)</c> (Feb 2021, commit <c>79100f509</c>). No pre-ContextKey PostgreSQL inbox ever shipped (ADR 0057 "Alternatives →
/// E"), so V1's <see cref="IAmABoxMigration.LogicalColumns"/> already includes <c>contextkey</c>.
/// A V2 then introduces <c>causationid</c> via an idempotent
/// <c>ALTER TABLE ... ADD COLUMN IF NOT EXISTS</c>, so the chain is V1→V2 — exactly one version
/// behind the other three (which run V1→V2→V3).
/// <para>
/// V1's <c>UpScript</c> is the literal historical baseline DDL extracted from commit
/// <c>79100f509</c> — the first PostgreSQL inbox builder shape. The fresh-install fast path
/// (ADR 0057 §3) takes its DDL from <see cref="FreshInstallDdl"/> instead. V1.UpScript is the historical
/// reference for legacy bootstrap; V2's <c>ADD COLUMN IF NOT EXISTS causationid</c> is safe to
/// re-execute on chain replay via Postgres's native idempotency clause.
/// </para>
/// <para>
/// LogicalColumns are lowercase per ADR 0057 §1 to match PostgreSQL's <c>information_schema.columns</c>
/// folding at runtime; comparer is <see cref="StringComparer.Ordinal"/> to enforce that contract.
/// </para>
/// </remarks>
public class PostgreSqlInboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private const string DefaultSchema = "public";

    // From commit 79100f509 (Feb 2021); born with ContextKey, see the class remarks.
    // {0} is the table name folded to lowercase then quoted: that names the same table the
    // original unquoted DDL created, and lets reserved words (Order, User) parse.
    private const string V1HistoricalDdl =
        """
        CREATE TABLE {0}
            (
                CommandId uuid NOT NULL ,
                CommandType VARCHAR(256) NULL ,
                CommandBody TEXT NULL ,
                Timestamp timestamptz  NULL ,
                ContextKey VARCHAR(256) NULL,
                PRIMARY KEY (CommandId, ContextKey)
            );
        """;

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.InBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        if (configuration.SchemaName is not null)
        {
            Identifiers.AssertSafe(
                configuration.SchemaName,
                nameof(IAmARelationalDatabaseConfiguration.SchemaName));
        }
        return PostgreSqlInboxBuilder.GetDDL(
            configuration.InBoxTableName,
            configuration.BinaryMessagePayload,
            jsonMessagePayload: false,
            schemaName: configuration.SchemaName);
    }

    /// <summary>
    /// Returns the migration list for the PostgreSQL inbox: V1 (the literal historical
    /// PostgreSQL inbox DDL, composite primary key on <c>(CommandId, ContextKey)</c>) followed
    /// by V2 (idempotent <c>ADD COLUMN IF NOT EXISTS causationid</c>).
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V2.</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var schema = configuration.SchemaName ?? DefaultSchema;
        var table = configuration.InBoxTableName;

        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        Identifiers.AssertSafe(schema, nameof(IAmARelationalDatabaseConfiguration.SchemaName));

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: string.Format(V1HistoricalDdl, PgIdentifier.Quote(table)),
                LogicalColumns: new HashSet<string>(StringComparer.Ordinal)
                {
                    "commandid", "commandtype", "commandbody", "timestamp", "contextkey"
                }),

            new BoxMigration(
                Version: 2,
                Description: "Add causationid column",
                UpScript: AddColumn(schema, table, "causationid", "character varying(256)"),
                LogicalColumns: new HashSet<string>(StringComparer.Ordinal)
                {
                    "commandid", "commandtype", "commandbody", "timestamp", "contextkey", "causationid"
                },
                SourceReference: "#2541")
        ];
    }

    // Column names are emitted bare: the catalog's are already lowercase (ADR 0057 §1).
    private static string AddColumn(string schema, string table, string column, string type) =>
        $"ALTER TABLE {PgIdentifier.QuoteQualified(schema, table)} ADD COLUMN IF NOT EXISTS {column} {type} NULL;";
}

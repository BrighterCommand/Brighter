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
/// composite primary key <c>(CommandId, ContextKey)</c> in PR #1401 (Nov 2023, commit
/// <c>1cdc04b60</c>). No pre-ContextKey PostgreSQL inbox ever shipped (ADR 0057 "Alternatives →
/// E"), so V1's <see cref="IAmABoxMigration.LogicalColumns"/> already includes <c>contextkey</c>.
/// Spec 0027 (#2541) then adds a V2 that introduces <c>causationid</c> via an idempotent
/// <c>ALTER TABLE ... ADD COLUMN IF NOT EXISTS</c>, so the chain is V1→V2 — exactly one version
/// behind the other three (which run V1→V2→V3).
/// <para>
/// V1's <c>UpScript</c> is the literal historical baseline DDL extracted from commit
/// <c>1cdc04b60</c> — the first PostgreSQL inbox builder shape. Spec 0027 R1 split "live
/// builder DDL" away from V1.UpScript: the fresh-install fast path (ADR 0057 §3) now
/// sources its DDL from <see cref="FreshInstallDdl"/>. V1.UpScript is the historical
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

    // Literal historical PostgreSQL inbox DDL extracted from commit 1cdc04b60 (Nov 2023, PR
    // #2560). First-shipped state already carried ContextKey with a composite PRIMARY KEY on
    // (CommandId, ContextKey) — see the V1-only/born-past-V1 note in the class remarks.
    // {0} = quoted, lowercased table identifier (via PgIdentifier).
    // Lowercase-then-quote: PG case-folds unquoted identifiers to lowercase at parse time, so
    // the legacy unquoted form created e.g. `inbox` from a configured `"Inbox"`. Quoting the
    // already-lowercased form ("inbox") resolves to the same physical table as the legacy
    // unquoted form, AND lets reserved-keyword names (User, Order, …) parse cleanly. All
    // PG catalogs and builders share this fold-then-quote convention via PgIdentifier.
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
        // Pass SchemaName so the builder schema-qualifies the CREATE TABLE IF NOT EXISTS.
        // Per PR #4039 reviewer item M4-1 (F1b). See the outbox catalog for the full
        // rationale — the same fix applies symmetrically to the inbox.
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
    /// by V2 (idempotent <c>ADD COLUMN IF NOT EXISTS causationid</c>, Spec 0027 #2541).
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

    private static string AddColumn(string schema, string table, string column, string type) =>
        // schema/table are lowercase-then-quoted via PgIdentifier so the ALTER targets the same
        // physical table that V1's CREATE produced — and so reserved-keyword names parse cleanly.
        // Column names are sourced from the catalog and are already ADR 0057 §1 lowercase, so they
        // are emitted bare. Postgres's native ADD COLUMN IF NOT EXISTS keeps the migration safe to
        // re-execute on chain replay.
        $"ALTER TABLE {PgIdentifier.QuoteQualified(schema, table)} ADD COLUMN IF NOT EXISTS {column} {type} NULL;";
}

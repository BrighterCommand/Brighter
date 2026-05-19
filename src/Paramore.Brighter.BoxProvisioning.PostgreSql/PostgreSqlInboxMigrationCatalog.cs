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

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Defines the migration history for PostgreSQL inbox tables.
/// </summary>
/// <remarks>
/// PostgreSQL inbox is V1-only by design. Unlike MSSQL/MySQL/SQLite — which have a V1 baseline
/// followed by a V2 that adds <c>ContextKey</c> (commit <c>787c31c52</c>, Oct 2018) — the
/// PostgreSQL inbox was born with <c>ContextKey</c> + composite primary key
/// <c>(CommandId, ContextKey)</c> in PR #1401 (Nov 2023, commit <c>1cdc04b60</c>). No
/// pre-ContextKey PostgreSQL inbox ever shipped (ADR 0057 "Alternatives → E"), so V1's
/// <see cref="IAmABoxMigration.LogicalColumns"/> already includes <c>contextkey</c>.
/// <para>
/// V1's <c>UpScript</c> is the literal historical baseline DDL extracted from commit
/// <c>1cdc04b60</c> — the first PostgreSQL inbox builder shape. Spec 0027 R1 split "live
/// builder DDL" away from V1.UpScript: the fresh-install fast path (ADR 0057 §3) now
/// sources its DDL from <see cref="FreshInstallDdl"/>. Because the PostgreSQL inbox chain
/// is V1-only there is no chain replay to worry about; V1.UpScript is purely a historical
/// reference for legacy bootstrap.
/// </para>
/// <para>
/// LogicalColumns are lowercase per ADR 0057 §1 to match PostgreSQL's <c>information_schema.columns</c>
/// folding at runtime; comparer is <see cref="StringComparer.Ordinal"/> to enforce that contract.
/// </para>
/// </remarks>
public class PostgreSqlInboxMigrationCatalog : IAmABoxMigrationCatalog
{
    // Literal historical PostgreSQL inbox DDL extracted from commit 1cdc04b60 (Nov 2023, PR
    // #2560). First-shipped state already carried ContextKey with a composite PRIMARY KEY on
    // (CommandId, ContextKey) — see the V1-only/born-past-V1 note in the class remarks.
    // {0} = table name (validated).
    // Intentionally NOT double-quoted — PG identifiers are case-folded when unquoted and the
    // PG inbox is V1-only (no V2+ chain). Quoting V1 here would change runtime semantics for
    // existing deployments (case-folded `outbox`-style lookups would miss a now case-sensitive
    // table). Reserved-keyword identifiers (e.g. "Order") are a chain-wide PG limitation, not
    // a V1-specific gap. Per PR #4039 reviewer item F2-1.
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
    /// Returns the migration list for the PostgreSQL inbox: a single V1 entry whose
    /// <c>UpScript</c> is the literal historical PostgreSQL inbox DDL (composite primary
    /// key on <c>(CommandId, ContextKey)</c>).
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list with a single V1 migration.</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.InBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: string.Format(V1HistoricalDdl, configuration.InBoxTableName),
                LogicalColumns: new HashSet<string>(StringComparer.Ordinal)
                {
                    "commandid", "commandtype", "commandbody", "timestamp", "contextkey"
                })
        ];
    }
}

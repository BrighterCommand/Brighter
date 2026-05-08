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
/// <c>(CommandId, ContextKey)</c> in PR #1401 (Feb 2021). No pre-ContextKey PostgreSQL inbox
/// ever shipped (ADR 0057 "Alternatives → E"), so V1's <see cref="IAmABoxMigration.LogicalColumns"/>
/// already includes <c>contextkey</c>.
/// <para>
/// LogicalColumns are lowercase per ADR 0057 §1 to match PostgreSQL's <c>information_schema.columns</c>
/// folding at runtime; comparer is <see cref="StringComparer.Ordinal"/> to enforce that contract.
/// </para>
/// </remarks>
public class PostgreSqlInboxMigrationCatalog : IAmABoxMigrationCatalog
{
    /// <summary>
    /// Returns the migration list for the PostgreSQL inbox: a single V1 entry whose
    /// <c>UpScript</c> is the live <see cref="PostgreSqlInboxBuilder"/> DDL.
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
                UpScript: PostgreSqlInboxBuilder.GetDDL(
                    configuration.InBoxTableName,
                    configuration.BinaryMessagePayload),
                LogicalColumns: new HashSet<string>(StringComparer.Ordinal)
                {
                    "commandid", "commandtype", "commandbody", "timestamp", "contextkey"
                })
        ];
    }
}

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

using Npgsql;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.Legacy;

/// <summary>
/// Seeds a PostgreSQL inbox table at the V1 logical version using hand-rolled DDL per
/// ADR 0057 §1 archaeology. Used by concurrent-bootstrap and AC-6 tests so the runner
/// exercises a real legacy schema rather than a migration UpScript round-trip.
/// </summary>
/// <remarks>
/// Postgres inbox is V1-only by design — the table was born with <c>ContextKey</c> and a
/// composite primary key in PR #1401 (Feb 2021), and no pre-ContextKey shape ever shipped
/// (ADR 0057 "Alternatives → E"). The hand-rolled V1 shape therefore matches the live
/// <c>PostgreSqlInboxBuilder</c> DDL (text payload variant) — which means the V1 seeded
/// shape and the live builder shape are byte-equivalent. Column names use lowercase per
/// ADR §1 to align with Postgres unquoted-identifier folding and with
/// <c>information_schema.columns</c>.
/// </remarks>
internal static class PostgreSqlInboxLegacySeeder
{
    private const string V1Ddl = """
        CREATE TABLE "{0}"
        (
            commandid character varying(256) NOT NULL,
            commandtype character varying(256) NULL,
            commandbody text NULL,
            timestamp timestamptz NULL,
            contextkey character varying(256) NOT NULL,
            PRIMARY KEY (commandid, contextkey)
        );
        """;

    /// <summary>
    /// Creates an inbox table with the V1 column set (matching the live builder shape).
    /// The history table is NOT seeded — the test under verification expects the runner
    /// to stamp it.
    /// </summary>
    public static void SeedAtV1(string connectionString, string tableName)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = string.Format(V1Ddl, tableName);
        command.ExecuteNonQuery();
    }
}

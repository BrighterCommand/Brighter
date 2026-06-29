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

using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning.Legacy;

/// <summary>
/// Seeds an MSSQL inbox table at the V1 logical version (pre-October 2018, before
/// <c>ContextKey</c> was added in commit <c>787c31c52</c>) using hand-rolled DDL per ADR 0057
/// §1 archaeology. Used by bootstrap-at-V1 tests so the runner exercises the real legacy
/// schema rather than a migration UpScript round-trip.
/// </summary>
/// <remarks>
/// Inbox archaeology is short — V1 (baseline) and V2 (added <c>ContextKey</c>) — so the two
/// entry points <see cref="SeedAtV1"/> / <see cref="SeedAtV2"/> are sufficient for MSSQL.
/// Column types match the live <c>SqlInboxBuilder</c> DDL minus the <c>CausationId</c> column.
/// </remarks>
internal static class MsSqlInboxLegacySeeder
{
    private const string V1Ddl = """
        CREATE TABLE [{0}]
        (
            [Id] [BIGINT] IDENTITY(1, 1) NOT NULL,
            [CommandId] [NVARCHAR](256) NOT NULL,
            [CommandType] [NVARCHAR](256) NULL,
            [CommandBody] [NVARCHAR](MAX) NULL,
            [Timestamp] [DATETIME] NULL,
            PRIMARY KEY ([Id])
        );
        """;

    // V2 adds ContextKey (787c31c52) but is still pre-CausationId (V3). This is the schema a
    // user has after upgrading Brighter past the ContextKey era but before the Replay feature
    // migration — the realistic "no CausationId column" inbox the backward-compat fix must tolerate.
    private const string V2Ddl = """
        CREATE TABLE [{0}]
        (
            [Id] [BIGINT] IDENTITY(1, 1) NOT NULL,
            [CommandId] [NVARCHAR](256) NOT NULL,
            [CommandType] [NVARCHAR](256) NULL,
            [CommandBody] [NVARCHAR](MAX) NULL,
            [Timestamp] [DATETIME] NULL,
            [ContextKey] [NVARCHAR](256) NULL,
            PRIMARY KEY ([Id])
        );
        """;

    /// <summary>
    /// Creates an inbox table with the V1 column set (no <c>ContextKey</c>). The history
    /// table is NOT seeded — the test under verification expects the runner to stamp it.
    /// </summary>
    public static void SeedAtV1(string connectionString, string tableName)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = string.Format(V1Ddl, tableName);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates an inbox table with the V2 column set (<c>ContextKey</c> present, but no
    /// <c>CausationId</c>) — the pre-Replay schema a user runs after upgrading Brighter
    /// without applying the V3 causation migration.
    /// </summary>
    public static void SeedAtV2(string connectionString, string tableName)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = string.Format(V2Ddl, tableName);
        command.ExecuteNonQuery();
    }
}

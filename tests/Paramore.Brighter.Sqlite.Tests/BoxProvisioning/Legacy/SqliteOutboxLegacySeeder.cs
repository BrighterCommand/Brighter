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
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning.Legacy;

/// <summary>
/// Seeds a SQLite outbox table at a historical logical version (V1..V7) using hand-rolled
/// <c>CREATE TABLE</c> DDL per ADR 0057 §1 archaeology. Used by mid-chain-failure /
/// bootstrap-at-V_k tests so the runner exercises real legacy schemas — not migration UpScript
/// round-trips.
/// </summary>
/// <remarks>
/// Cumulative column sets match <see cref="BoxProvisioning.Sqlite.SqliteOutboxMigrationCatalog"/>
/// LogicalColumns. SQLite has no declared housekeeping (the engine's implicit <c>rowid</c> is
/// not part of the DDL surface) and no synthetic identity column (unlike MSSQL <c>Id</c>,
/// MySQL <c>Created</c>/<c>CreatedID</c>, Postgres <c>id</c>). Column types align with the
/// live <see cref="BoxProvisioning.Sqlite.SqliteOutboxMigrationCatalog"/> (TEXT for everything
/// except <c>Body</c> in binary mode — text-mode is the default this seeder uses) so the V7
/// hand-rolled shape and the live builder shape have identical column-name sets. Column order
/// differs from the live builder, which interleaves V_k columns; the migration-list-order
/// chosen here is the simpler mental model and the detection helper / drift extractor are
/// both order-insensitive.
/// </remarks>
internal static class SqliteOutboxLegacySeeder
{
    private static readonly IReadOnlyDictionary<int, string[]> ColumnsByVersion = new Dictionary<int, string[]>
    {
        // V1 baseline: V1 logical columns. MessageId carries COLLATE NOCASE for the GUID
        // case-insensitivity workaround documented on the live SqliteOutboxBuilder.
        [1] =
        [
            "[MessageId] TEXT NOT NULL COLLATE NOCASE",
            "[Topic] TEXT NULL",
            "[MessageType] TEXT NULL",
            "[Timestamp] TEXT NULL",
            "[HeaderBag] TEXT NULL",
            "[Body] TEXT NULL"
        ],
        // V2 adds: Dispatched (#3c30343fa, 2019-07).
        [2] = ["[Dispatched] TEXT NULL"],
        // V3 adds: CorrelationId, ReplyTo, ContentType (#79100f509 / PR #1401).
        [3] =
        [
            "[CorrelationId] TEXT NULL",
            "[ReplyTo] TEXT NULL",
            "[ContentType] TEXT NULL"
        ],
        // V4 adds: PartitionKey (#1cdc04b60 / PR #2560 — same as MSSQL/MySQL; Postgres lagged).
        [4] = ["[PartitionKey] TEXT NULL"],
        // V5 adds: CloudEvents columns (#b740a68ed / PR #3633).
        [5] =
        [
            "[Source] TEXT NULL",
            "[Type] TEXT NULL",
            "[DataSchema] TEXT NULL",
            "[Subject] TEXT NULL",
            "[TraceParent] TEXT NULL",
            "[TraceState] TEXT NULL",
            "[Baggage] TEXT NULL"
        ],
        // V6 adds: WorkflowId, JobId (#0e79332f1 / PR #3693).
        [6] =
        [
            "[WorkflowId] TEXT NULL",
            "[JobId] TEXT NULL"
        ],
        // V7 adds: DataRef, SpecVersion (#d67dac947 / PR #3790).
        [7] =
        [
            "[DataRef] TEXT NULL",
            "[SpecVersion] TEXT NULL"
        ]
    };

    public static void SeedAtV(int version, string connectionString, string tableName)
    {
        if (version < 1 || version > 7)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Outbox version must be 1..7");

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = BuildCreateTable(version, tableName);
        command.ExecuteNonQuery();
    }

    private static string BuildCreateTable(int version, string tableName)
    {
        var fragments = new List<string>();
        for (var v = 1; v <= version; v++)
        {
            fragments.AddRange(ColumnsByVersion[v]);
        }

        return $"CREATE TABLE [{tableName}] ({Environment.NewLine}    " +
               string.Join("," + Environment.NewLine + "    ", fragments) +
               $"{Environment.NewLine});";
    }
}

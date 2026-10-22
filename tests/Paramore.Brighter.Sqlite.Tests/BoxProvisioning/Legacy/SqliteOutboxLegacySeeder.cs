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

// Hand-rolled legacy DDL per version (ADR 0057 §1), so tests run against real legacy schemas, not replayed
// UpScripts. Column order differs from the live builder; detection and drift checks are order-insensitive.
internal static class SqliteOutboxLegacySeeder
{
    private static readonly IReadOnlyDictionary<int, string[]> ColumnsByVersion = new Dictionary<int, string[]>
    {
        // V1 baseline. MessageId carries COLLATE NOCASE, as SqliteOutboxBuilder does.
        [1] =
        [
            "[MessageId] TEXT NOT NULL COLLATE NOCASE",
            "[Topic] TEXT NULL",
            "[MessageType] TEXT NULL",
            "[Timestamp] TEXT NULL",
            "[HeaderBag] TEXT NULL",
            "[Body] TEXT NULL"
        ],
        // V2 adds: Dispatched (3c30343fa, 2019-07).
        [2] = ["[Dispatched] TEXT NULL"],
        // V3 adds: CorrelationId, ReplyTo, ContentType (79100f509).
        [3] =
        [
            "[CorrelationId] TEXT NULL",
            "[ReplyTo] TEXT NULL",
            "[ContentType] TEXT NULL"
        ],
        // V4 adds: PartitionKey (1cdc04b60).
        [4] = ["[PartitionKey] TEXT NULL"],
        // V5 adds: CloudEvents columns (b740a68ed).
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
        // V6 adds: WorkflowId, JobId (0e79332f1).
        [6] =
        [
            "[WorkflowId] TEXT NULL",
            "[JobId] TEXT NULL"
        ],
        // V7 adds: DataRef, SpecVersion (d67dac947).
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

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
using Npgsql;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.Legacy;

// Hand-rolled legacy DDL per version (ADR 0057 §1), so tests run against real legacy schemas, not replayed
// UpScripts. Column types match PostgreSqlOutboxBuilder.
internal static class PostgreSqlOutboxLegacySeeder
{
    private static readonly IReadOnlyDictionary<int, string[]> ColumnsByVersion = new Dictionary<int, string[]>
    {
        // V1 baseline: the id key plus the V1 logical columns.
        [1] =
        [
            "id bigserial PRIMARY KEY",
            "messageid character varying(255) UNIQUE NOT NULL",
            "topic character varying(255) NULL",
            "messagetype character varying(32) NULL",
            "timestamp timestamptz NULL",
            "headerbag text NULL",
            "body text NULL"
        ],
        // V2 adds: dispatched (3c30343fa, 2019-07).
        [2] = ["dispatched timestamptz NULL"],
        // V3 adds: correlationid, replyto, contenttype (79100f509).
        [3] =
        [
            "correlationid character varying(255) NULL",
            "replyto character varying(255) NULL",
            "contenttype character varying(128) NULL"
        ],
        // V4 adds: partitionkey (cff67fd5e).
        [4] = ["partitionkey character varying(128) NULL"],
        // V5 adds: CloudEvents columns (b740a68ed).
        [5] =
        [
            "source character varying(255) NULL",
            "type character varying(255) NULL",
            "dataschema character varying(255) NULL",
            "subject character varying(255) NULL",
            "traceparent character varying(255) NULL",
            "tracestate character varying(255) NULL",
            "baggage text NULL"
        ],
        // V6 adds: workflowid, jobid (0e79332f1).
        [6] =
        [
            "workflowid character varying(255) NULL",
            "jobid character varying(255) NULL"
        ],
        // V7 adds: dataref, specversion (d67dac947).
        [7] =
        [
            "dataref character varying(255) NULL",
            "specversion character varying(255) NULL"
        ]
    };

    public static void SeedAtV(int version, string connectionString, string tableName)
    {
        if (version < 1 || version > 7)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Outbox version must be 1..7");

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = BuildCreateTable(version, tableName);
        command.ExecuteNonQuery();
    }

    private static string BuildCreateTable(int version, string tableName)
    {
        var columns = new List<string>();
        for (var v = 1; v <= version; v++)
        {
            columns.AddRange(ColumnsByVersion[v]);
        }
        return $"CREATE TABLE \"{tableName}\" ({Environment.NewLine}    " +
               string.Join("," + Environment.NewLine + "    ", columns) +
               $"{Environment.NewLine});";
    }
}

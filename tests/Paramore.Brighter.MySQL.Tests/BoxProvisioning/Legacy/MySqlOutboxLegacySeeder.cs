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
using MySqlConnector;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning.Legacy;

/// <summary>
/// Seeds a MySQL outbox table at a historical logical version (V1..V7) using hand-rolled
/// <c>CREATE TABLE</c> DDL per ADR 0057 §1 archaeology. Used by mid-chain-failure /
/// bootstrap-at-V_k tests so the runner exercises real legacy schemas — not migration UpScript
/// round-trips.
/// </summary>
/// <remarks>
/// Cumulative column sets match <see cref="BoxProvisioning.MySql.MySqlOutboxMigrations"/>
/// LogicalColumns plus the V1 housekeeping (<c>Created</c>, <c>CreatedID</c>) and PK
/// <c>MessageId</c>. Column types align with the live <c>MySqlOutboxBuilder</c> DDL so the V7
/// hand-rolled shape and the live builder shape are byte-equivalent.
/// </remarks>
internal static class MySqlOutboxLegacySeeder
{
    private static readonly IReadOnlyDictionary<int, string[]> ColumnsByVersion = new Dictionary<int, string[]>
    {
        // V1 baseline: V1 logical columns.
        [1] =
        [
            "`MessageId` VARCHAR(255) NOT NULL",
            "`Topic` VARCHAR(255) NOT NULL",
            "`MessageType` VARCHAR(32) NOT NULL",
            "`Timestamp` TIMESTAMP(3) NOT NULL",
            "`HeaderBag` TEXT NOT NULL",
            "`Body` TEXT NOT NULL"
        ],
        // V2 adds: Dispatched (#3c30343fa, 2019-07).
        [2] = ["`Dispatched` TIMESTAMP(3) NULL"],
        // V3 adds: CorrelationId, ReplyTo, ContentType (#79100f509 / PR #1401).
        [3] =
        [
            "`CorrelationId` VARCHAR(255) NULL",
            "`ReplyTo` VARCHAR(255) NULL",
            "`ContentType` VARCHAR(128) NULL"
        ],
        // V4 adds: PartitionKey (#1cdc04b60 / PR #2560).
        [4] = ["`PartitionKey` VARCHAR(128) NULL"],
        // V5 adds: CloudEvents columns (#b740a68ed / PR #3633).
        [5] =
        [
            "`Source` VARCHAR(255) NULL",
            "`Type` VARCHAR(255) NULL",
            "`DataSchema` VARCHAR(255) NULL",
            "`Subject` VARCHAR(255) NULL",
            "`TraceParent` VARCHAR(255) NULL",
            "`TraceState` VARCHAR(255) NULL",
            "`Baggage` TEXT NULL"
        ],
        // V6 adds: WorkflowId, JobId (#0e79332f1 / PR #3693).
        [6] =
        [
            "`WorkflowId` VARCHAR(255) NULL",
            "`JobId` VARCHAR(255) NULL"
        ],
        // V7 adds: DataRef, SpecVersion (#d67dac947 / PR #3790).
        [7] =
        [
            "`DataRef` VARCHAR(255) NULL",
            "`SpecVersion` VARCHAR(255) NULL"
        ]
    };

    private static readonly string[] s_housekeepingAndKeys =
    [
        "`Created` TIMESTAMP(3) NOT NULL DEFAULT NOW(3)",
        "`CreatedID` INT(11) NOT NULL AUTO_INCREMENT",
        "UNIQUE(`CreatedID`)",
        "PRIMARY KEY (`MessageId`)"
    ];

    public static void SeedAtV(int version, string connectionString, string tableName)
    {
        if (version < 1 || version > 7)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Outbox version must be 1..7");

        using var connection = new MySqlConnection(connectionString);
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
        fragments.AddRange(s_housekeepingAndKeys);

        return $"CREATE TABLE `{tableName}` ({Environment.NewLine}    " +
               string.Join("," + Environment.NewLine + "    ", fragments) +
               $"{Environment.NewLine}) ENGINE = InnoDB;";
    }
}

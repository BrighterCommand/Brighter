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
using System.Linq;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using System.Threading.Tasks;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class SqliteOutboxMigrationsTests
{
    //SQLite identifiers are case-insensitive (per QuoteStyle.Sqlite); LogicalColumns are
    //OrdinalIgnoreCase to match the SqliteOutboxMigrationCatalog storage convention.
    private const string TableName = "outbox_test";

    private static readonly string[] s_v1Columns =
        ["MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"];

    private static readonly string[] s_v2Added = ["Dispatched"];
    private static readonly string[] s_v3Added = ["CorrelationId", "ReplyTo", "ContentType"];
    private static readonly string[] s_v4Added = ["PartitionKey"];

    private static readonly string[] s_v5Added =
        ["Source", "Type", "DataSchema", "Subject", "TraceParent", "TraceState", "Baggage"];

    private static readonly string[] s_v6Added = ["WorkflowId", "JobId"];
    private static readonly string[] s_v7Added = ["DataRef", "SpecVersion"];

    [Test]
    public async Task When_sqlite_outbox_migrations_are_listed_it_should_return_v1_through_v7_with_idempotency_check_sql_and_plain_alter_upscripts()
    {
        //Arrange — derive each version's expected LogicalColumns by accumulating per-version
        //additions from the archaeology (spec README outbox table). SQLite's V1 UpScript stays
        //the live SqliteOutboxBuilder DDL (fresh-install fast path); V2..V7 use plain
        //ALTER TABLE ADD COLUMN and pair with an IdempotencyCheckSql that probes
        //pragma_table_info — the SQLite-specific shape per ADR §5/§6 (SQLite's grammar lacks
        //ALTER TABLE ADD COLUMN IF NOT EXISTS, so the check is hoisted to a separate field).
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            outBoxTableName: TableName);
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new SqliteOutboxMigrationCatalog().All(config);

        //Assert — exactly seven migrations numbered 1..7 in order.
        await Assert.That(migrations.Count).IsEqualTo(7);
        for (var i = 0; i < migrations.Count; i++)
        {
            await Assert.That(migrations[i].Version.Value).IsEqualTo(i + 1);
        }

        //Assert — LogicalColumns at each version match the cumulative archaeology.
        for (var v = 1; v <= 7; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            await Assert.That(expected.SetEquals(migration.LogicalColumns)).IsTrue();
        }

        //Assert — V1 IdempotencyCheckSql is null (V1 is the full CREATE TABLE — its own
        //CREATE TABLE IF NOT EXISTS provides idempotency).
        await Assert.That(migrations[0].IdempotencyCheckSql).IsNull();

        //Assert — V2..V7 each carry an IdempotencyCheckSql probing pragma_table_info for
        //a column added by that version. Per ADR §6, the runner evaluates this scalar before
        //running UpScript and skips it (still stamping history) when the result is > 0.
        for (var v = 2; v <= 7; v++)
        {
            var check = migrations[v - 1].IdempotencyCheckSql?.Value;
            await Assert.That(string.IsNullOrWhiteSpace(check)).IsFalse().Because($"V{v} must have a non-empty IdempotencyCheckSql per ADR §6");
            await Assert.That(check).Contains("SELECT COUNT(*)");
            await Assert.That(check).Contains($"pragma_table_info('{TableName}')");
            await Assert.That(check).Contains("WHERE name=");
        }

        //Assert — V2..V7 UpScripts are plain ALTER TABLE ADD COLUMN statements with no
        //embedded existence guard. SQLite's grammar can't host ALTER ... IF NOT EXISTS, and
        //it has no PREPARE/EXECUTE conditional pattern (unlike MySQL) — the existence check
        //lives entirely in IdempotencyCheckSql, applied by the runner.
        for (var v = 2; v <= 7; v++)
        {
            var script = migrations[v - 1].UpScript.Value;
            await Assert.That(script).Contains($"ALTER TABLE [{TableName}]");
            await Assert.That(script).Contains("ADD COLUMN");
            await Assert.That(script).DoesNotContain("pragma_table_info");
            await Assert.That(script).DoesNotContain("IF NOT EXISTS");
            await Assert.That(script).DoesNotContain("PREPARE");
            await Assert.That(script).DoesNotContain("information_schema");
            await Assert.That(script).DoesNotContain("COL_LENGTH");
        }

        //Assert — every column added by V_k appears as its own [col] ADD COLUMN clause in
        //V_k's UpScript. The expected new columns for V_k = LogicalColumns(V_k) \ LogicalColumns(V_{k-1}).
        for (var v = 2; v <= 7; v++)
        {
            var script = migrations[v - 1].UpScript.Value;
            var newColumns = new HashSet<string>(expectedPerVersion[v], StringComparer.OrdinalIgnoreCase);
            newColumns.ExceptWith(expectedPerVersion[v - 1]);
            foreach (var column in newColumns)
            {
                await Assert.That(script).Contains($"[{column}]");
            }
        }

        //Assert — V1 has no single source commit; V2..V7 each carry archaeology pointers.
        await Assert.That(migrations[0].SourceReference).IsNull();
        for (var v = 2; v <= 7; v++)
        {
            await Assert.That(string.IsNullOrWhiteSpace(migrations[v - 1].SourceReference)).IsFalse().Because($"V{v} must have a non-empty SourceReference (archaeology pointer)");
        }

        //Assert — V4 PartitionKey on SQLite ships with the cross-backend commit
        //1cdc04b60 / PR #2560 (same as MSSQL/MySQL — distinct from Postgres's
        //cff67fd5e / #3464 which lagged the payload widening). Pins archaeology accuracy.
        await Assert.That(migrations[3].SourceReference).IsEqualTo("1cdc04b60 / #2560");
    }

    private static Dictionary<int, HashSet<string>> BuildExpectedColumnsByVersion()
    {
        var byVersion = new Dictionary<int, HashSet<string>>();
        var cumulative = new HashSet<string>(s_v1Columns, StringComparer.OrdinalIgnoreCase);
        byVersion[1] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        cumulative.UnionWith(s_v2Added); byVersion[2] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);
        cumulative.UnionWith(s_v3Added); byVersion[3] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);
        cumulative.UnionWith(s_v4Added); byVersion[4] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);
        cumulative.UnionWith(s_v5Added); byVersion[5] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);
        cumulative.UnionWith(s_v6Added); byVersion[6] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);
        cumulative.UnionWith(s_v7Added); byVersion[7] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        return byVersion;
    }
}
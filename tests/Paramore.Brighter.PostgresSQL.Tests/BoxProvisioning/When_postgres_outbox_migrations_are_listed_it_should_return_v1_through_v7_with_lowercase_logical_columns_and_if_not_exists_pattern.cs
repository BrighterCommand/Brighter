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
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using System.Threading.Tasks;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlOutboxMigrationsTests
{
    //Postgres folds unquoted identifiers to lowercase at runtime; ADR §1 mandates lowercase
    //LogicalColumns so that detection-by-information_schema (which returns lowercase) works
    //without case mismatch. Comparer is StringComparer.Ordinal to enforce that contract.
    private static readonly string[] s_v1Columns =
        ["messageid", "topic", "messagetype", "timestamp", "headerbag", "body"];

    private static readonly string[] s_v2Added = ["dispatched"];
    private static readonly string[] s_v3Added = ["correlationid", "replyto", "contenttype"];
    private static readonly string[] s_v4Added = ["partitionkey"];

    private static readonly string[] s_v5Added =
        ["source", "type", "dataschema", "subject", "traceparent", "tracestate", "baggage"];

    private static readonly string[] s_v6Added = ["workflowid", "jobid"];
    private static readonly string[] s_v7Added = ["dataref", "specversion"];

    [Test]
    public async Task When_postgres_outbox_migrations_are_listed_it_should_return_v1_through_v7_with_lowercase_logical_columns_and_if_not_exists_pattern()
    {
        //Arrange — derive each version's expected (lowercase) LogicalColumns by accumulating
        //per-version additions from the archaeology (spec README outbox table).
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            outBoxTableName: "outbox_test");
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new PostgreSqlOutboxMigrationCatalog().All(config);

        //Assert — exactly seven migrations numbered 1..7 in order.
        await Assert.That(migrations.Count).IsEqualTo(7);
        for (var i = 0; i < migrations.Count; i++)
        {
            await Assert.That(migrations[i].Version.Value).IsEqualTo(i + 1);
        }

        //Assert — LogicalColumns at each version match the cumulative archaeology, all lowercase.
        for (var v = 1; v <= 7; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            await Assert.That(expected.SetEquals(migration.LogicalColumns)).IsTrue();
        }

        //Assert — V2..V7 UpScripts use the Postgres-native idempotent ADD COLUMN IF NOT EXISTS
        //pattern (ADR §5). Case-insensitive contains check tolerates whitespace and casing.
        for (var v = 2; v <= 7; v++)
        {
            var script = migrations[v - 1].UpScript.Value;
            await Assert.That(script).Contains("ADD COLUMN IF NOT EXISTS");
        }

        //Assert — V1 has no single source commit; V2..V7 each carry archaeology pointers.
        await Assert.That(migrations[0].SourceReference).IsNull();
        for (var v = 2; v <= 7; v++)
        {
            await Assert.That(string.IsNullOrWhiteSpace(migrations[v - 1].SourceReference)).IsFalse().Because($"V{v} must have a non-empty SourceReference (archaeology pointer)");
        }

        //Assert — V4 PartitionKey was added to Postgres in commit cff67fd5e / PR #3464
        //(distinct from the MSSQL V4 commit 1cdc04b60 / #2560 because Postgres lagged the
        //payload widening / binary-variant work). This pins archaeology accuracy.
        await Assert.That(migrations[3].SourceReference).IsEqualTo("cff67fd5e / #3464");
    }

    private static Dictionary<int, HashSet<string>> BuildExpectedColumnsByVersion()
    {
        var byVersion = new Dictionary<int, HashSet<string>>();
        var cumulative = new HashSet<string>(s_v1Columns, StringComparer.Ordinal);
        byVersion[1] = new HashSet<string>(cumulative, StringComparer.Ordinal);

        cumulative.UnionWith(s_v2Added); byVersion[2] = new HashSet<string>(cumulative, StringComparer.Ordinal);
        cumulative.UnionWith(s_v3Added); byVersion[3] = new HashSet<string>(cumulative, StringComparer.Ordinal);
        cumulative.UnionWith(s_v4Added); byVersion[4] = new HashSet<string>(cumulative, StringComparer.Ordinal);
        cumulative.UnionWith(s_v5Added); byVersion[5] = new HashSet<string>(cumulative, StringComparer.Ordinal);
        cumulative.UnionWith(s_v6Added); byVersion[6] = new HashSet<string>(cumulative, StringComparer.Ordinal);
        cumulative.UnionWith(s_v7Added); byVersion[7] = new HashSet<string>(cumulative, StringComparer.Ordinal);

        return byVersion;
    }
}
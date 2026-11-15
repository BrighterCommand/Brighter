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

/// <summary>
/// Postgres folds unquoted identifiers to lowercase, so LogicalColumns are lowercase and compared with
/// <see cref="StringComparer.Ordinal"/> (ADR 0057 §2). V2–V8 use ADD COLUMN IF NOT EXISTS (ADR 0057 §5), and V8 also
/// creates the causationid replay index.
/// </summary>
public class PostgreSqlOutboxMigrationsTests
{
    private static readonly string[] s_v1Columns =
        ["messageid", "topic", "messagetype", "timestamp", "headerbag", "body"];

    private static readonly string[] s_v2Added = ["dispatched"];
    private static readonly string[] s_v3Added = ["correlationid", "replyto", "contenttype"];
    private static readonly string[] s_v4Added = ["partitionkey"];

    private static readonly string[] s_v5Added =
        ["source", "type", "dataschema", "subject", "traceparent", "tracestate", "baggage"];

    private static readonly string[] s_v6Added = ["workflowid", "jobid"];
    private static readonly string[] s_v7Added = ["dataref", "specversion"];
    private static readonly string[] s_v8Added = ["causationid"];

    [Test]
    public async System.Threading.Tasks.Task When_postgres_outbox_migrations_are_listed_it_should_return_v1_through_v8_with_lowercase_logical_columns_and_if_not_exists_pattern()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            outBoxTableName: "outbox_test");
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new PostgreSqlOutboxMigrationCatalog().All(config);

        //Assert
        await Assert.That(migrations.Count).IsEqualTo(8);
        for (var i = 0; i < migrations.Count; i++)
        {
            await Assert.That(migrations[i].Version.Value).IsEqualTo(i + 1);
        }

        for (var v = 1; v <= 8; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            await Assert.That(expected.SetEquals(migration.LogicalColumns)).IsTrue();
        }

        for (var v = 2; v <= 8; v++)
        {
            var script = migrations[v - 1].UpScript.Value;
            await Assert.That(script).Contains("ADD COLUMN IF NOT EXISTS");
        }

        await Assert.That(migrations[0].SourceReference).IsNull();
        for (var v = 2; v <= 8; v++)
        {
            await Assert.That(string.IsNullOrWhiteSpace(migrations[v - 1].SourceReference)).IsFalse().Because($"V{v} must have a non-empty SourceReference (archaeology pointer)");
        }

        await Assert.That(migrations[3].SourceReference).IsEqualTo("cff67fd5e / #3464");

        var v8Script = migrations[7].UpScript;
        await Assert.That((v8Script).Value).Contains("causationid");
        await Assert.That((v8Script).Value).Contains("CREATE INDEX IF NOT EXISTS");
        await Assert.That((v8Script).Value).Contains("idx_outbox_test_causationid");
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
        cumulative.UnionWith(s_v8Added); byVersion[8] = new HashSet<string>(cumulative, StringComparer.Ordinal);

        return byVersion;
    }
}

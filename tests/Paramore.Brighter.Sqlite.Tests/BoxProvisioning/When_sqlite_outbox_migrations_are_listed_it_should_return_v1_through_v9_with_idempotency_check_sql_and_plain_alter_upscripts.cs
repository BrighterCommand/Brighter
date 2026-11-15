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

/// <summary>
/// SQLite's outbox catalog: V1 is the historical baseline DDL; V2..V8 are plain <c>ALTER TABLE ADD COLUMN</c>
/// statements paired with a <c>pragma_table_info</c> IdempotencyCheckSql, because SQLite has no
/// <c>ADD COLUMN IF NOT EXISTS</c> (ADR 0057 §5). V8 also creates the CausationId index, so it is
/// checked apart from the plain ALTERs. V9 adds MessageId uniqueness without adding columns.
/// </summary>
public class SqliteOutboxMigrationsTests
{
    private const string TableName = "outbox_test";
    private const int LastPlainAlterVersion = 7;

    private static readonly string[] s_v1Columns =
        ["MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"];

    private static readonly string[] s_v2Added = ["Dispatched"];
    private static readonly string[] s_v3Added = ["CorrelationId", "ReplyTo", "ContentType"];
    private static readonly string[] s_v4Added = ["PartitionKey"];

    private static readonly string[] s_v5Added =
        ["Source", "Type", "DataSchema", "Subject", "TraceParent", "TraceState", "Baggage"];

    private static readonly string[] s_v6Added = ["WorkflowId", "JobId"];
    private static readonly string[] s_v7Added = ["DataRef", "SpecVersion"];
    private static readonly string[] s_v8Added = ["CausationId"];

    [Test]
    public async Task When_sqlite_outbox_migrations_are_listed_it_should_return_v1_through_v9_with_idempotency_check_sql_and_plain_alter_upscripts()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            outBoxTableName: TableName);
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new SqliteOutboxMigrationCatalog().All(config);

        //Assert
        await Assert.That(migrations.Count).IsEqualTo(9);
        for (var i = 0; i < migrations.Count; i++)
        {
            await Assert.That(migrations[i].Version.Value).IsEqualTo(i + 1);
        }

        for (var v = 1; v <= 9; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            await Assert.That(expected.SetEquals(migration.LogicalColumns)).IsTrue();
        }

        await Assert.That(migrations[0].IdempotencyCheckSql).IsNull();

        for (var v = 2; v <= 8; v++)
        {
            var check = migrations[v - 1].IdempotencyCheckSql?.Value;
            await Assert.That(string.IsNullOrWhiteSpace(check)).IsFalse().Because($"V{v} must have a non-empty IdempotencyCheckSql per ADR §6");
            await Assert.That(check).Contains("SELECT COUNT(*)");
            await Assert.That(check).Contains($"pragma_table_info('{TableName}')");
            await Assert.That(check).Contains("WHERE name=");
        }

        for (var v = 2; v <= LastPlainAlterVersion; v++)
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

        var v8Script = migrations[7].UpScript;
        await Assert.That((v8Script).Value).Contains($"ALTER TABLE [{TableName}]");
        await Assert.That((v8Script).Value).Contains("[CausationId]");
        await Assert.That((v8Script).Value).Contains("CREATE INDEX IF NOT EXISTS");
        await Assert.That((v8Script).Value).Contains($"idx_{TableName}_CausationId");

        for (var v = 2; v <= 8; v++)
        {
            var script = migrations[v - 1].UpScript.Value;
            var newColumns = new HashSet<string>(expectedPerVersion[v], StringComparer.OrdinalIgnoreCase);
            newColumns.ExceptWith(expectedPerVersion[v - 1]);
            foreach (var column in newColumns)
            {
                await Assert.That(script).Contains($"[{column}]");
            }
        }

        await Assert.That(migrations[0].SourceReference).IsNull();
        for (var v = 2; v <= 8; v++)
        {
            await Assert.That(string.IsNullOrWhiteSpace(migrations[v - 1].SourceReference)).IsFalse().Because($"V{v} must have a non-empty SourceReference (archaeology pointer)");
        }

        await Assert.That((migrations[8].UpScript).Value).Contains("CREATE UNIQUE INDEX");
        await Assert.That((migrations[8].UpScript).Value).Contains("[MessageId] COLLATE NOCASE");
        await Assert.That((migrations[8].IdempotencyCheckSql).Value).Contains("pragma_index_list");
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
        cumulative.UnionWith(s_v8Added); byVersion[8] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        byVersion[9] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        return byVersion;
    }
}

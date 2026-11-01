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
using Xunit;

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

    [Fact]
    public void When_sqlite_outbox_migrations_are_listed_it_should_return_v1_through_v9_with_idempotency_check_sql_and_plain_alter_upscripts()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            outBoxTableName: TableName);
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new SqliteOutboxMigrationCatalog().All(config);

        //Assert
        Assert.Equal(9, migrations.Count);
        for (var i = 0; i < migrations.Count; i++)
        {
            Assert.Equal(i + 1, migrations[i].Version.Value);
        }

        for (var v = 1; v <= 9; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            Assert.True(
                expected.SetEquals(migration.LogicalColumns),
                $"V{v} LogicalColumns mismatch — " +
                $"expected: [{string.Join(", ", expected.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
                $"got: [{string.Join(", ", migration.LogicalColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
        }

        Assert.Null(migrations[0].IdempotencyCheckSql);

        for (var v = 2; v <= 8; v++)
        {
            var check = migrations[v - 1].IdempotencyCheckSql;
            Assert.False(
                string.IsNullOrWhiteSpace(check),
                $"V{v} must have a non-empty IdempotencyCheckSql per ADR §6");
            Assert.Contains("SELECT COUNT(*)", check, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"pragma_table_info('{TableName}')", check, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE name=", check, StringComparison.OrdinalIgnoreCase);
        }

        for (var v = 2; v <= LastPlainAlterVersion; v++)
        {
            var script = migrations[v - 1].UpScript;
            Assert.Contains($"ALTER TABLE [{TableName}]", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ADD COLUMN", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pragma_table_info", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("IF NOT EXISTS", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PREPARE", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("information_schema", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("COL_LENGTH", script, StringComparison.OrdinalIgnoreCase);
        }

        var v8Script = migrations[7].UpScript;
        Assert.Contains($"ALTER TABLE [{TableName}]", v8Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[CausationId]", v8Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE INDEX IF NOT EXISTS", v8Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"idx_{TableName}_CausationId", v8Script, StringComparison.OrdinalIgnoreCase);

        for (var v = 2; v <= 8; v++)
        {
            var script = migrations[v - 1].UpScript;
            var newColumns = new HashSet<string>(expectedPerVersion[v], StringComparer.OrdinalIgnoreCase);
            newColumns.ExceptWith(expectedPerVersion[v - 1]);
            foreach (var column in newColumns)
            {
                Assert.Contains($"[{column}]", script, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.Null(migrations[0].SourceReference);
        for (var v = 2; v <= 8; v++)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(migrations[v - 1].SourceReference),
                $"V{v} must have a non-empty SourceReference (archaeology pointer)");
        }

        Assert.Contains("CREATE UNIQUE INDEX", migrations[8].UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[MessageId] COLLATE NOCASE", migrations[8].UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pragma_index_list", migrations[8].IdempotencyCheckSql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("1cdc04b60 / #2560", migrations[3].SourceReference);
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

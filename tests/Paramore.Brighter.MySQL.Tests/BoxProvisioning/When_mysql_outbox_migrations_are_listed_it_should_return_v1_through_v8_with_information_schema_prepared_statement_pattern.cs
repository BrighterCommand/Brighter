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
using Paramore.Brighter.BoxProvisioning.MySql;
using System.Threading.Tasks;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// The MySQL outbox catalog lists V1..V8 with cumulative logical columns. V2..V8 guard each change with the
/// <c>information_schema</c> + prepared-statement probe, because MySQL before 8.0.29 has no
/// <c>ADD COLUMN IF NOT EXISTS</c> (ADR 0057 §5); V8 guards its <c>idx_CausationId</c> index the same way.
/// </summary>
public class MySqlOutboxMigrationsTests
{
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
    public async System.Threading.Tasks.Task When_mysql_outbox_migrations_are_listed_it_should_return_v1_through_v8_with_information_schema_prepared_statement_pattern()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            outBoxTableName: "outbox_test");
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new MySqlOutboxMigrationCatalog().All(config);

        //Assert
        await Assert.That(migrations.Count).IsEqualTo(ExpectedMigrationVersions.OutboxLatest);
        for (var i = 0; i < migrations.Count; i++)
        {
            await Assert.That(migrations[i].Version.Value).IsEqualTo(i + 1);
        }

        for (var v = 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            await Assert.That(expected.SetEquals(migration.LogicalColumns)).IsTrue();
        }

        for (var v = 2; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            var script = migrations[v - 1].UpScript.Value;
            await Assert.That(script).Contains("information_schema.columns");
            await Assert.That(script).Contains("PREPARE stmt FROM @q");
        }

        await Assert.That(migrations[0].SourceReference).IsNull();
        for (var v = 2; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            await Assert.That(string.IsNullOrWhiteSpace(migrations[v - 1].SourceReference)).IsFalse().Because($"V{v} must have a non-empty SourceReference (archaeology pointer)");
        }

        await Assert.That(migrations[3].SourceReference).IsEqualTo("1cdc04b60 / #2560");

        for (var v = 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            await Assert.That(migrations[v - 1].IdempotencyCheckSql).IsNull();
        }

        var v8Script = migrations[7].UpScript;
        await Assert.That((v8Script).Value).Contains("CausationId");
        await Assert.That((v8Script).Value).Contains("CREATE INDEX");
        await Assert.That((v8Script).Value).Contains("idx_CausationId");
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

        return byVersion;
    }
}

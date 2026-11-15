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
using Paramore.Brighter.BoxProvisioning.MsSql;
using System.Threading.Tasks;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// The MSSQL outbox catalog lists V1..V9 in order with cumulative logical columns; V2..V9 each cite a
/// source commit (V1 has none), and V8's UpScript also creates the CausationId replay index.
/// </summary>
public class MsSqlOutboxMigrationsTests
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
    public async Task When_mssql_outbox_migrations_are_listed_it_should_return_v1_through_v9_with_correct_logical_columns()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            outBoxTableName: "outbox_test");
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new MsSqlOutboxMigrationCatalog().All(config);

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

        await Assert.That(migrations[0].SourceReference).IsNull();
        for (var v = 2; v <= 9; v++)
        {
            await Assert.That(string.IsNullOrWhiteSpace(migrations[v - 1].SourceReference)).IsFalse().Because($"V{v} must have a non-empty SourceReference (archaeology pointer)");
        }

        var v9Script = migrations[8].UpScript;
        await Assert.That((v9Script).Value).Contains("[MessageId]");
        await Assert.That((v9Script).Value).Contains("CREATE UNIQUE INDEX");
        await Assert.That((v9Script).Value).Contains("IGNORE_DUP_KEY = ON");

        var v8Script = migrations[7].UpScript;
        await Assert.That((v8Script).Value).Contains("[CausationId]");
        await Assert.That((v8Script).Value).Contains("CREATE INDEX");
        await Assert.That((v8Script).Value).Contains("idx_outbox_test_CausationId");
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

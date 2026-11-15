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
/// The MySQL inbox catalog lists V1 to V3 in order, each with the cumulative columns of the versions
/// before it; V2 and V3 use the <c>information_schema</c> prepared-statement guard (ADR 0057 §5), V2 and V3
/// carry a <c>SourceReference</c> and V1 does not, and none sets <c>IdempotencyCheckSql</c>.
/// </summary>
public class MySqlInboxMigrationsTests
{
    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2Added = ["ContextKey"];

    private static readonly string[] s_v3Added = ["CausationId"];

    [Test]
    public async System.Threading.Tasks.Task When_mysql_inbox_migrations_are_listed_it_should_return_v1_through_v3_with_contextkey_and_causationid_added()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            inboxTableName: "inbox_test");
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new MySqlInboxMigrationCatalog().All(config);

        //Assert
        await Assert.That(migrations.Count).IsEqualTo(ExpectedMigrationVersions.InboxLatest);
        for (var i = 0; i < migrations.Count; i++)
        {
            await Assert.That(migrations[i].Version.Value).IsEqualTo(i + 1);
        }

        for (var v = 1; v <= ExpectedMigrationVersions.InboxLatest; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            await Assert.That(expected.SetEquals(migration.LogicalColumns)).IsTrue();
        }

        var v2Script = migrations[1].UpScript;
        await Assert.That((v2Script).Value).Contains("information_schema.columns");
        await Assert.That((v2Script).Value).Contains("PREPARE stmt FROM @q");

        var v3Script = migrations[2].UpScript.Value;
        await Assert.That(v3Script).Contains("information_schema.columns");
        await Assert.That(v3Script).Contains("PREPARE stmt FROM @q");

        await Assert.That(migrations[0].SourceReference).IsNull();
        await Assert.That(string.IsNullOrWhiteSpace(migrations[1].SourceReference)).IsFalse().Because("V2 must have a non-empty SourceReference (archaeology pointer for ContextKey addition)");
        await Assert.That(string.IsNullOrWhiteSpace(migrations[2].SourceReference)).IsFalse().Because("V3 must have a non-empty SourceReference (issue pointer for CausationId addition)");

        await Assert.That(migrations[0].IdempotencyCheckSql).IsNull();
        await Assert.That(migrations[1].IdempotencyCheckSql).IsNull();
        await Assert.That(migrations[2].IdempotencyCheckSql).IsNull();
    }

    private static Dictionary<int, HashSet<string>> BuildExpectedColumnsByVersion()
    {
        var byVersion = new Dictionary<int, HashSet<string>>();
        var cumulative = new HashSet<string>(s_v1Columns, StringComparer.OrdinalIgnoreCase);
        byVersion[1] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        cumulative.UnionWith(s_v2Added); byVersion[2] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        cumulative.UnionWith(s_v3Added); byVersion[3] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        return byVersion;
    }
}

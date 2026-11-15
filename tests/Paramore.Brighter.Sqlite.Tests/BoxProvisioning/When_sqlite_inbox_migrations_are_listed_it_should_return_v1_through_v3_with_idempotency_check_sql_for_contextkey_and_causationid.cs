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
/// SQLite has no <c>ADD COLUMN IF NOT EXISTS</c>, so V2 and V3 put the column probe in IdempotencyCheckSql
/// and keep UpScript a plain ALTER (ADR 0057 §5).
/// </summary>
public class SqliteInboxMigrationsTests
{
    private const string TableName = "inbox_test";

    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2Added = ["ContextKey"];

    private static readonly string[] s_v3Added = ["CausationId"];

    [Test]
    public async System.Threading.Tasks.Task When_sqlite_inbox_migrations_are_listed_it_should_return_v1_through_v3_with_idempotency_check_sql_for_contextkey_and_causationid()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            inboxTableName: TableName);
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new SqliteInboxMigrationCatalog().All(config);

        //Assert
        await Assert.That(migrations.Count).IsEqualTo(3);
        for (var i = 0; i < migrations.Count; i++)
        {
            await Assert.That(migrations[i].Version.Value).IsEqualTo(i + 1);
        }

        for (var v = 1; v <= 3; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            await Assert.That(expected.SetEquals(migration.LogicalColumns)).IsTrue();
        }

        await Assert.That(migrations[0].IdempotencyCheckSql).IsNull();

        var v2Check = migrations[1].IdempotencyCheckSql;
        await Assert.That(string.IsNullOrWhiteSpace(v2Check)).IsFalse().Because("V2 must have a non-empty IdempotencyCheckSql per ADR §6");
        await Assert.That((v2Check).Value).Contains("SELECT COUNT(*)");
        await Assert.That((v2Check).Value).Contains($"pragma_table_info('{TableName}')");
        await Assert.That((v2Check).Value).Contains("name='ContextKey'");

        var v2Script = migrations[1].UpScript;
        await Assert.That((v2Script).Value).Contains($"ALTER TABLE [{TableName}]");
        await Assert.That((v2Script).Value).Contains("ADD COLUMN");
        await Assert.That((v2Script).Value).Contains("[ContextKey]");
        await Assert.That((v2Script).Value).DoesNotContain("pragma_table_info");
        await Assert.That((v2Script).Value).DoesNotContain("IF NOT EXISTS");
        await Assert.That((v2Script).Value).DoesNotContain("PREPARE");
        await Assert.That((v2Script).Value).DoesNotContain("information_schema");
        await Assert.That((v2Script).Value).DoesNotContain("COL_LENGTH");

        var v3Check = migrations[2].IdempotencyCheckSql;
        await Assert.That(string.IsNullOrWhiteSpace(v3Check)).IsFalse().Because("V3 must have a non-empty IdempotencyCheckSql per ADR §6");
        await Assert.That((v3Check).Value).Contains("SELECT COUNT(*)");
        await Assert.That((v3Check).Value).Contains($"pragma_table_info('{TableName}')");
        await Assert.That((v3Check).Value).Contains("name='CausationId'");

        var v3Script = migrations[2].UpScript;
        await Assert.That((v3Script).Value).Contains($"ALTER TABLE [{TableName}]");
        await Assert.That((v3Script).Value).Contains("ADD COLUMN");
        await Assert.That((v3Script).Value).Contains("[CausationId]");
        await Assert.That((v3Script).Value).DoesNotContain("pragma_table_info");
        await Assert.That((v3Script).Value).DoesNotContain("IF NOT EXISTS");
        await Assert.That((v3Script).Value).DoesNotContain("PREPARE");
        await Assert.That((v3Script).Value).DoesNotContain("information_schema");
        await Assert.That((v3Script).Value).DoesNotContain("COL_LENGTH");

        await Assert.That(migrations[0].SourceReference).IsNull();
        await Assert.That(migrations[1].SourceReference).IsEqualTo("787c31c52");
        await Assert.That(migrations[2].SourceReference).IsEqualTo("#2541");
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

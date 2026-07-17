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

public class MySqlInboxMigrationsTests
{
    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2Added = ["ContextKey"];

    [Test]
    public async Task When_mysql_inbox_migrations_are_listed_it_should_return_v1_and_v2_with_contextkey_added_in_v2()
    {
        //Arrange — derive each version's expected LogicalColumns by accumulating the
        //per-version additions from the archaeology (spec README inbox table).
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            inboxTableName: "inbox_test");
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new MySqlInboxMigrationCatalog().All(config);

        //Assert — exactly two migrations numbered 1..2 in order.
        await Assert.That(migrations.Count).IsEqualTo(2);
        for (var i = 0; i < migrations.Count; i++)
        {
            await Assert.That(migrations[i].Version.Value).IsEqualTo(i + 1);
        }

        //Assert — LogicalColumns at each version matches the cumulative archaeology.
        for (var v = 1; v <= 2; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            await Assert.That(expected.SetEquals(migration.LogicalColumns)).IsTrue();
        }

        //Assert — V2 UpScript uses the MySQL information_schema + prepared-statement
        //idempotency pattern (ADR §5). Same pattern used by MySQL outbox V2..V7.
        var v2Script = migrations[1].UpScript.Value;
        await Assert.That(v2Script).Contains("information_schema.columns");
        await Assert.That(v2Script).Contains("PREPARE stmt FROM @q");

        //Assert — V1 has no single source commit so SourceReference is null;
        //V2 carries the archaeology pointer for the ContextKey addition (787c31c52, Oct 2018).
        await Assert.That(migrations[0].SourceReference).IsNull();
        await Assert.That(string.IsNullOrWhiteSpace(migrations[1].SourceReference)).IsFalse().Because("V2 must have a non-empty SourceReference (archaeology pointer for ContextKey addition)");

        //Assert — IdempotencyCheckSql is null for MySQL (only SQLite uses that field per ADR §6).
        await Assert.That(migrations[0].IdempotencyCheckSql).IsNull();
        await Assert.That(migrations[1].IdempotencyCheckSql).IsNull();
    }

    private static Dictionary<int, HashSet<string>> BuildExpectedColumnsByVersion()
    {
        var byVersion = new Dictionary<int, HashSet<string>>();
        var cumulative = new HashSet<string>(s_v1Columns, StringComparer.OrdinalIgnoreCase);
        byVersion[1] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        cumulative.UnionWith(s_v2Added); byVersion[2] = new HashSet<string>(cumulative, StringComparer.OrdinalIgnoreCase);

        return byVersion;
    }
}
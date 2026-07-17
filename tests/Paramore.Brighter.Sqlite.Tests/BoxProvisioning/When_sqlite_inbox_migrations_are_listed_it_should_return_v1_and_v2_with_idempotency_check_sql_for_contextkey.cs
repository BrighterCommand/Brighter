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

public class SqliteInboxMigrationsTests
{
    private const string TableName = "inbox_test";

    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2Added = ["ContextKey"];

    [Test]
    public async Task When_sqlite_inbox_migrations_are_listed_it_should_return_v1_and_v2_with_idempotency_check_sql_for_contextkey()
    {
        //Arrange — derive each version's expected LogicalColumns by accumulating the
        //per-version additions from the archaeology (spec README inbox table). SQLite inbox
        //matches MSSQL/MySQL: V1 baseline → V2 adds ContextKey (787c31c52, Oct 2018). Per
        //ADR §6, V2 hoists the existence guard out of UpScript into IdempotencyCheckSql
        //because SQLite's grammar lacks ALTER TABLE ADD COLUMN IF NOT EXISTS.
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            inboxTableName: TableName);
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new SqliteInboxMigrationCatalog().All(config);

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

        //Assert — V1 IdempotencyCheckSql is null (V1 is the full CREATE TABLE; its own
        //CREATE TABLE IF NOT EXISTS provides idempotency).
        await Assert.That(migrations[0].IdempotencyCheckSql).IsNull();

        //Assert — V2 IdempotencyCheckSql probes pragma_table_info for ContextKey per
        //ADR §6. Runner (Task 4.4) evaluates this scalar before UpScript and skips the
        //ALTER (still stamping history) when the column is already present.
        var v2Check = migrations[1].IdempotencyCheckSql?.Value;
        await Assert.That(string.IsNullOrWhiteSpace(v2Check)).IsFalse().Because("V2 must have a non-empty IdempotencyCheckSql per ADR §6");
        await Assert.That(v2Check).Contains("SELECT COUNT(*)");
        await Assert.That(v2Check).Contains($"pragma_table_info('{TableName}')");
        await Assert.That(v2Check).Contains("name='ContextKey'");

        //Assert — V2 UpScript is a plain ALTER TABLE ADD COLUMN with no embedded existence
        //guard (unlike MSSQL's IF COL_LENGTH, Postgres's IF NOT EXISTS, or MySQL's
        //information_schema + prepared-statement pattern).
        var v2Script = migrations[1].UpScript.Value;
        await Assert.That(v2Script).Contains($"ALTER TABLE [{TableName}]");
        await Assert.That(v2Script).Contains("ADD COLUMN");
        await Assert.That(v2Script).Contains("[ContextKey]");
        await Assert.That(v2Script).DoesNotContain("pragma_table_info");
        await Assert.That(v2Script).DoesNotContain("IF NOT EXISTS");
        await Assert.That(v2Script).DoesNotContain("PREPARE");
        await Assert.That(v2Script).DoesNotContain("information_schema");
        await Assert.That(v2Script).DoesNotContain("COL_LENGTH");

        //Assert — V1 has no single source commit so SourceReference is null;
        //V2 carries the archaeology pointer for the ContextKey addition (787c31c52, Oct 2018,
        //same commit as MSSQL/MySQL inbox V2).
        await Assert.That(migrations[0].SourceReference).IsNull();
        await Assert.That(migrations[1].SourceReference).IsEqualTo("787c31c52");
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
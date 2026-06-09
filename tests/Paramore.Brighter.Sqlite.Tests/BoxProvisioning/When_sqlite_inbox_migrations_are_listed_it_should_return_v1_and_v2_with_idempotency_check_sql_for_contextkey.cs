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

public class SqliteInboxMigrationsTests
{
    private const string TableName = "inbox_test";

    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2Added = ["ContextKey"];

    [Fact]
    public void When_sqlite_inbox_migrations_are_listed_it_should_return_v1_and_v2_with_idempotency_check_sql_for_contextkey()
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
        Assert.Equal(2, migrations.Count);
        for (var i = 0; i < migrations.Count; i++)
        {
            Assert.Equal(i + 1, migrations[i].Version.Value);
        }

        //Assert — LogicalColumns at each version matches the cumulative archaeology.
        for (var v = 1; v <= 2; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            Assert.True(
                expected.SetEquals(migration.LogicalColumns),
                $"V{v} LogicalColumns mismatch — " +
                $"expected: [{string.Join(", ", expected.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
                $"got: [{string.Join(", ", migration.LogicalColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
        }

        //Assert — V1 IdempotencyCheckSql is null (V1 is the full CREATE TABLE; its own
        //CREATE TABLE IF NOT EXISTS provides idempotency).
        Assert.Null(migrations[0].IdempotencyCheckSql);

        //Assert — V2 IdempotencyCheckSql probes pragma_table_info for ContextKey per
        //ADR §6. Runner (Task 4.4) evaluates this scalar before UpScript and skips the
        //ALTER (still stamping history) when the column is already present.
        var v2Check = migrations[1].IdempotencyCheckSql;
        Assert.False(
            string.IsNullOrWhiteSpace(v2Check),
            "V2 must have a non-empty IdempotencyCheckSql per ADR §6");
        Assert.Contains("SELECT COUNT(*)", v2Check, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"pragma_table_info('{TableName}')", v2Check, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name='ContextKey'", v2Check, StringComparison.OrdinalIgnoreCase);

        //Assert — V2 UpScript is a plain ALTER TABLE ADD COLUMN with no embedded existence
        //guard (unlike MSSQL's IF COL_LENGTH, Postgres's IF NOT EXISTS, or MySQL's
        //information_schema + prepared-statement pattern).
        var v2Script = migrations[1].UpScript;
        Assert.Contains($"ALTER TABLE [{TableName}]", v2Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ADD COLUMN", v2Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[ContextKey]", v2Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pragma_table_info", v2Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IF NOT EXISTS", v2Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PREPARE", v2Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("information_schema", v2Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COL_LENGTH", v2Script, StringComparison.OrdinalIgnoreCase);

        //Assert — V1 has no single source commit so SourceReference is null;
        //V2 carries the archaeology pointer for the ContextKey addition (787c31c52, Oct 2018,
        //same commit as MSSQL/MySQL inbox V2).
        Assert.Null(migrations[0].SourceReference);
        Assert.Equal("787c31c52", migrations[1].SourceReference);
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

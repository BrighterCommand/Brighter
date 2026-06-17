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
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlOutboxMigrationsTests
{
    //MySQL identifiers are case-insensitive; comparer is OrdinalIgnoreCase to match the
    //existing MySqlOutboxMigrationCatalog LogicalColumns convention (PascalCase storage, lookup-folded).
    private static readonly string[] s_v1Columns =
        ["MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"];

    private static readonly string[] s_v2Added = ["Dispatched"];
    private static readonly string[] s_v3Added = ["CorrelationId", "ReplyTo", "ContentType"];
    private static readonly string[] s_v4Added = ["PartitionKey"];

    private static readonly string[] s_v5Added =
        ["Source", "Type", "DataSchema", "Subject", "TraceParent", "TraceState", "Baggage"];

    private static readonly string[] s_v6Added = ["WorkflowId", "JobId"];
    private static readonly string[] s_v7Added = ["DataRef", "SpecVersion"];

    [Fact]
    public void When_mysql_outbox_migrations_are_listed_it_should_return_v1_through_v7_with_information_schema_prepared_statement_pattern()
    {
        //Arrange — derive each version's expected LogicalColumns by accumulating
        //per-version additions from the archaeology (spec README outbox table).
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            outBoxTableName: "outbox_test");
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new MySqlOutboxMigrationCatalog().All(config);

        //Assert — exactly seven migrations numbered 1..7 in order.
        Assert.Equal(7, migrations.Count);
        for (var i = 0; i < migrations.Count; i++)
        {
            Assert.Equal(i + 1, migrations[i].Version.Value);
        }

        //Assert — LogicalColumns at each version match the cumulative archaeology.
        for (var v = 1; v <= 7; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            Assert.True(
                expected.SetEquals(migration.LogicalColumns),
                $"V{v} LogicalColumns mismatch — " +
                $"expected: [{string.Join(", ", expected.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
                $"got: [{string.Join(", ", migration.LogicalColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
        }

        //Assert — V2..V7 UpScripts use the MySQL information_schema + prepared-statement
        //idempotency pattern (ADR §5). Targets MySQL 5.7+ which lacks native
        //ALTER TABLE ADD COLUMN IF NOT EXISTS — the prepared statement conditionally emits
        //the ALTER only when the column is absent.
        for (var v = 2; v <= 7; v++)
        {
            var script = migrations[v - 1].UpScript;
            Assert.Contains("information_schema.columns", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("PREPARE stmt FROM @q", script, StringComparison.OrdinalIgnoreCase);
        }

        //Assert — V1 has no single source commit; V2..V7 each carry archaeology pointers.
        Assert.Null(migrations[0].SourceReference);
        for (var v = 2; v <= 7; v++)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(migrations[v - 1].SourceReference),
                $"V{v} must have a non-empty SourceReference (archaeology pointer)");
        }

        //Assert — V4 PartitionKey on MySQL ships with the cross-backend commit
        //1cdc04b60 / PR #2560 (same as MSSQL/SQLite — distinct from Postgres's
        //cff67fd5e / #3464 which lagged the payload widening). This pins archaeology accuracy.
        Assert.Equal("1cdc04b60 / #2560", migrations[3].SourceReference);

        //Assert — IdempotencyCheckSql is null for MySQL (only SQLite uses that field per ADR §6).
        for (var v = 1; v <= 7; v++)
        {
            Assert.Null(migrations[v - 1].IdempotencyCheckSql);
        }
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

        return byVersion;
    }
}

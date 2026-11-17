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
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// The MSSQL inbox catalog lists V1 to V3 in order, each with the cumulative columns of the versions
/// before it; V2 and V3 carry a <c>SourceReference</c> and V1 does not.
/// </summary>
public class MsSqlInboxMigrationsTests
{
    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2Added = ["ContextKey"];

    private static readonly string[] s_v3Added = ["CausationId"];

    [Fact]
    public void When_mssql_inbox_migrations_are_listed_it_should_return_v1_through_v3_with_correct_logical_columns()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            inboxTableName: "inbox_test");
        var expectedPerVersion = BuildExpectedColumnsByVersion();

        //Act
        var migrations = new MsSqlInboxMigrationCatalog().All(config);

        //Assert
        Assert.Equal(3, migrations.Count);
        for (var i = 0; i < migrations.Count; i++)
        {
            Assert.Equal(i + 1, migrations[i].Version.Value);
        }

        for (var v = 1; v <= 3; v++)
        {
            var migration = migrations[v - 1];
            var expected = expectedPerVersion[v];
            Assert.True(
                expected.SetEquals(migration.LogicalColumns),
                $"V{v} LogicalColumns mismatch — " +
                $"expected: [{string.Join(", ", expected.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
                $"got: [{string.Join(", ", migration.LogicalColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
        }

        Assert.Null(migrations[0].SourceReference);
        Assert.False(
            string.IsNullOrWhiteSpace(migrations[1].SourceReference),
            "V2 must have a non-empty SourceReference (archaeology pointer for ContextKey addition)");
        Assert.False(
            string.IsNullOrWhiteSpace(migrations[2].SourceReference),
            "V3 must have a non-empty SourceReference (issue pointer for CausationId addition)");
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

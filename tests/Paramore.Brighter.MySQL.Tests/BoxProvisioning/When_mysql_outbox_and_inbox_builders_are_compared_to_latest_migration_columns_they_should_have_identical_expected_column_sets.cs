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
using Paramore.Brighter.BoxProvisioning.Tests.Drift;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.Drift;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlOutboxBuilderDriftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mysql_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
        bool hasBinaryMessagePayload)
    {
        //Arrange — drive the builder DDL and the V_latest LogicalColumns from the same config so
        //any developer change to one without the other surfaces here. MySQL identifiers are
        //quoted with `backticks`; QuoteStyle.MySql is case-insensitive.
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            MySqlOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload),
            QuoteStyle.MySql);

        //Act
        var migrations = new MySqlOutboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);
        migrationColumns.UnionWith(MySqlOutboxHousekeeping.V1);

        //Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest ∪ housekeeping: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }
}

public class MySqlInboxBuilderDriftTests
{
    [Fact]
    public void When_mysql_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
    {
        //Arrange — MySQL inbox has V1 + V2 (V2 added ContextKey). After Task 3.3 the migration
        //list should reach V_latest=2 and the live builder column set should match V2.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            inboxTableName: tableName);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            MySqlInboxBuilder.GetDDL(tableName),
            QuoteStyle.MySql);

        //Act
        var migrations = new MySqlInboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);
        migrationColumns.UnionWith(MySqlInboxHousekeeping.V1);

        //Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest ∪ housekeeping: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }
}

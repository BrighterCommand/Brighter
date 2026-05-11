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
using Paramore.Brighter.BoxProvisioning.Tests.Drift;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.BoxProvisioning.Drift;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class SqliteOutboxBuilderDriftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_sqlite_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
        bool hasBinaryMessagePayload)
    {
        //Arrange — drive the builder DDL and the V_latest LogicalColumns from the same config so
        //any developer change to one without the other surfaces here. SQLite identifiers are
        //wrapped in [brackets] in Brighter's builder DDL; QuoteStyle.Sqlite extraction is
        //case-insensitive and copes with inline COLLATE NOCASE on MessageId.
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            SqliteOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload),
            QuoteStyle.Sqlite);

        //Act
        var migrations = new SqliteOutboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);
        migrationColumns.UnionWith(SqliteOutboxHousekeeping.V1);

        //Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest ∪ housekeeping: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }
}

public class SqliteInboxBuilderDriftTests
{
    [Fact]
    public void When_sqlite_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
    {
        //Arrange — SQLite inbox has V1 + V2 (V2 adds ContextKey, mirroring MSSQL/MySQL). After
        //Task 4.3 the migration list should reach V_latest=2 and the live builder column set
        //should match V2. Today the builder ships ContextKey but the migration list is V1-only,
        //so this Fact is RED for Task 4.1 and goes GREEN with Task 4.3.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            inboxTableName: tableName);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            SqliteInboxBuilder.GetDDL(tableName),
            QuoteStyle.Sqlite);

        //Act
        var migrations = new SqliteInboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);
        migrationColumns.UnionWith(SqliteInboxHousekeeping.V1);

        //Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest ∪ housekeeping: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }
}

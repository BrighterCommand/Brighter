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

/// <summary>
/// <c>MySqlOutboxBuilder</c> stays in step with the migration chain: its columns are V_latest's logical
/// columns plus housekeeping, it emits the CausationId replay index, and it is the catalog's fresh-install
/// DDL; V1's UpScript stays the historical, pre-Dispatched DDL (ADR 0057 §1, §3).
/// </summary>
public class MySqlOutboxBuilderDriftTests
{
    private const string IgnoredConnectionString = "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mysql_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
        bool hasBinaryMessagePayload)
    {
        //Arrange
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            IgnoredConnectionString,
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mysql_outbox_v1_upscript_is_inspected_it_should_carry_pre_dispatched_historical_baseline(
        bool hasBinaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            IgnoredConnectionString,
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var migrations = new MySqlOutboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        Assert.Equal(1, v1.Version.Value);
        Assert.DoesNotContain("Dispatched", v1.UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HeaderBag", v1.UpScript, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mysql_outbox_fresh_install_ddl_is_inspected_it_should_match_live_builder(
        bool hasBinaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            IgnoredConnectionString,
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var expected = MySqlOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload);
        var actual = new MySqlOutboxMigrationCatalog().FreshInstallDdl(config);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mysql_outbox_builder_is_inspected_it_should_emit_the_causation_replay_index(
        bool hasBinaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var ddl = MySqlOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload);

        Assert.Contains("KEY `idx_CausationId`", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`CausationId`", ddl, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// The MySQL inbox catalog stays in step with <c>MySqlInboxBuilder</c>: V_latest's columns plus the
/// housekeeping columns equal the builder's, <c>FreshInstallDdl</c> is the builder's DDL, and V1 is
/// the pre-<c>ContextKey</c> historical baseline (ADR 0057 §3).
/// </summary>
public class MySqlInboxBuilderDriftTests
{
    private const string IgnoredConnectionString = "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;";

    [Fact]
    public void When_mysql_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
    {
        //Arrange
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            IgnoredConnectionString,
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

    [Fact]
    public void When_mysql_inbox_v1_upscript_is_inspected_it_should_carry_the_pre_contextkey_historical_baseline()
    {
        //Arrange
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            IgnoredConnectionString,
            inboxTableName: tableName);

        //Act
        var migrations = new MySqlInboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        //Assert
        Assert.Equal(1, v1.Version.Value);
        Assert.DoesNotContain("ContextKey", v1.UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CommandBody", v1.UpScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void When_mysql_inbox_fresh_install_ddl_is_inspected_it_should_match_live_builder()
    {
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            IgnoredConnectionString,
            inboxTableName: tableName);

        var expected = MySqlInboxBuilder.GetDDL(tableName, config.BinaryMessagePayload);
        var actual = new MySqlInboxMigrationCatalog().FreshInstallDdl(config);

        Assert.Equal(expected, actual);
    }
}

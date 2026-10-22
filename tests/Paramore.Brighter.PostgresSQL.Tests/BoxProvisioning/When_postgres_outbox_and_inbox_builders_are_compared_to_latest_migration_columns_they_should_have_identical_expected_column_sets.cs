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
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.BoxProvisioning.Tests.Drift;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.Drift;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// The live builder, FreshInstallDdl and V_latest LogicalColumns must not drift apart, and V1.UpScript stays the historical
/// first-shipped DDL (ADR 0057 §3). Columns compare case-insensitively, as the builders use PascalCase and LogicalColumns the
/// lowercase Postgres folds to. The builder also emits the causationid replay index.
/// </summary>
public class PostgreSqlOutboxBuilderDriftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_postgres_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
        bool binaryMessagePayload)
    {
        //Arrange
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            outBoxTableName: tableName,
            binaryMessagePayload: binaryMessagePayload);

        var builderColumns = new HashSet<string>(
            DdlColumnExtractor.GetExpectedColumns(
                PostgreSqlOutboxBuilder.GetDDL(tableName, binaryMessagePayload),
                QuoteStyle.Postgres),
            StringComparer.OrdinalIgnoreCase);

        //Act
        var migrations = new PostgreSqlOutboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);
        migrationColumns.UnionWith(PostgreSqlOutboxHousekeeping.V1);

        //Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest ∪ housekeeping: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_postgres_outbox_v1_upscript_is_inspected_it_should_carry_born_past_v1_historical_baseline_with_dispatched(
        bool binaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            outBoxTableName: tableName,
            binaryMessagePayload: binaryMessagePayload);

        var migrations = new PostgreSqlOutboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        Assert.Equal(1, v1.Version.Value);
        Assert.Contains("Dispatched", v1.UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HeaderBag", v1.UpScript, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_postgres_outbox_fresh_install_ddl_is_inspected_it_should_match_live_builder(
        bool binaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            outBoxTableName: tableName,
            binaryMessagePayload: binaryMessagePayload);

        var expected = PostgreSqlOutboxBuilder.GetDDL(tableName, binaryMessagePayload);
        var actual = new PostgreSqlOutboxMigrationCatalog().FreshInstallDdl(config);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_postgres_outbox_builder_is_inspected_it_should_emit_the_causation_replay_index(
        bool binaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var ddl = PostgreSqlOutboxBuilder.GetDDL(tableName, binaryMessagePayload);

        Assert.Contains("CREATE INDEX IF NOT EXISTS", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"idx_{tableName}_causationid", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(causationid)", ddl, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// The live builder, FreshInstallDdl and V_latest LogicalColumns must not drift apart, and V1.UpScript stays the historical
/// first-shipped DDL, which already carries ContextKey and the composite primary key (ADR 0057 §3).
/// </summary>
public class PostgreSqlInboxBuilderDriftTests
{
    [Fact]
    public void When_postgres_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
    {
        //Arrange
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            inboxTableName: tableName);

        var builderColumns = new HashSet<string>(
            DdlColumnExtractor.GetExpectedColumns(
                PostgreSqlInboxBuilder.GetDDL(tableName),
                QuoteStyle.Postgres),
            StringComparer.OrdinalIgnoreCase);

        //Act
        var migrations = new PostgreSqlInboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);
        migrationColumns.UnionWith(PostgreSqlInboxHousekeeping.V1);

        //Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest ∪ housekeeping: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }

    [Fact]
    public void When_postgres_inbox_v1_upscript_is_inspected_it_should_carry_born_past_v1_historical_baseline_with_contextkey_and_composite_pk()
    {
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            inboxTableName: tableName);

        var migrations = new PostgreSqlInboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        Assert.Equal(1, v1.Version.Value);
        Assert.Contains("ContextKey", v1.UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CommandBody", v1.UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "PRIMARY KEY (CommandId, ContextKey)",
            v1.UpScript,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void When_postgres_inbox_fresh_install_ddl_is_inspected_it_should_match_live_builder()
    {
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            inboxTableName: tableName);

        var expected = PostgreSqlInboxBuilder.GetDDL(tableName, config.BinaryMessagePayload);
        var actual = new PostgreSqlInboxMigrationCatalog().FreshInstallDdl(config);

        Assert.Equal(expected, actual);
    }
}

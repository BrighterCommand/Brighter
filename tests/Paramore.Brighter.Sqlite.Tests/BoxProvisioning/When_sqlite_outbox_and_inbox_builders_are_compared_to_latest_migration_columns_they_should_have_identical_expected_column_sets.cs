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
using System.Threading.Tasks;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

/// <summary>
/// The live builder, FreshInstallDdl and V_latest LogicalColumns must not drift apart, and V1.UpScript stays the
/// historical first-shipped DDL (ADR 0057 §3). The builder also emits the CausationId replay index.
/// </summary>
public class SqliteOutboxBuilderDriftTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_sqlite_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
        bool hasBinaryMessagePayload)
    {
        //Arrange
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
        await Assert.That(builderColumns.SetEquals(migrationColumns)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_sqlite_outbox_v1_upscript_is_inspected_it_should_carry_pre_dispatched_historical_baseline(
        bool hasBinaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var migrations = new SqliteOutboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        await Assert.That(v1.Version.Value).IsEqualTo(1);
        await Assert.That(v1.UpScript.Value).DoesNotContain("Dispatched");
        await Assert.That(v1.UpScript.Value).Contains("HeaderBag");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_sqlite_outbox_fresh_install_ddl_is_inspected_it_should_match_live_builder(
        bool hasBinaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var expected = SqliteOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload);
        var actual = new SqliteOutboxMigrationCatalog().FreshInstallDdl(config);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async System.Threading.Tasks.Task When_sqlite_outbox_builder_is_inspected_it_should_emit_the_causation_replay_index(
        bool hasBinaryMessagePayload)
    {
        const string tableName = "outbox_test";
        var ddl = SqliteOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload);

        await Assert.That(ddl).Contains("CREATE INDEX IF NOT EXISTS");
        await Assert.That(ddl).Contains($"idx_{tableName}_CausationId");
        await Assert.That(ddl).Contains("[CausationId]");
    }
}

/// <summary>
/// The live builder, FreshInstallDdl and V_latest LogicalColumns must not drift apart, and V1.UpScript stays the
/// historical first-shipped DDL, the baseline before ContextKey (ADR 0057 §3).
/// </summary>
public class SqliteInboxBuilderDriftTests
{
    [Test]
    public async Task When_sqlite_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
    {
        //Arrange
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
        await Assert.That(builderColumns.SetEquals(migrationColumns)).IsTrue();
    }

    [Test]
    public async Task When_sqlite_inbox_v1_upscript_is_inspected_it_should_carry_the_pre_contextkey_historical_baseline()
    {
        //Arrange
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            inboxTableName: tableName);

        //Act
        var migrations = new SqliteInboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        //Assert
        await Assert.That(v1.Version.Value).IsEqualTo(1);
        await Assert.That((v1.UpScript).Value).DoesNotContain("ContextKey");
        await Assert.That((v1.UpScript).Value).Contains("CommandBody");
    }

    [Test]
    public async Task When_sqlite_inbox_fresh_install_ddl_is_inspected_it_should_match_live_builder()
    {
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            inboxTableName: tableName);

        var expected = SqliteInboxBuilder.GetDDL(tableName, config.BinaryMessagePayload);
        var actual = new SqliteInboxMigrationCatalog().FreshInstallDdl(config);

        await Assert.That(actual).IsEqualTo(expected);
    }
}

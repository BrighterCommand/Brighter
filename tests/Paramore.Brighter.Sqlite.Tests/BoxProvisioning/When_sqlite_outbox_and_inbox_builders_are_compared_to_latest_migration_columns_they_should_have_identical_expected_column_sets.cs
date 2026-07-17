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

public class SqliteOutboxBuilderDriftTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_sqlite_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
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
        await Assert.That(builderColumns.SetEquals(migrationColumns)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_sqlite_outbox_v1_upscript_is_inspected_it_should_carry_pre_dispatched_historical_baseline(
        bool hasBinaryMessagePayload)
    {
        //V1.UpScript is the literal historical first-shipped DDL (Spec 0027 R1, commit
        //695522367, March 2019 — pre-Dispatched). The Dispatched column was introduced in V2
        //(3c30343fa, July 2019). This tripwire prevents a future "helpful" refactor from
        //quietly rewriting V1.UpScript back to live-builder shape: chain replay against a
        //legacy installation that bootstrapped at V1 must see the same starting DDL it always
        //saw. See spec 0027 README archaeology and ADR 0057 §3.
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
        //Spec 0027 R1 Part 1 contract: FreshInstallDdl on the catalog is the canonical source
        //for the fresh-install fast path (ADR 0057 §3), distinct from V1.UpScript which now
        //carries the historical baseline (Part 4). This tripwire holds the fast-path DDL
        //identical to the live builder so the two cannot diverge silently.
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var expected = SqliteOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload);
        var actual = new SqliteOutboxMigrationCatalog().FreshInstallDdl(config);

        await Assert.That(actual).IsEqualTo(expected);
    }
}

public class SqliteInboxBuilderDriftTests
{
    [Test]
    public async Task When_sqlite_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
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
        await Assert.That(builderColumns.SetEquals(migrationColumns)).IsTrue();
    }

    [Test]
    public async Task When_sqlite_inbox_v1_upscript_is_inspected_it_should_carry_born_past_v1_historical_baseline_with_contextkey()
    {
        //V1.UpScript is the literal historical first-shipped DDL (Spec 0027 R1, commit
        //695522367, March 2019). The SQLite inbox is one of the five "born past V1" backends:
        //it shipped with ContextKey from the first commit, so V1.UpScript already contains it
        //and V2's IdempotencyCheckSql (pragma_table_info probe) skips the ALTER on chain
        //replay. This tripwire prevents a future "helpful" rewrite from dropping ContextKey
        //out of V1. See spec 0027 README archaeology and ADR 0057 §3.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            inboxTableName: tableName);

        var migrations = new SqliteInboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        await Assert.That(v1.Version.Value).IsEqualTo(1);
        await Assert.That(v1.UpScript.Value).Contains("ContextKey");
        await Assert.That(v1.UpScript.Value).Contains("CommandBody");
    }

    [Test]
    public async Task When_sqlite_inbox_fresh_install_ddl_is_inspected_it_should_match_live_builder()
    {
        //Spec 0027 R1 Part 1 contract: FreshInstallDdl on the catalog is the canonical source
        //for the fresh-install fast path (ADR 0057 §3), distinct from V1.UpScript which now
        //carries the historical baseline (Part 4). This tripwire holds the fast-path DDL
        //identical to the live builder so the two cannot diverge silently.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Data Source=:memory:",
            inboxTableName: tableName);

        var expected = SqliteInboxBuilder.GetDDL(tableName, config.BinaryMessagePayload);
        var actual = new SqliteInboxMigrationCatalog().FreshInstallDdl(config);

        await Assert.That(actual).IsEqualTo(expected);
    }
}
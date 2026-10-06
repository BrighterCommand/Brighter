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

public class PostgreSqlOutboxBuilderDriftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_postgres_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
        bool binaryMessagePayload)
    {
        //Arrange — drive the builder DDL and the V_latest LogicalColumns from the same config so
        //any developer change to one without the other surfaces here. Postgres folds unquoted
        //identifiers to lowercase; QuoteStyle.Postgres extraction is case-sensitive.
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            outBoxTableName: tableName,
            binaryMessagePayload: binaryMessagePayload);

        //Postgres folds unquoted identifiers to lowercase at runtime. Brighter's builders use
        //mixed-case PascalCase identifiers today (issue #2850 tracks moving to snake_case
        //lowercase post-v10); migration LogicalColumns use lowercase to match information_schema
        //folding at runtime. Compare case-insensitively to bridge the two without hiding drift —
        //the test still fails if a column is added on one side and not the other.
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
        //V1.UpScript is the literal historical first-shipped DDL (Spec 0027 R1, commit
        //3c30343fa, July 2019). The Postgres outbox is one of the five "born past V1"
        //backends: it shipped with Dispatched from the first commit, so V1.UpScript already
        //contains it and V2's idempotency-guarded ALTER (ADD COLUMN IF NOT EXISTS) skips on
        //chain replay. This tripwire prevents a future "helpful" rewrite from dropping
        //Dispatched out of V1 (which would then re-apply the V2 ALTER and corrupt chain
        //replay against legacy tables). See spec 0027 README archaeology and ADR 0057 §3.
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
        //Spec 0027 R1 Part 1 contract: FreshInstallDdl on the catalog is the canonical source
        //for the fresh-install fast path (ADR 0057 §3), distinct from V1.UpScript which now
        //carries the historical baseline (Part 4). This tripwire holds the fast-path DDL
        //identical to the live builder so the two cannot diverge silently.
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
        //The drift test above compares columns only; the new causationid replay index (Spec 0027,
        //#2541) is asserted separately here per AC9. The builder appends a CREATE INDEX IF NOT
        //EXISTS after the CREATE TABLE so a fresh install lands the same index a V8 migration does.
        const string tableName = "outbox_test";
        var ddl = PostgreSqlOutboxBuilder.GetDDL(tableName, binaryMessagePayload);

        Assert.Contains("CREATE INDEX IF NOT EXISTS", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"idx_{tableName}_causationid", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(causationid)", ddl, StringComparison.OrdinalIgnoreCase);
    }
}

public class PostgreSqlInboxBuilderDriftTests
{
    [Fact]
    public void When_postgres_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
    {
        //Arrange — Postgres inbox V1 was born with ContextKey + composite PK (Feb 2021); Spec 0027
        //(#2541) adds a V2 that introduces CausationId. The builder must match the V_latest set.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            inboxTableName: tableName);

        //See outbox-arm comment: case-insensitive comparison matches Postgres's unquoted-folding semantics.
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
        //V1.UpScript is the literal historical first-shipped DDL (Spec 0027 R1, commit
        //1cdc04b60, November 2023). The Postgres inbox is one of the five "born past V1"
        //backends and is the most extreme case: it shipped with ContextKey AND a composite
        //primary key on (CommandId, ContextKey) from the first commit. There is no pre-ContextKey
        //Postgres inbox V1→V2 ContextKey catch-up (ADR "Alternatives → E" — the speculative
        //composite-PK rebuild migration was rejected because no pre-ContextKey Postgres inbox ever
        //shipped); Spec 0027's V2 instead adds CausationId. This tripwire asserts the V1 baseline
        //still carries the ContextKey column and composite PK and must not be rewritten away.
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
        //Spec 0027 R1 Part 1 contract: FreshInstallDdl on the catalog is the canonical source
        //for the fresh-install fast path (ADR 0057 §3), distinct from V1.UpScript which now
        //carries the historical baseline (Part 4). This tripwire holds the fast-path DDL
        //identical to the live builder so the two cannot diverge silently.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            inboxTableName: tableName);

        var expected = PostgreSqlInboxBuilder.GetDDL(tableName, config.BinaryMessagePayload);
        var actual = new PostgreSqlInboxMigrationCatalog().FreshInstallDdl(config);

        Assert.Equal(expected, actual);
    }
}

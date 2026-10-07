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
using Paramore.Brighter.BoxProvisioning.Tests.Drift;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.Drift;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlOutboxBuilderDriftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mssql_outbox_builder_is_compared_to_v8_migration_columns_it_should_have_identical_expected_column_set(
        bool hasBinaryMessagePayload)
    {
        //Arrange — drive the builder DDL and the V_latest LogicalColumns from the same config
        //so any developer change to one without the other surfaces here. Connection string is
        //unused: All() only reads OutBoxTableName and BinaryMessagePayload to render V1's UpScript.
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            SqlOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload),
            QuoteStyle.MsSql);

        //Act
        var migrations = new MsSqlOutboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);
        migrationColumns.UnionWith(MsSqlOutboxHousekeeping.V1);

        //Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest ∪ housekeeping: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mssql_outbox_v1_upscript_is_inspected_it_should_carry_pre_dispatched_historical_baseline(
        bool hasBinaryMessagePayload)
    {
        //V1.UpScript is the literal historical first-shipped DDL (Spec 0027 R1, commit
        //44a405b79, April 2019 — pre-Dispatched). The Dispatched column was introduced in V2
        //(3c30343fa, July 2019). This tripwire prevents a future "helpful" refactor from
        //quietly rewriting V1.UpScript back to live-builder shape: chain replay against a
        //legacy installation that bootstrapped at V1 must see the same starting DDL it always
        //saw. See spec 0027 README archaeology and ADR 0057 §3 for the historical/fresh-path
        //split. The discriminator column (HeaderBag) is also asserted to guarantee detection.
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var migrations = new MsSqlOutboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        Assert.Equal(1, v1.Version.Value);
        Assert.DoesNotContain("Dispatched", v1.UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HeaderBag", v1.UpScript, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mssql_outbox_builder_is_inspected_it_should_emit_the_causation_replay_index(
        bool hasBinaryMessagePayload)
    {
        //The drift test above compares columns only; the new CausationId replay index (Spec 0027,
        //#2541) is asserted separately here per AC9. The builder appends a CREATE INDEX after the
        //CREATE TABLE so a fresh install lands the same index a V8 migration upgrade does.
        const string tableName = "outbox_test";
        var ddl = SqlOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload);

        Assert.Contains("CREATE INDEX", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"idx_{tableName}_CausationId", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[CausationId]", ddl, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mssql_outbox_fresh_install_ddl_is_inspected_it_should_match_live_builder(
        bool hasBinaryMessagePayload)
    {
        //Spec 0027 R1 Part 1 contract: FreshInstallDdl on the catalog is the canonical source
        //for the fresh-install fast path (ADR 0057 §3), distinct from V1.UpScript which now
        //carries the historical baseline (Part 4). This tripwire holds the fast-path DDL
        //identical to the live builder so the two cannot diverge silently.
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            outBoxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var expected = SqlOutboxBuilder.GetDDL(tableName, hasBinaryMessagePayload);
        var actual = new MsSqlOutboxMigrationCatalog().FreshInstallDdl(config);

        Assert.Equal(expected, actual);
    }
}

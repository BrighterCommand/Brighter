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
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.Drift;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlInboxBuilderDriftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mssql_inbox_builder_is_compared_to_v3_migration_columns_it_should_have_identical_expected_column_set(
        bool hasBinaryMessagePayload)
    {
        //Arrange — drive the builder DDL and the V_latest LogicalColumns from the same config
        //so any developer change to one without the other surfaces here. Connection string is
        //unused: All() only reads InBoxTableName and BinaryMessagePayload to render V1's UpScript.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            inboxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            SqlInboxBuilder.GetDDL(tableName, hasBinaryMessagePayload),
            QuoteStyle.MsSql);

        //Act
        var migrations = new MsSqlInboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);
        migrationColumns.UnionWith(MsSqlInboxHousekeeping.V1);

        //Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest ∪ housekeeping: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mssql_inbox_v1_upscript_is_inspected_it_should_carry_born_past_v1_historical_baseline_with_contextkey(
        bool hasBinaryMessagePayload)
    {
        //V1.UpScript is the literal historical first-shipped DDL (Spec 0027 R1, commit
        //b7f96957b, March 2019). The MSSQL inbox is one of the five "born past V1" backends:
        //it shipped with ContextKey from the first commit, so V1.UpScript already contains it
        //and V2's idempotency-guarded ALTER (IF COL_LENGTH ... IS NULL) skips on chain replay.
        //This tripwire prevents a future "helpful" rewrite from dropping ContextKey out of V1
        //(which would then re-apply the V2 ALTER and corrupt chain replay against legacy
        //tables). See spec 0027 README archaeology and ADR 0057 §3.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            inboxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var migrations = new MsSqlInboxMigrationCatalog().All(config);
        var v1 = migrations[0];

        Assert.Equal(1, v1.Version.Value);
        Assert.Contains("ContextKey", v1.UpScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CommandBody", v1.UpScript, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_mssql_inbox_fresh_install_ddl_is_inspected_it_should_match_live_builder(
        bool hasBinaryMessagePayload)
    {
        //Spec 0027 R1 Part 1 contract: FreshInstallDdl on the catalog is the canonical source
        //for the fresh-install fast path (ADR 0057 §3), distinct from V1.UpScript which now
        //carries the historical baseline (Part 4). This tripwire holds the fast-path DDL
        //identical to the live builder so the two cannot diverge silently.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            inboxTableName: tableName,
            binaryMessagePayload: hasBinaryMessagePayload);

        var expected = SqlInboxBuilder.GetDDL(tableName, hasBinaryMessagePayload);
        var actual = new MsSqlInboxMigrationCatalog().FreshInstallDdl(config);

        Assert.Equal(expected, actual);
    }
}

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

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
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
using Paramore.Brighter.Inbox.Spanner;
using Paramore.Brighter.Outbox.Spanner;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

/// <summary>
/// <c>SpannerOutboxBuilder</c>'s columns equal V_latest's logical columns, taken from the MySQL catalog
/// (Spanner has no catalog of its own, ADR 0057 §6), with no housekeeping columns; and it emits the
/// <c>CausationId</c> replay index as a separate statement.
/// </summary>
[Trait("Category", "Spanner")]
[Collection("Spanner")]
public class SpannerOutboxBuilderDriftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_spanner_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
        bool binaryMessagePayload)
    {
        // Arrange
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            outBoxTableName: tableName,
            binaryMessagePayload: binaryMessagePayload);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            SpannerOutboxBuilder.GetDDL(tableName, binaryMessagePayload),
            QuoteStyle.Spanner);

        // Act
        var migrations = new MySqlOutboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);

        // Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest LogicalColumns: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }

    [Fact]
    public void When_spanner_outbox_builder_is_inspected_it_should_emit_the_causation_replay_index()
    {
        const string tableName = "outbox_test";
        var indexDdl = SpannerOutboxBuilder.GetCausationIndexDDL(tableName);

        Assert.Contains("CREATE INDEX IF NOT EXISTS", indexDdl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"idx_{tableName}_CausationId", indexDdl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`CausationId`", indexDdl, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// <c>SpannerInboxBuilder</c>'s columns equal V_latest's logical columns, taken from the MySQL catalog
/// (Spanner has no catalog of its own, ADR 0057 §6), with no housekeeping columns.
/// </summary>
[Trait("Category", "Spanner")]
[Collection("Spanner")]
public class SpannerInboxBuilderDriftTests
{
    [Fact]
    public void When_spanner_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
    {
        // Arrange
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            inboxTableName: tableName);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            SpannerInboxBuilder.GetDDL(tableName),
            QuoteStyle.Spanner);

        // Act
        var migrations = new MySqlInboxMigrationCatalog().All(config);
        var migrationColumns = new HashSet<string>(
            migrations[migrations.Count - 1].LogicalColumns,
            StringComparer.OrdinalIgnoreCase);

        // Assert
        Assert.True(
            builderColumns.SetEquals(migrationColumns),
            $"Builder columns: [{string.Join(", ", builderColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}], " +
            $"V_latest LogicalColumns: [{string.Join(", ", migrationColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))}]");
    }
}

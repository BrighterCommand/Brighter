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

// Item #9 (PR #4039 third review). MSSQL/MySQL/PostgreSQL/SQLite each have a builder-vs-V_latest
// drift test that parses the live builder DDL via DdlColumnExtractor and asserts the column set
// equals the V_latest LogicalColumns plus per-backend housekeeping. Spanner had no such guard
// even though the same drift class is possible: a developer can add a column to
// SpannerOutboxBuilder / SpannerInboxBuilder without bumping VLatestOutbox / VLatestInbox or
// the relational catalogs' V_latest LogicalColumns, and the bug would not surface until a real
// Spanner deploy detected the schema mismatch.
//
// Spanner is fresh-install-only (ADR 0057 §6) so it has no per-Spanner migration catalog of its
// own. We compare against the MySQL catalog because (a) MySQL uses the same identifier-quoting
// convention as Spanner — backticks, case-insensitive — so QuoteStyle.Spanner and the MySQL
// V_latest LogicalColumns line up without case-folding gymnastics, and (b) Item #8's drift test
// already pins MySQL .All(cfg).Count == VLatestOutbox / VLatestInbox, so MySQL is the canonical
// reference for "what columns are expected at V_latest across the whole relational tier".
//
// Spanner has NO housekeeping union — the relational backends each add an extra `id` or
// `CreatedID` column, but Spanner's PRIMARY KEY clause lives OUTSIDE the column body (Spanner
// declares the PK after the closing paren) so the column set is the logical model exactly.

[Trait("Category", "Spanner")]
public class SpannerOutboxBuilderDriftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_spanner_outbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set(
        bool binaryMessagePayload)
    {
        // Arrange — drive the builder DDL and the V_latest LogicalColumns from the same config so
        // any developer change to one without the other surfaces here. Spanner uses GoogleSQL
        // backticks; QuoteStyle.Spanner is case-insensitive.
        const string tableName = "outbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;Uid=ignored;Pwd=ignored;",
            outBoxTableName: tableName,
            binaryMessagePayload: binaryMessagePayload);

        var builderColumns = DdlColumnExtractor.GetExpectedColumns(
            SpannerOutboxBuilder.GetDDL(tableName, binaryMessagePayload),
            QuoteStyle.Spanner);

        // Act — MySQL outbox V7 LogicalColumns is the canonical V_latest set (the four relational
        // outbox catalogs agree at V7 per Item #8's drift test).
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
}

[Trait("Category", "Spanner")]
public class SpannerInboxBuilderDriftTests
{
    [Fact]
    public void When_spanner_inbox_builder_is_compared_to_v_latest_migration_columns_it_should_have_identical_expected_column_set()
    {
        // Arrange — Spanner inbox is fresh-install-only at V2-equivalent: the builder ships
        // CommandId + CommandType + CommandBody + Timestamp + ContextKey, which matches the
        // MySQL inbox V2 LogicalColumns (V1 + ContextKey). PostgreSQL inbox would also work
        // (its V1-only ContextKey-inclusive shape is the same five columns), but MySQL is the
        // canonical V_latest reference for the four-backend invariant.
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

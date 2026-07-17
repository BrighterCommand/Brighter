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

using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.BoxProvisioning.Spanner;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

// Item #8 (PR #4039 third review). SpannerBoxMigrationRunner stores VLatestOutbox / VLatestInbox
// as bare constants because Spanner is fresh-install-only (ADR 0057 §6) — no V_k migration chain
// runs against Spanner, so the latest version is just a stamp. The constants MUST track the
// relational chain length: every relational backend ships the same outbox migrations V1..Vn and
// the same inbox migrations V1..Vm, and Spanner's history row needs to record V_latest equal to
// `n` (outbox) and `m` (inbox).
//
// Without this drift guard, a relational backend bump (e.g. all four catalogs going to V8) could
// land without bumping Spanner, leaving Spanner's history row stamped at V7 while Spanner's
// freshly-installed table actually carries V8's schema. Detection-by-history would then report
// "needs migration V8" forever on Spanner installs.

[Trait("Category", "Spanner")]
public class SpannerVLatestDriftAgainstRelationalCatalogTests
{
    // Catalog .All(cfg) does not touch the database — it only emits SQL strings keyed off the
    // configured table name. Any non-empty, identifier-safe name works for the drift comparison.
    private static readonly RelationalDatabaseConfiguration Configuration =
        new(
            connectionString: "Server=localhost",
            outBoxTableName: "drift_test_outbox",
            inboxTableName: "drift_test_inbox");

    [Fact]
    public void Vlatest_outbox_should_equal_every_relational_outbox_catalog_count()
    {
        // Asserts each backend separately so a drift on a single backend surfaces in the
        // failure message rather than collapsing into a generic "counts disagreed" assertion.
        Assert.Equal(SpannerBoxMigrationRunner.VLatestOutbox, new MsSqlOutboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestOutbox, new MySqlOutboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestOutbox, new PostgreSqlOutboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestOutbox, new SqliteOutboxMigrationCatalog().All(Configuration).Count);
    }

    [Fact]
    public void Vlatest_inbox_should_equal_every_relational_inbox_catalog_count_except_postgres()
    {
        // PostgreSQL inbox is V1-only by design (ADR 0057 §E) — its V1 already includes the
        // ContextKey column that MSSQL/MySQL/Sqlite add at V2, so its catalog returns 1
        // while the other three return 2. Pin that asymmetry here as a known carve-out: if
        // PostgreSQL gains a V2 (e.g. an unrelated future column add), this constant flips
        // from 1 to 2 and the assertion fails — at that point bump VLatestInbox AND fold
        // PostgreSQL back into the main set.
        Assert.Equal(SpannerBoxMigrationRunner.VLatestInbox, new MsSqlInboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestInbox, new MySqlInboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestInbox, new SqliteInboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(1, new PostgreSqlInboxMigrationCatalog().All(Configuration).Count);
    }
}

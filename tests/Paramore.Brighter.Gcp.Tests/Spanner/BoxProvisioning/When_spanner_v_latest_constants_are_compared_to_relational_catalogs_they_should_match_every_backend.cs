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

/// <summary>
/// <c>SpannerBoxMigrationRunner.VLatestOutbox</c> and <c>VLatestInbox</c> equal every relational catalog's
/// length, so Spanner stamps the version its fresh-install schema really has (ADR 0057 §6). The PostgreSQL
/// inbox, born with <c>ContextKey</c>, is one version behind the others (ADR 0057 §1).
/// </summary>
[Trait("Category", "Spanner")]
[Collection("Spanner")]
public class SpannerVLatestDriftAgainstRelationalCatalogTests
{
    private const int PostgreSqlInboxLatest = 2;

    // All(cfg) only builds SQL strings, so no database is needed.
    private static readonly RelationalDatabaseConfiguration Configuration =
        new(
            connectionString: "Server=localhost",
            outBoxTableName: "drift_test_outbox",
            inboxTableName: "drift_test_inbox");

    [Fact]
    public void Vlatest_outbox_should_equal_every_relational_outbox_catalog_count()
    {
        Assert.Equal(SpannerBoxMigrationRunner.VLatestOutbox, new MsSqlOutboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestOutbox, new MySqlOutboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestOutbox, new PostgreSqlOutboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestOutbox, new SqliteOutboxMigrationCatalog().All(Configuration).Count);
    }

    [Fact]
    public void Vlatest_inbox_should_equal_every_relational_inbox_catalog_count_except_postgres()
    {
        Assert.Equal(SpannerBoxMigrationRunner.VLatestInbox, new MsSqlInboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestInbox, new MySqlInboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(SpannerBoxMigrationRunner.VLatestInbox, new SqliteInboxMigrationCatalog().All(Configuration).Count);
        Assert.Equal(PostgreSqlInboxLatest, new PostgreSqlInboxMigrationCatalog().All(Configuration).Count);
    }
}

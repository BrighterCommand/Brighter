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
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Spanner;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

/// <summary>
/// <c>SpannerBoxMigrationRunner.MigrateAsync</c> rejects an unsafe table name with a
/// <see cref="ConfigurationException"/> at entry, before any DDL or probe interpolates it; Spanner has no
/// catalog, so the runner is the only guard (ADR 0057 §6; ADR 0058, Guards and contracts). No connection is opened.
/// </summary>
[Trait("Category", "Spanner")]
[Collection("Spanner")]
public class SpannerRunnerUnsafeIdentifierTests
{
    private const string IgnoredConnectionString = "Data Source=ignored;";

    [Theory]
    [InlineData("O'Brien")]    // single quote — would break inlined predicates in information_schema probe
    [InlineData("1Outbox")]    // leading digit — invalid as bare identifier
    [InlineData("my-outbox")]  // hyphen — would need backtick-quoting to be legal
    public async Task When_spanner_runner_migrates_an_outbox_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(IgnoredConnectionString);
        var runner = new SpannerBoxMigrationRunner(config);
        var tableState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act + Assert
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => runner.MigrateAsync(
            unsafeTable, schemaName: null, BoxType.Outbox, tableState));
        Assert.Contains(unsafeTable, ex.Message);
    }

    [Theory]
    [InlineData("O'Brien")]
    [InlineData("1Inbox")]
    [InlineData("my-inbox")]
    public async Task When_spanner_runner_migrates_an_inbox_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(IgnoredConnectionString);
        var runner = new SpannerBoxMigrationRunner(config);
        var tableState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act + Assert
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => runner.MigrateAsync(
            unsafeTable, schemaName: null, BoxType.Inbox, tableState));
        Assert.Contains(unsafeTable, ex.Message);
    }
}

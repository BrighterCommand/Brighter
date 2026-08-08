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
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.BoxProvisioning.Spanner;
using Paramore.Brighter.Outbox.Spanner;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

[Trait("Category", "Spanner")]
[Collection("SpannerBoxProvisioning")]
[Trait("Category", "Spanner")]
public class SpannerConcurrentBootstrapTests : IAsyncLifetime
{
    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly RelationalDatabaseConfiguration _config;

    public SpannerConcurrentBootstrapTests()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";
        _connectionString = Const.ConnectionString;

        _config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
    }

    [Fact]
    public async Task Should_not_throw_when_two_concurrent_bootstrap_callers_race_on_history_insert()
    {
        //Arrange — pre-create a Brighter-shaped outbox table without any history rows
        //(simulates a spec-0023-era install). Both provisioners will detect
        //TableExists=true / HistoryExists=false and route to BootstrapExistingTableAsync.
        //Both will pass the discriminator gate (HeaderBag is present in V1Columns), both
        //will call IsMigrationAppliedAsync(V_latest=7) which returns false because no
        //history row exists yet (and Spanner uses CommitTimestamp for AppliedAt so the
        //winner's row is invisible to the loser's read snapshot until commit), then both
        //will attempt InsertHistoryRowAsync — the loser hits a Spanner AlreadyExists on
        //the PK (BoxTableName, MigrationVersion) which currently surfaces as an
        //unrecoverable startup error.
        using (var setupConnection = CreateConnection())
        {
            await setupConnection.OpenAsync();
            var ddl = setupConnection.CreateDdlCommand(
                SpannerOutboxBuilder.GetDDL(_tableName, _config.BinaryMessagePayload));
            await ddl.ExecuteNonQueryAsync();
        }

        var provisionerA = new SpannerOutboxProvisioner(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            _config,
            new SpannerBoxMigrationRunner(_config));
        var provisionerB = new SpannerOutboxProvisioner(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            _config,
            new SpannerBoxMigrationRunner(_config));

        //Act
        var act = async () => await Task.WhenAll(
            provisionerA.ProvisionAsync(),
            provisionerB.ProvisionAsync());
        var ex = await Record.ExceptionAsync(act);

        //Assert — neither replica should surface the benign-race AlreadyExists.
        Assert.Null(ex);

        //Assert — exactly one bootstrap row was stamped at V_latest with the bootstrap
        //description (proves the loser's insert was absorbed cleanly, not silently
        //double-stamped, and that this was the bootstrap path — not the fresh-install
        //path covered by P-fresh).
        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var historyQuery = connection.CreateSelectCommand(
            @"SELECT `Description` FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, _tableName },
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.OutboxLatest }
            });

        using var reader = await historyQuery.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "expected exactly one history row at V_latest");
        var description = reader.GetString(0);
        Assert.StartsWith("bootstrap: spanner-assumed-current", description);
        Assert.False(await reader.ReadAsync(), "expected no second history row at V_latest");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            var dropTable = connection.CreateDdlCommand($"DROP TABLE IF EXISTS `{_tableName}`");
            await dropTable.ExecuteNonQueryAsync();

            using var deleteHistory = connection.CreateDmlCommand(
                @"DELETE FROM `BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName",
                new SpannerParameterCollection
                {
                    { "BoxTableName", SpannerDbType.String, _tableName }
                });
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private SpannerConnection CreateConnection()
    {
        var builder = new SpannerConnectionStringBuilder(_connectionString)
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        };
        return new SpannerConnection(builder);
    }
}

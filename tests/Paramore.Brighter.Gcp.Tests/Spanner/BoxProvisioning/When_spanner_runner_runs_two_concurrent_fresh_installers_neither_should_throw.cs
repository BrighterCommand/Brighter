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
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

[Trait("Category", "Spanner")]
[Collection("SpannerBoxProvisioning")]
[Trait("Category", "Spanner")]
public class SpannerConcurrentFreshInstallTests : IAsyncLifetime
{
    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly RelationalDatabaseConfiguration _config;

    public SpannerConcurrentFreshInstallTests()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";
        _connectionString = Const.ConnectionString;

        _config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
    }

    [Fact]
    public async Task Should_not_throw_when_two_concurrent_fresh_installers_race_on_history_insert()
    {
        //Arrange — two independent provisioners against the same empty table. Both will
        //take the fresh-install path; both will run CREATE TABLE (Spanner's DDL is serialised
        //internally — ExecuteCreateTableIfNotExistsSafeAsync swallows the loser's AlreadyExists), then both
        //will check IsMigrationAppliedAsync (which can read at a snapshot before the winner's
        //history-row commit timestamp lands), and both will attempt InsertHistoryRowAsync —
        //the loser hits a Spanner AlreadyExists on the PK (BoxTableName, MigrationVersion)
        //which currently surfaces as an unrecoverable startup error.
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

        //Assert — exactly one history row was stamped at V_latest (proves the loser's
        //insert was absorbed cleanly, not silently double-stamped).
        using var connection = CreateConnection();
        await connection.OpenAsync();
        var historyCount = await ReadHistoryCountAsync(connection, _tableName);
        Assert.Equal(1, historyCount);
    }

    private static async Task<long> ReadHistoryCountAsync(
        SpannerConnection connection, string tableName)
    {
        using var command = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.OutboxLatest }
            });
        return (long)(await command.ExecuteScalarAsync())!;
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

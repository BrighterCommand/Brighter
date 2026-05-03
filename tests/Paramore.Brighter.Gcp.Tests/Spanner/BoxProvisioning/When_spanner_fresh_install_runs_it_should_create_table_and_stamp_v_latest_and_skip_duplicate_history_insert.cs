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

[Collection("SpannerBoxProvisioning")]
public class SpannerOutboxFreshInstallTests : IAsyncLifetime
{
    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerOutboxProvisioner _provisioner;

    public SpannerOutboxFreshInstallTests()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";
        _connectionString = Const.ConnectionString;

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new SpannerBoxMigrationRunner(config);
        _provisioner = new SpannerOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_stamp_v_latest_with_fresh_install_description_and_skip_duplicate_on_rerun()
    {
        // Act — first provision on absent table (fresh-install path)
        await _provisioner.ProvisionAsync();

        // Assert — single history row at V_latest with description starting with "fresh install"
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var (firstCount, firstDescription) = await ReadHistoryAsync(connection, _tableName);
        Assert.Equal(1, firstCount);
        Assert.NotNull(firstDescription);
        Assert.StartsWith("fresh install", firstDescription, StringComparison.OrdinalIgnoreCase);

        // Act — second provision must be a no-op (verifies IsMigrationAppliedAsync gate / detection bypass)
        await _provisioner.ProvisionAsync();

        // Assert — still exactly one history row at V_latest, description preserved
        var (secondCount, secondDescription) = await ReadHistoryAsync(connection, _tableName);
        Assert.Equal(1, secondCount);
        Assert.Equal(firstDescription, secondDescription);
    }

    private static async Task<(long Count, string? Description)> ReadHistoryAsync(
        SpannerConnection connection, string tableName)
    {
        using var countCommand = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.OutboxLatest }
            });
        var count = (long)(await countCommand.ExecuteScalarAsync())!;

        using var descCommand = connection.CreateSelectCommand(
            @"SELECT `Description` FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.OutboxLatest }
            });
        var description = (string?)await descCommand.ExecuteScalarAsync();

        return (count, description);
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
        }
        catch { }
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

[Collection("SpannerBoxProvisioning")]
public class SpannerInboxFreshInstallTests : IAsyncLifetime
{
    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerInboxProvisioner _provisioner;

    public SpannerInboxFreshInstallTests()
    {
        _tableName = $"test_inbox_{Guid.NewGuid():N}";
        _connectionString = Const.ConnectionString;

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _tableName);
        var runner = new SpannerBoxMigrationRunner(config);
        _provisioner = new SpannerInboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_stamp_v_latest_with_fresh_install_description_and_skip_duplicate_on_rerun()
    {
        // Act — first provision on absent table (fresh-install path)
        await _provisioner.ProvisionAsync();

        // Assert — single history row at V_latest with description starting with "fresh install"
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var (firstCount, firstDescription) = await ReadHistoryAsync(connection, _tableName);
        Assert.Equal(1, firstCount);
        Assert.NotNull(firstDescription);
        Assert.StartsWith("fresh install", firstDescription, StringComparison.OrdinalIgnoreCase);

        // Act — second provision must be a no-op (verifies IsMigrationAppliedAsync gate / detection bypass)
        await _provisioner.ProvisionAsync();

        // Assert — still exactly one history row at V_latest, description preserved
        var (secondCount, secondDescription) = await ReadHistoryAsync(connection, _tableName);
        Assert.Equal(1, secondCount);
        Assert.Equal(firstDescription, secondDescription);
    }

    private static async Task<(long Count, string? Description)> ReadHistoryAsync(
        SpannerConnection connection, string tableName)
    {
        using var countCommand = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.InboxLatest }
            });
        var count = (long)(await countCommand.ExecuteScalarAsync())!;

        using var descCommand = connection.CreateSelectCommand(
            @"SELECT `Description` FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.InboxLatest }
            });
        var description = (string?)await descCommand.ExecuteScalarAsync();

        return (count, description);
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
        }
        catch { }
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

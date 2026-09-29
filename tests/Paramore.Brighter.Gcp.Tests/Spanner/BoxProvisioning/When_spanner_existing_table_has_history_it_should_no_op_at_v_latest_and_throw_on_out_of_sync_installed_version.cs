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
using Paramore.Brighter.Inbox.Spanner;
using Paramore.Brighter.Outbox.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

/// <summary>
/// A Spanner outbox whose history is at V_latest is left alone, history preserved; history above V_latest
/// throws, because the build and the database are out of sync (ADR 0057 §6).
/// </summary>
[Property("Category", "Spanner")]
[NotInParallel]
[Property("Category", "Spanner")]
public class SpannerOutboxNormalPathTests
{
    private const string SeededDescription = "spec 0023 fresh install";

    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerOutboxProvisioner _provisioner;

    public SpannerOutboxNormalPathTests()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";
        _connectionString = Const.ConnectionString;

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new SpannerBoxMigrationRunner(config);
        _provisioner = new SpannerOutboxProvisioner(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task Should_be_a_no_op_when_history_max_equals_v_latest()
    {
        // Arrange
        await CreateBoxAndSeedHistoryAsync(
            ExpectedMigrationVersions.OutboxLatest, SeededDescription);

        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var (totalCount, description) = await ReadHistoryAsync(
            connection, _tableName, ExpectedMigrationVersions.OutboxLatest);
        var totalAcrossVersions = await ReadTotalRowCountAsync(connection, _tableName);

        await Assert.That(totalCount).IsEqualTo(1);
        await Assert.That(description).IsEqualTo(SeededDescription);
        await Assert.That(totalAcrossVersions).IsEqualTo(1);
    }

    [Test]
    public async Task Should_throw_configuration_exception_when_history_max_exceeds_v_latest()
    {
        // Arrange
        await CreateBoxAndSeedHistoryAsync(99, "speculative future migration");

        // Act / Assert
        var exception = await Assert.That(() => _provisioner.ProvisionAsync()).ThrowsExactly<ConfigurationException>();

        await Assert.That(exception.Message).Contains("migration list out of sync");
    }

    private async Task CreateBoxAndSeedHistoryAsync(int seedVersion, string description)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var createBox = connection.CreateDdlCommand(SpannerOutboxBuilder.GetDDL(_tableName));
        await createBox.ExecuteNonQueryAsync();

        var createHistory = connection.CreateDdlCommand(
            @"CREATE TABLE IF NOT EXISTS `BrighterMigrationHistory` (
                `MigrationVersion` INT64 NOT NULL,
                `BoxTableName` STRING(255) NOT NULL,
                `Description` STRING(MAX) NOT NULL,
                `AppliedAt` TIMESTAMP NOT NULL OPTIONS (allow_commit_timestamp = true)
            ) PRIMARY KEY (`BoxTableName`, `MigrationVersion`)");
        await createHistory.ExecuteNonQueryAsync();

        using var insert = connection.CreateInsertCommand(
            "BrighterMigrationHistory",
            new SpannerParameterCollection
            {
                { "MigrationVersion", SpannerDbType.Int64, (long)seedVersion },
                { "BoxTableName", SpannerDbType.String, _tableName },
                { "Description", SpannerDbType.String, description },
                { "AppliedAt", SpannerDbType.Timestamp, SpannerParameter.CommitTimestamp }
            });
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task<(long Count, string? Description)> ReadHistoryAsync(
        SpannerConnection connection, string tableName, int version)
    {
        using var countCommand = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @Version",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "Version", SpannerDbType.Int64, (long)version }
            });
        var count = (long)(await countCommand.ExecuteScalarAsync())!;

        using var descCommand = connection.CreateSelectCommand(
            @"SELECT `Description` FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @Version",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "Version", SpannerDbType.Int64, (long)version }
            });
        var description = (string?)await descCommand.ExecuteScalarAsync();

        return (count, description);
    }

    private static async Task<long> ReadTotalRowCountAsync(
        SpannerConnection connection, string tableName)
    {
        using var command = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName }
            });
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Before(HookType.Test)]

    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var deleteHistory = connection.CreateDmlCommand(
                "DELETE FROM `BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName",
                new SpannerParameterCollection
                {
                    { "BoxTableName", SpannerDbType.String, _tableName }
                });
            await deleteHistory.ExecuteNonQueryAsync();

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

/// <summary>
/// A Spanner inbox whose history is at V_latest is left alone, history preserved; history above V_latest
/// throws, because the build and the database are out of sync (ADR 0057 §6).
/// </summary>
[Property("Category", "Spanner")]
[NotInParallel("Spanner")]
[Property("Category", "Spanner")]
public class SpannerInboxNormalPathTests
{
    private const string SeededDescription = "spec 0023 fresh install";

    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerInboxProvisioner _provisioner;

    public SpannerInboxNormalPathTests()
    {
        _tableName = $"test_inbox_{Guid.NewGuid():N}";
        _connectionString = Const.ConnectionString;

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _tableName);
        var runner = new SpannerBoxMigrationRunner(config);
        _provisioner = new SpannerInboxProvisioner(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task Should_be_a_no_op_when_history_max_equals_v_latest()
    {
        // Arrange
        await CreateBoxAndSeedHistoryAsync(
            ExpectedMigrationVersions.InboxLatest, SeededDescription);

        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var (totalCount, description) = await ReadHistoryAsync(
            connection, _tableName, ExpectedMigrationVersions.InboxLatest);
        var totalAcrossVersions = await ReadTotalRowCountAsync(connection, _tableName);

        await Assert.That(totalCount).IsEqualTo(1);
        await Assert.That(description).IsEqualTo(SeededDescription);
        await Assert.That(totalAcrossVersions).IsEqualTo(1);
    }

    [Test]
    public async Task Should_throw_configuration_exception_when_history_max_exceeds_v_latest()
    {
        // Arrange
        await CreateBoxAndSeedHistoryAsync(99, "speculative future migration");

        // Act / Assert
        var exception = await Assert.That(() => _provisioner.ProvisionAsync()).ThrowsExactly<ConfigurationException>();

        await Assert.That(exception.Message).Contains("migration list out of sync");
    }

    private async Task CreateBoxAndSeedHistoryAsync(int seedVersion, string description)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var createBox = connection.CreateDdlCommand(SpannerInboxBuilder.GetDDL(_tableName));
        await createBox.ExecuteNonQueryAsync();

        var createHistory = connection.CreateDdlCommand(
            @"CREATE TABLE IF NOT EXISTS `BrighterMigrationHistory` (
                `MigrationVersion` INT64 NOT NULL,
                `BoxTableName` STRING(255) NOT NULL,
                `Description` STRING(MAX) NOT NULL,
                `AppliedAt` TIMESTAMP NOT NULL OPTIONS (allow_commit_timestamp = true)
            ) PRIMARY KEY (`BoxTableName`, `MigrationVersion`)");
        await createHistory.ExecuteNonQueryAsync();

        using var insert = connection.CreateInsertCommand(
            "BrighterMigrationHistory",
            new SpannerParameterCollection
            {
                { "MigrationVersion", SpannerDbType.Int64, (long)seedVersion },
                { "BoxTableName", SpannerDbType.String, _tableName },
                { "Description", SpannerDbType.String, description },
                { "AppliedAt", SpannerDbType.Timestamp, SpannerParameter.CommitTimestamp }
            });
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task<(long Count, string? Description)> ReadHistoryAsync(
        SpannerConnection connection, string tableName, int version)
    {
        using var countCommand = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @Version",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "Version", SpannerDbType.Int64, (long)version }
            });
        var count = (long)(await countCommand.ExecuteScalarAsync())!;

        using var descCommand = connection.CreateSelectCommand(
            @"SELECT `Description` FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @Version",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "Version", SpannerDbType.Int64, (long)version }
            });
        var description = (string?)await descCommand.ExecuteScalarAsync();

        return (count, description);
    }

    private static async Task<long> ReadTotalRowCountAsync(
        SpannerConnection connection, string tableName)
    {
        using var command = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName }
            });
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Before(HookType.Test)]

    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var deleteHistory = connection.CreateDmlCommand(
                "DELETE FROM `BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName",
                new SpannerParameterCollection
                {
                    { "BoxTableName", SpannerDbType.String, _tableName }
                });
            await deleteHistory.ExecuteNonQueryAsync();

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

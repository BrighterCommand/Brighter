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

[Property("Category", "Spanner")]
[NotInParallel]
[Property("Category", "Spanner")]
public class SpannerOutboxBootstrapDiscriminatorTests
{
    private const string BootstrapDescription =
        "bootstrap: spanner-assumed-current (no known legacy installations, A-2)";

    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerOutboxProvisioner _provisioner;

    public SpannerOutboxBootstrapDiscriminatorTests()
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
    public async Task Should_stamp_v_latest_with_bootstrap_description_when_headerbag_present()
    {
        // Arrange — a Brighter-shaped outbox already exists (HeaderBag present), no history yet
        using (var setup = CreateConnection())
        {
            await setup.OpenAsync();
            var ddl = setup.CreateDdlCommand(SpannerOutboxBuilder.GetDDL(_tableName));
            await ddl.ExecuteNonQueryAsync();
        }

        // Act
        await _provisioner.ProvisionAsync();

        // Assert — exactly one history row at V_latest, description matches ADR §6 verbatim
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var (count, description) = await ReadHistoryAsync(
            connection, _tableName, ExpectedMigrationVersions.OutboxLatest);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(description).IsEqualTo(BootstrapDescription);
    }

    [Test]
    public async Task Should_throw_configuration_exception_when_headerbag_absent()
    {
        // Arrange — a foreign table without the outbox discriminator HeaderBag
        using (var setup = CreateConnection())
        {
            await setup.OpenAsync();
            var ddl = setup.CreateDdlCommand(
                $"CREATE TABLE `{_tableName}` (Id INT64 NOT NULL, OtherCol STRING(MAX)) PRIMARY KEY (Id)");
            await ddl.ExecuteNonQueryAsync();
        }

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => _provisioner.ProvisionAsync());

        await Assert.That(exception.Message).Contains("not a Brighter outbox");
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

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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

[Property("Category", "Spanner")]
[NotInParallel]
[Property("Category", "Spanner")]
public class SpannerInboxBootstrapDiscriminatorTests
{
    private const string BootstrapDescription =
        "bootstrap: spanner-assumed-current (no known legacy installations, A-2)";

    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerInboxProvisioner _provisioner;

    public SpannerInboxBootstrapDiscriminatorTests()
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
    public async Task Should_stamp_v_latest_with_bootstrap_description_when_commandbody_present()
    {
        // Arrange — a Brighter-shaped inbox already exists (CommandBody present), no history yet
        using (var setup = CreateConnection())
        {
            await setup.OpenAsync();
            var ddl = setup.CreateDdlCommand(SpannerInboxBuilder.GetDDL(_tableName));
            await ddl.ExecuteNonQueryAsync();
        }

        // Act
        await _provisioner.ProvisionAsync();

        // Assert — exactly one history row at V_latest, description matches ADR §6 verbatim
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var (count, description) = await ReadHistoryAsync(
            connection, _tableName, ExpectedMigrationVersions.InboxLatest);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(description).IsEqualTo(BootstrapDescription);
    }

    [Test]
    public async Task Should_throw_configuration_exception_when_commandbody_absent()
    {
        // Arrange — a foreign table without the inbox discriminator CommandBody
        using (var setup = CreateConnection())
        {
            await setup.OpenAsync();
            var ddl = setup.CreateDdlCommand(
                $"CREATE TABLE `{_tableName}` (Id INT64 NOT NULL, OtherCol STRING(MAX)) PRIMARY KEY (Id)");
            await ddl.ExecuteNonQueryAsync();
        }

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => _provisioner.ProvisionAsync());

        await Assert.That(exception.Message).Contains("not a Brighter inbox");
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

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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

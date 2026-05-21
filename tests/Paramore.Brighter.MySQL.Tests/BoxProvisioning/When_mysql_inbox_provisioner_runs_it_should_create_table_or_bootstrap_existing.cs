using System;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.Inbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class InboxProvisionerTests : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _freshTableName;
    private readonly string _existingTableName;

    public InboxProvisionerTests()
    {
        _freshTableName = $"test_inbox_{Guid.NewGuid():N}";
        _existingTableName = $"test_inbox_{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Should_create_inbox_table_on_fresh_database()
    {
        // Arrange
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _freshTableName);
        var runner = new MySqlBoxMigrationRunner(new MySqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new MySqlInboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlInboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            runner);

        // Act
        await provisioner.ProvisionAsync();

        // Assert
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT EXISTS(SELECT 1 FROM information_schema.tables
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName)";
        tableCheck.Parameters.AddWithValue("@TableName", _freshTableName);
        var tableExists = Convert.ToBoolean(await tableCheck.ExecuteScalarAsync());
        Assert.True(tableExists);

        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _freshTableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.InboxLatest);
        var historyCount = (long)(await historyCheck.ExecuteScalarAsync())!;
        Assert.Equal(1, historyCount);
    }

    [Fact]
    public async Task Should_bootstrap_existing_table_without_history()
    {
        // Arrange — create inbox table directly (simulating pre-migration install)
        using (var setupConn = new MySqlConnection(_connectionString))
        {
            await setupConn.OpenAsync();
            using var ddl = setupConn.CreateCommand();
            ddl.CommandText = MySqlInboxBuilder.GetDDL(_existingTableName);
            await ddl.ExecuteNonQueryAsync();
        }

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _existingTableName);
        var runner = new MySqlBoxMigrationRunner(new MySqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new MySqlInboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlInboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            runner);

        // Act
        await provisioner.ProvisionAsync();

        // Assert
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // History should have a synthetic row at the latest inbox version
        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _existingTableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.InboxLatest);
        var historyCount = (long)(await historyCheck.ExecuteScalarAsync())!;
        Assert.Equal(1, historyCount);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd1 = connection.CreateCommand();
            cmd1.CommandText = $"DROP TABLE IF EXISTS `{_freshTableName}`";
            await cmd1.ExecuteNonQueryAsync();

            using var cmd2 = connection.CreateCommand();
            cmd2.CommandText = $"DROP TABLE IF EXISTS `{_existingTableName}`";
            await cmd2.ExecuteNonQueryAsync();
        }
        catch { }
    }
}

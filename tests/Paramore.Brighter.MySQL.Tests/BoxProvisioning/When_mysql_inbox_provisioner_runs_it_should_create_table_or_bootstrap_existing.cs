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
    public async Task When_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table()
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

        Assert.True(await TableExistsAsync(connection, _freshTableName));

        Assert.Equal(1, await CountHistoryRowsAtLatestVersionAsync(connection, _freshTableName));
    }

    [Fact]
    public async Task When_inbox_provisioner_runs_against_existing_table_without_history_it_should_bootstrap_existing()
    {
        // Arrange
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

        Assert.Equal(1, await CountHistoryRowsAtLatestVersionAsync(connection, _existingTableName));
    }

    private static async Task<bool> TableExistsAsync(MySqlConnection connection, string tableName)
    {
        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT EXISTS(SELECT 1 FROM information_schema.tables
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName)";
        tableCheck.Parameters.AddWithValue("@TableName", tableName);
        return Convert.ToBoolean(await tableCheck.ExecuteScalarAsync());
    }

    private static async Task<long> CountHistoryRowsAtLatestVersionAsync(MySqlConnection connection, string tableName)
    {
        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", tableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.InboxLatest);
        return (long)(await historyCheck.ExecuteScalarAsync())!;
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

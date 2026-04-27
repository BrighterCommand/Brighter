using System;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class OutboxProvisionerFreshDatabaseTests : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName;
    private readonly MySqlOutboxProvisioner _provisioner;

    public OutboxProvisionerFreshDatabaseTests()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new MySqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        _provisioner = new MySqlOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_create_outbox_table()
    {
        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Verify table exists
        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT EXISTS(SELECT 1 FROM information_schema.tables
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName)";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        var tableExists = Convert.ToBoolean(await tableCheck.ExecuteScalarAsync());
        Assert.True(tableExists);

        // Verify migration history
        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _tableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.OutboxLatest);
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
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS `{_tableName}`";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}

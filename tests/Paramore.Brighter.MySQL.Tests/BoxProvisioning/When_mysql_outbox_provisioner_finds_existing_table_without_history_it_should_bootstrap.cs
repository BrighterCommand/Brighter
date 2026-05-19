using System;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class When_mysql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName;
    private readonly MySqlOutboxProvisioner _provisioner;

    public When_mysql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new MySqlBoxMigrationRunner(new MySqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new MySqlOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_bootstrap_with_synthetic_history()
    {
        // Arrange — create outbox table directly (simulating pre-migration install)
        using (var setupConn = new MySqlConnection(_connectionString))
        {
            await setupConn.OpenAsync();
            using var ddl = setupConn.CreateCommand();
            ddl.CommandText = MySqlOutboxBuilder.GetDDL(_tableName);
            await ddl.ExecuteNonQueryAsync();
        }

        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

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

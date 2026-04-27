using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class OutboxProvisionerFreshDatabaseTests : IAsyncLifetime
{
    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SqliteOutboxProvisioner _provisioner;

    public OutboxProvisionerFreshDatabaseTests()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";
        _connectionString = Configuration.ConnectionString;

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new SqliteBoxMigrationRunner(config);
        _provisioner = new SqliteOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_create_outbox_table()
    {
        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Verify table exists
        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@TableName";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        var tableCount = Convert.ToInt64(await tableCheck.ExecuteScalarAsync());
        Assert.Equal(1, tableCount);

        // Verify migration history
        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [MigrationVersion] = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _tableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.OutboxLatest);
        var historyCount = Convert.ToInt64(await historyCheck.ExecuteScalarAsync());
        Assert.Equal(1, historyCount);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}

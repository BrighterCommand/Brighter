using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class OutboxProvisionerFreshDatabaseTests
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
        var runner = new SqliteBoxMigrationRunner(new SqliteOutboxMigrationCatalog(), config);
        _provisioner = new SqliteOutboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteOutboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task When_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table()
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
        await Assert.That(tableCount).IsEqualTo(1);

        // Verify migration history
        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [MigrationVersion] = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _tableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.OutboxLatest);
        var historyCount = Convert.ToInt64(await historyCheck.ExecuteScalarAsync());
        await Assert.That(historyCount).IsEqualTo(1);
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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
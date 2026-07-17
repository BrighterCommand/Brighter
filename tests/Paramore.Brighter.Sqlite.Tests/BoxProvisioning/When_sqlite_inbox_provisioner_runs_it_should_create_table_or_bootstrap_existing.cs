using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Inbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class InboxProvisionerTests
{
    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly string _freshTableName;
    private readonly string _existingTableName;

    public InboxProvisionerTests()
    {
        _freshTableName = $"test_inbox_{Guid.NewGuid():N}";
        _existingTableName = $"test_inbox_{Guid.NewGuid():N}";
    }

    [Test]
    public async Task When_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table()
    {
        // Arrange
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _freshTableName);
        var runner = new SqliteBoxMigrationRunner(new SqliteInboxMigrationCatalog(), config);
        var provisioner = new SqliteInboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteInboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);

        // Act
        await provisioner.ProvisionAsync();

        // Assert
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@TableName";
        tableCheck.Parameters.AddWithValue("@TableName", _freshTableName);
        var tableCount = Convert.ToInt64(await tableCheck.ExecuteScalarAsync());
        await Assert.That(tableCount).IsEqualTo(1);

        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [MigrationVersion] = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _freshTableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.InboxLatest);
        var historyCount = Convert.ToInt64(await historyCheck.ExecuteScalarAsync());
        await Assert.That(historyCount).IsEqualTo(1);
    }

    [Test]
    public async Task When_inbox_provisioner_runs_against_existing_table_without_history_it_should_bootstrap_existing()
    {
        // Arrange — create inbox table directly (simulating pre-migration install)
        using (var setupConn = new SqliteConnection(_connectionString))
        {
            await setupConn.OpenAsync();
            using var walCmd = setupConn.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            await walCmd.ExecuteNonQueryAsync();

            using var ddl = setupConn.CreateCommand();
            ddl.CommandText = SqliteInboxBuilder.GetDDL(_existingTableName);
            await ddl.ExecuteNonQueryAsync();
        }

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _existingTableName);
        var runner = new SqliteBoxMigrationRunner(new SqliteInboxMigrationCatalog(), config);
        var provisioner = new SqliteInboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteInboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);

        // Act
        await provisioner.ProvisionAsync();

        // Assert
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [MigrationVersion] = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _existingTableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.InboxLatest);
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

            using var cmd1 = connection.CreateCommand();
            cmd1.CommandText = $"DROP TABLE IF EXISTS [{_freshTableName}]";
            await cmd1.ExecuteNonQueryAsync();

            using var cmd2 = connection.CreateCommand();
            cmd2.CommandText = $"DROP TABLE IF EXISTS [{_existingTableName}]";
            await cmd2.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
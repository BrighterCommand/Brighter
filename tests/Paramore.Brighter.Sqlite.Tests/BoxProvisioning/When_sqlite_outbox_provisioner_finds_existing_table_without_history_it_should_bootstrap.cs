using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class OutboxProvisionerBootstrapTests
{
    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly string _tableName;
    private readonly SqliteOutboxProvisioner _provisioner;

    public OutboxProvisionerBootstrapTests()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";

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
    public async Task When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()
    {
        // Arrange — create outbox table directly (simulating pre-migration install)
        using (var setupConn = new SqliteConnection(_connectionString))
        {
            await setupConn.OpenAsync();
            using var walCmd = setupConn.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            await walCmd.ExecuteNonQueryAsync();

            using var ddl = setupConn.CreateCommand();
            ddl.CommandText = SqliteOutboxBuilder.GetDDL(_tableName);
            await ddl.ExecuteNonQueryAsync();
        }

        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

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
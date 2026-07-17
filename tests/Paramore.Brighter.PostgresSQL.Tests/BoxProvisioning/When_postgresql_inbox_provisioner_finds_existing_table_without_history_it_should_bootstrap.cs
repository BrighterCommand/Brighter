using System;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Inbox.Postgres;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlInboxProvisionerBootstrapTests
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName;
    private readonly PostgreSqlInboxProvisioner _provisioner;

    public PostgreSqlInboxProvisionerBootstrapTests()
    {
        _tableName = $"test_inbox_{Guid.NewGuid():N}";

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _tableName);
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new PostgreSqlInboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlInboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task When_postgresql_inbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();

        var ddl = PostgreSqlInboxBuilder.GetDDL(_tableName);
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = ddl;
            await command.ExecuteNonQueryAsync();
        }

        //Act
        await _provisioner.ProvisionAsync();

        //Assert
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        using var historyCheck = conn.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public' AND ""MigrationVersion"" = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _tableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.InboxLatest);
        var historyCount = (long)(await historyCheck.ExecuteScalarAsync())!;
        await Assert.That(historyCount).IsEqualTo(1);
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS \"{_tableName}\"";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
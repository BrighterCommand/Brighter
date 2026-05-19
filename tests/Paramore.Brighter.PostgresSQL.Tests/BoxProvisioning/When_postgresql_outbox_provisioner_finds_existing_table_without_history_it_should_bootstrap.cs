using System;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_postgresql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName;
    private readonly PostgreSqlOutboxProvisioner _provisioner;

    public When_postgresql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new PostgreSqlOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_bootstrap_with_synthetic_history()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();

        var ddl = PostgreSqlOutboxBuilder.GetDDL(_tableName);
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
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.OutboxLatest);
        var historyCount = (long)(await historyCheck.ExecuteScalarAsync())!;
        Assert.Equal(1, historyCount);
    }

    public Task InitializeAsync() => Task.CompletedTask;

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

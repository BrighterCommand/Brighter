using System;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_postgresql_outbox_provisioner_runs_on_already_provisioned_database_it_should_be_idempotent : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName;
    private readonly PostgreSqlOutboxProvisioner _provisioner;

    public When_postgresql_outbox_provisioner_runs_on_already_provisioned_database_it_should_be_idempotent()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        _provisioner = new PostgreSqlOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_be_idempotent()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();
        await _provisioner.ProvisionAsync();

        //Act
        await _provisioner.ProvisionAsync();

        //Assert
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public' AND ""MigrationVersion"" = 1";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _tableName);
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

using System;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_multiple_postgresql_provisioners_run_concurrently_they_should_not_corrupt_state : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName;

    public When_multiple_postgresql_provisioners_run_concurrently_they_should_not_corrupt_state()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Should_not_corrupt_state()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);

        var provisioner1 = new PostgreSqlOutboxProvisioner(
            config, new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30)));
        var provisioner2 = new PostgreSqlOutboxProvisioner(
            config, new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30)));

        //Act
        await Task.WhenAll(
            provisioner1.ProvisionAsync(),
            provisioner2.ProvisionAsync());

        //Assert
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'public' AND TABLE_NAME = @TableName)";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        var tableExists = (bool)(await tableCheck.ExecuteScalarAsync())!;
        Assert.True(tableExists);

        using var historyCheck = connection.CreateCommand();
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

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly MsSqlOutboxProvisioner _provisioner;

    public When_mssql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()
    {
        var builder = new ConfigurationBuilder().AddEnvironmentVariables();
        var configuration = builder.Build();
        _connectionString = configuration.GetSection("Sql")["TestsBrighterConnectionString"]
            ?? "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";

        _tableName = $"test_outbox_{Guid.NewGuid():N}";

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new MsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        _provisioner = new MsSqlOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_bootstrap_with_synthetic_history()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        // Create table directly via builder (simulating pre-migration install)
        var ddl = SqlOutboxBuilder.GetDDL(_tableName);
        Configuration.CreateTable(_connectionString, ddl);

        //Act
        await _provisioner.ProvisionAsync();

        //Assert
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        // Verify migration history has synthetic version 1
        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo' AND [MigrationVersion] = 1";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _tableName);
        var historyCount = (int)historyCheck.ExecuteScalar()!;
        Assert.Equal(1, historyCount);

        // Verify the outbox table still exists (wasn't recreated)
        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT COUNT(1) FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = @TableName AND s.name = 'dbo'";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        var tableCount = (int)tableCheck.ExecuteScalar()!;
        Assert.Equal(1, tableCount);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            command.ExecuteNonQuery();
        }
        catch { }
    }
}

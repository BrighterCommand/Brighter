using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Inbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlInboxProvisionerBootstrapTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly MsSqlInboxProvisioner _provisioner;

    public MsSqlInboxProvisionerBootstrapTests()
    {
        var builder = new ConfigurationBuilder().AddEnvironmentVariables();
        var configuration = builder.Build();
        _connectionString = configuration.GetSection("Sql")["TestsBrighterConnectionString"]
            ?? "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";

        _tableName = $"test_inbox_{Guid.NewGuid():N}";

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _tableName);
        var runner = new MsSqlBoxMigrationRunner(new MsSqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new MsSqlInboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlInboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Fact]
    public async Task When_mssql_inbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        CreatePreMigrationInboxTable();

        //Act
        await _provisioner.ProvisionAsync();

        //Assert
        Assert.Equal(1, GetHistoryRowCountAtInboxLatest());
        Assert.Equal(1, GetInboxTableCountInDbo());
    }

    private void CreatePreMigrationInboxTable() =>
        Configuration.CreateTable(_connectionString, SqlInboxBuilder.GetDDL(_tableName));

    private int GetHistoryRowCountAtInboxLatest()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo' AND [MigrationVersion] = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _tableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.InboxLatest);
        return (int)historyCheck.ExecuteScalar()!;
    }

    private int GetInboxTableCountInDbo()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT COUNT(1) FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = @TableName AND s.name = 'dbo'";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        return (int)tableCheck.ExecuteScalar()!;
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

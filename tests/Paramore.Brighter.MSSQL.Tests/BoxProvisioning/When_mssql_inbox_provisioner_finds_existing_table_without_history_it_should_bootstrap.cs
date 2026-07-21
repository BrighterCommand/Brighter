using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Inbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlInboxProvisionerBootstrapTests
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

    [Test]
    public async Task When_mssql_inbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        // Create table directly via builder (simulating pre-migration install)
        var ddl = SqlInboxBuilder.GetDDL(_tableName);
        Configuration.CreateTable(_connectionString, ddl);

        //Act
        await _provisioner.ProvisionAsync();

        //Assert
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var historyCheck = connection.CreateCommand();
        historyCheck.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo' AND [MigrationVersion] = @ExpectedVersion";
        historyCheck.Parameters.AddWithValue("@BoxTableName", _tableName);
        historyCheck.Parameters.AddWithValue("@ExpectedVersion", ExpectedMigrationVersions.InboxLatest);
        var historyCount = (int)historyCheck.ExecuteScalar()!;
        await Assert.That(historyCount).IsEqualTo(1);

        // Verify the inbox table still exists (wasn't recreated)
        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT COUNT(1) FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = @TableName AND s.name = 'dbo'";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        var tableCount = (int)tableCheck.ExecuteScalar()!;
        await Assert.That(tableCount).IsEqualTo(1);
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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
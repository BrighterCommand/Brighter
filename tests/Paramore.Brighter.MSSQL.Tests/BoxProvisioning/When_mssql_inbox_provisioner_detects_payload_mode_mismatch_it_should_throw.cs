using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Inbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_inbox_provisioner_detects_payload_mode_mismatch_it_should_throw : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public When_mssql_inbox_provisioner_detects_payload_mode_mismatch_it_should_throw()
    {
        var builder = new ConfigurationBuilder().AddEnvironmentVariables();
        var configuration = builder.Build();
        _connectionString = configuration.GetSection("Sql")["TestsBrighterConnectionString"]
            ?? "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";

        _tableName = $"test_inbox_{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Should_throw_configuration_exception()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        // Create a text-mode inbox table
        var ddl = SqlInboxBuilder.GetDDL(_tableName, binaryMessagePayload: false);
        Configuration.CreateTable(_connectionString, ddl);

        // Configure provisioner with binary mode (mismatch)
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _tableName,
            binaryMessagePayload: true);
        var runner = new MsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        var provisioner = new MsSqlInboxProvisioner(config, runner);

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(
            () => provisioner.ProvisionAsync());

        Assert.Contains("mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
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

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlOutboxPayloadModeMismatchTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public MsSqlOutboxPayloadModeMismatchTests()
    {
        var builder = new ConfigurationBuilder().AddEnvironmentVariables();
        var configuration = builder.Build();
        _connectionString = configuration.GetSection("Sql")["TestsBrighterConnectionString"]
            ?? "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";

        _tableName = $"test_outbox_{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task When_mssql_outbox_provisioner_detects_payload_mode_mismatch_it_should_throw()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        // Create a text-mode outbox table
        var ddl = SqlOutboxBuilder.GetDDL(_tableName, hasBinaryMessagePayload: false);
        Configuration.CreateTable(_connectionString, ddl);

        // Configure provisioner with binary mode (mismatch)
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            binaryMessagePayload: true);
        var runner = new MsSqlBoxMigrationRunner(new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);

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

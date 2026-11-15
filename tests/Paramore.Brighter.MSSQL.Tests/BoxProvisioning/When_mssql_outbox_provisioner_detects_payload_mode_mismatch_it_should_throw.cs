using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlOutboxPayloadModeMismatchTests
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

    [Test]
    public async Task When_mssql_outbox_provisioner_detects_payload_mode_mismatch_it_should_throw()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        var ddl = SqlOutboxBuilder.GetDDL(_tableName, hasBinaryMessagePayload: false);
        Configuration.CreateTable(_connectionString, ddl);

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
        var exception = await Assert.That(() => provisioner.ProvisionAsync()).ThrowsExactly<ConfigurationException>();

        await Assert.That(exception.Message).Contains("mismatch");
    }

    [Before(HookType.Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]
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

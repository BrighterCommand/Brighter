using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlConnectionNameResolutionTests
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public MsSqlConnectionNameResolutionTests()
    {
        var builder = new ConfigurationBuilder().AddEnvironmentVariables();
        var configuration = builder.Build();
        _connectionString = configuration.GetSection("Sql")["TestsBrighterConnectionString"]
            ?? "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";

        _tableName = $"test_outbox_{Guid.NewGuid():N}";
    }

    [Test]
    public async Task When_mssql_box_provisioning_uses_connection_name_it_should_resolve_from_configuration()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        var configData = new System.Collections.Generic.Dictionary<string, string?>
        {
            ["ConnectionStrings:BrighterDb"] = _connectionString
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        // Use the extension via a stub builder to wire up DI properly
        var stubBuilder = new StubBrighterBuilder(services);
        stubBuilder.UseBoxProvisioning(opts =>
        {
            opts.AddMsSqlOutbox("BrighterDb", outboxTableName: _tableName);
        });

        var provider = services.BuildServiceProvider();
        var provisioners = provider.GetServices<IAmABoxProvisioner>().ToList();

        //Act
        await Assert.That(provisioners).HasSingleItem();
        await provisioners[0].ProvisionAsync();

        //Assert
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

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
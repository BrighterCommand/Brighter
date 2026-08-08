using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlInboxConnectionNameResolutionTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName;

    public PostgreSqlInboxConnectionNameResolutionTests()
    {
        _tableName = $"test_inbox_{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task When_postgres_box_provisioning_uses_connection_name_for_inbox_it_should_resolve_from_configuration()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();

        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:BrighterDb"] = _connectionString
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        var stubBuilder = new StubBrighterBuilder(services);
        stubBuilder.UseBoxProvisioning(opts =>
        {
            opts.AddPostgreSqlInbox("BrighterDb", inboxTableName: _tableName);
        });

        var provider = services.BuildServiceProvider();
        var provisioners = provider.GetServices<IAmABoxProvisioner>().ToList();

        //Act
        Assert.Single(provisioners);
        await provisioners[0].ProvisionAsync();

        //Assert
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT COUNT(*) FROM information_schema.tables
WHERE table_name = @TableName AND table_schema = 'public'";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        var tableCount = (long)(await tableCheck.ExecuteScalarAsync())!;
        Assert.Equal(1, tableCount);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS public.\"{_tableName}\"";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}

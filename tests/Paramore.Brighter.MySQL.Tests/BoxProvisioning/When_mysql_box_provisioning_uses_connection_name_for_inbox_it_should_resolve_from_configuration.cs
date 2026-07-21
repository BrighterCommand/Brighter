using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlInboxConnectionNameResolutionTests
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName;

    public MySqlInboxConnectionNameResolutionTests()
    {
        _tableName = $"test_inbox_{Guid.NewGuid():N}";
    }

    [Test]
    public async Task When_mysql_box_provisioning_uses_connection_name_for_inbox_it_should_resolve_from_configuration()
    {
        //Arrange
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
            opts.AddMySqlInbox("BrighterDb", inboxTableName: _tableName);
        });

        var provider = services.BuildServiceProvider();
        var provisioners = provider.GetServices<IAmABoxProvisioner>().ToList();

        //Act
        await Assert.That(provisioners).HasSingleItem();
        await provisioners[0].ProvisionAsync();

        //Assert
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = @"
SELECT COUNT(*) FROM information_schema.tables
WHERE table_name = @TableName AND table_schema = DATABASE()";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        var tableCount = Convert.ToInt64(await tableCheck.ExecuteScalarAsync());
        await Assert.That(tableCount).IsEqualTo(1);
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS `{_tableName}`";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
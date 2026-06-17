using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class InboxConnectionNameResolutionTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly string _tableName;

    public InboxConnectionNameResolutionTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"brighter_test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
        _tableName = $"test_inbox_{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task When_sqlite_box_provisioning_uses_connection_name_for_inbox_it_should_resolve_from_configuration()
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
            opts.AddSqliteInbox("BrighterDb", inboxTableName: _tableName);
        });

        var provider = services.BuildServiceProvider();
        var provisioners = provider.GetServices<IAmABoxProvisioner>().ToList();

        //Act
        Assert.Single(provisioners);
        await provisioners[0].ProvisionAsync();

        //Assert
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @TableName";
        tableCheck.Parameters.AddWithValue("@TableName", _tableName);
        var tableCount = Convert.ToInt64(await tableCheck.ExecuteScalarAsync());
        Assert.Equal(1, tableCount);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = $"DROP TABLE IF EXISTS \"{_tableName}\"";
                await command.ExecuteNonQueryAsync();
            }
            SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlTrippedTopicsProvisionerTests : IAsyncLifetime
{
    private const int TRIP_TABLE_VERSION = 1;
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_trips_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_mysql_tripped_topics_provisioner_runs_on_fresh_database_it_should_create_the_trip_table()
    {
        //Arrange
        var provisioner = ProvisionerFor();

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.Equal(BoxType.TrippedTopics, provisioner.BoxType);
        Assert.Equal(_tableName, provisioner.BoxTableName.Value);
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(1) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{_tableName}'"));
        Assert.Equal(["EndedAt", "PriorTripTime", "Topic", "TripTime"],
            (await ColumnsOfAsync()).OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal(1, await HistoryRowCountAsync());
    }

    [Fact]
    public async Task When_mysql_tripped_topics_provisioner_runs_twice_it_should_be_idempotent()
    {
        //Arrange
        var provisioner = ProvisionerFor();
        await provisioner.ProvisionAsync();

        //Act
        var exception = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert
        Assert.Null(exception);
        Assert.Equal(1, await HistoryRowCountAsync());
    }

    [Fact]
    public async Task When_mysql_tripped_topics_provisioner_finds_a_hand_created_table_it_should_bootstrap()
    {
        //Arrange
        await ExecuteNonQueryAsync(MySqlTrippedTopicStore.GetDDL(_tableName));
        var provisioner = ProvisionerFor();

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.Equal(1, await HistoryRowCountAsync());
    }

    private IAmABoxProvisioner ProvisionerFor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new RelationalDatabaseConfiguration(_connectionString);

        new StubBrighterBuilder(services).UseBoxProvisioning(options =>
            options.AddMySqlTrippedTopics(configuration, _tableName));

        return services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single();
    }

    private async Task<long> HistoryRowCountAsync() => await ScalarAsync($@"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = '{_tableName}' AND `MigrationVersion` = {TRIP_TABLE_VERSION}");

    private async Task<List<string>> ColumnsOfAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '{_tableName}'";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
        return columns;
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await ExecuteNonQueryAsync($"DROP TABLE IF EXISTS `{_tableName}`");
        }
        catch
        {
            // Best effort cleanup
        }
    }
}

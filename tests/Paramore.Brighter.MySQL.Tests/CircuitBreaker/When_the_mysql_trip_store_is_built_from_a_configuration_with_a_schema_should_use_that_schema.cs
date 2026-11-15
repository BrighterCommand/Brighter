using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreConfigurationSchemaTests : IAsyncLifetime
{
    private const string SCHEMA = "billing_for_store_test";

    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_trips_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_the_mysql_trip_store_is_built_from_a_configuration_with_a_schema_should_use_that_schema()
    {
        // Arrange
        var configuration = new RelationalDatabaseConfiguration(_connectionString, schemaName: SCHEMA);
        var services = new ServiceCollection();
        services.AddLogging();
        new StubBrighterBuilder(services).UseBoxProvisioning(options => options.AddMySqlTrippedTopics(configuration, _tableName));
        await services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single().ProvisionAsync();
        var store = new MySqlTrippedTopicStore(configuration, new MySqlConnectionProvider(configuration), _tableName);
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");

        // Act
        await store.WriteAsync([new TopicTrip(topic, new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero))]);

        // Assert
        Assert.Equal(1L, await RowCountAsync(topic));
    }

    public Task InitializeAsync() => ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{SCHEMA}`");

    public Task DisposeAsync() => ExecuteAsync($"DROP TABLE IF EXISTS `{SCHEMA}`.`{_tableName}`");

    private async Task<long> RowCountAsync(RoutingKey topic)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM `{SCHEMA}`.`{_tableName}` WHERE Topic = @topic";
        command.Parameters.AddWithValue("@topic", topic.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

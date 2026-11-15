using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreConfigurationSchemaTests : IAsyncLifetime
{
    private const string SCHEMA = "billing_for_store_test";

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_trips_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_the_postgresql_trip_store_is_built_from_a_configuration_with_a_schema_should_use_that_schema()
    {
        // Arrange
        var configuration = new RelationalDatabaseConfiguration(_connectionString, schemaName: SCHEMA);
        var services = new ServiceCollection();
        services.AddLogging();
        new StubBrighterBuilder(services).UseBoxProvisioning(options => options.AddPostgreSqlTrippedTopics(configuration, _tableName));
        await services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single().ProvisionAsync();
        var store = new PostgreSqlTrippedTopicStore(configuration, new PostgreSqlConnectionProvider(configuration), _tableName);
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");

        // Act
        await store.WriteAsync([new TopicTrip(topic, new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero))]);

        // Assert
        Assert.Equal(1, await RowCountAsync(topic));
    }

    public Task InitializeAsync()
    {
        new PostgresSqlTestHelper().SetupDatabase();
        return ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{SCHEMA}\"");
    }

    public Task DisposeAsync() => ExecuteAsync($"DROP TABLE IF EXISTS \"{SCHEMA}\".\"{_tableName}\"");

    private async Task<long> RowCountAsync(RoutingKey topic)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{SCHEMA}\".\"{_tableName}\" WHERE topic = @topic";
        command.Parameters.AddWithValue("@topic", topic.Value);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

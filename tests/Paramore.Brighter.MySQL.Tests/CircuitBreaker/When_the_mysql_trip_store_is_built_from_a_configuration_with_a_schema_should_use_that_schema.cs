#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

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

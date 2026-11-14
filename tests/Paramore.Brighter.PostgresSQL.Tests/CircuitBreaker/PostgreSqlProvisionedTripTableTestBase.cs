using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public abstract class PostgreSqlProvisionedTripTableTestBase : IAsyncLifetime
{
    protected string ConnectionString { get; } = PostgreSqlSettings.TestsBrighterConnectionString;
    protected string TableName { get; } = $"test_trips_{Guid.NewGuid():N}";

    protected IAmATrippedTopicStore NewStore() =>
        new PostgreSqlTrippedTopicStore(new PostgreSqlConnectionProvider(new RelationalDatabaseConfiguration(ConnectionString)), TableName);

    protected async Task ProvisionTripTableAsync()
    {
        new PostgresSqlTestHelper().SetupDatabase();
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new RelationalDatabaseConfiguration(ConnectionString);
        new StubBrighterBuilder(services).UseBoxProvisioning(options =>
            options.AddPostgreSqlTrippedTopics(configuration, TableName));

        await services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single().ProvisionAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS \"public\".\"{TableName}\"";
        await command.ExecuteNonQueryAsync();
    }
}

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

public abstract class MySqlProvisionedTripTableTestBase : IAsyncLifetime
{
    protected string ConnectionString { get; } = Const.DefaultConnectingString;
    protected string TableName { get; } = $"test_trips_{Guid.NewGuid():N}";

    protected IAmATrippedTopicStore NewStore() =>
        new MySqlTrippedTopicStore(new MySqlConnectionProvider(new RelationalDatabaseConfiguration(ConnectionString)), TableName);

    protected async Task ProvisionTripTableAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new RelationalDatabaseConfiguration(ConnectionString);
        new StubBrighterBuilder(services).UseBoxProvisioning(options =>
            options.AddMySqlTrippedTopics(configuration, TableName));

        await services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single().ProvisionAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS `{TableName}`";
        await command.ExecuteNonQueryAsync();
    }
}

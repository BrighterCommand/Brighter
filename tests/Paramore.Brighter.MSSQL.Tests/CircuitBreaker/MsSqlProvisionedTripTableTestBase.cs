using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public abstract class MsSqlProvisionedTripTableTestBase : IAsyncLifetime
{
    protected string ConnectionString { get; } = Configuration.DefaultConnectingString;
    protected string TableName { get; } = $"test_trips_{Guid.NewGuid():N}";

    protected IAmATrippedTopicStore NewStore() =>
        new MsSqlTrippedTopicStore(new MsSqlConnectionProvider(new RelationalDatabaseConfiguration(ConnectionString)), TableName);

    protected async Task ProvisionTripTableAsync()
    {
        Configuration.EnsureDatabaseExists(ConnectionString);
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new RelationalDatabaseConfiguration(ConnectionString);
        new StubBrighterBuilder(services).UseBoxProvisioning(options =>
            options.AddMsSqlTrippedTopics(configuration, TableName));

        await services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single().ProvisionAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS [dbo].[{TableName}]";
        await command.ExecuteNonQueryAsync();
    }
}

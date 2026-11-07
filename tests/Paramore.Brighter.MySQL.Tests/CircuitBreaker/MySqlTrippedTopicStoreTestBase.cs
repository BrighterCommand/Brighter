using System;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public abstract class MySqlTrippedTopicStoreTestBase : IAsyncLifetime
{
    private IAmATrippedTopicStore? _store;

    protected string TableName { get; } = $"trips_{Guid.NewGuid():N}";
    protected MySqlConnectionProvider ConnectionProvider { get; } =
        new(new RelationalDatabaseConfiguration(Const.DefaultConnectingString));
    protected IAmATrippedTopicStore Store => _store!;

    public async Task InitializeAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MySqlTrippedTopicStore.GetDDL(TableName);
        await command.ExecuteNonQueryAsync();

        _store = new MySqlTrippedTopicStore(ConnectionProvider, TableName);
    }

    public async Task DisposeAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS `{TableName}`";
        await command.ExecuteNonQueryAsync();
    }
}

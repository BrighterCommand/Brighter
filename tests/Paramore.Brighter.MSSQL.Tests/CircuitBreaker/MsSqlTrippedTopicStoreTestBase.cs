using System;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public abstract class MsSqlTrippedTopicStoreTestBase : IAsyncLifetime
{
    private IAmATrippedTopicStore? _store;

    protected string TableName { get; } = $"trips_{Guid.NewGuid():N}";
    protected MsSqlConnectionProvider ConnectionProvider { get; }
    protected IAmATrippedTopicStore Store => _store!;

    protected MsSqlTrippedTopicStoreTestBase()
    {
        var helper = new MsSqlTestHelper(tableName: TableName);
        ConnectionProvider = new MsSqlConnectionProvider(helper.OutboxConfiguration);
    }

    public async Task InitializeAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MsSqlTrippedTopicStore.GetDDL(TableName);
        await command.ExecuteNonQueryAsync();

        _store = new MsSqlTrippedTopicStore(ConnectionProvider, TableName);
    }

    public async Task DisposeAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS [{TableName}]";
        await command.ExecuteNonQueryAsync();
    }
}

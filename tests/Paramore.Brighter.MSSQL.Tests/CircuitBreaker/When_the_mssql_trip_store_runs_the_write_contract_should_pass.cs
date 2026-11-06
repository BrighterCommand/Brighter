using System;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreWriteContractTests : TrippedTopicStoreWriteContractTest, IAsyncLifetime
{
    private readonly string _tableName = $"trips_{Guid.NewGuid():N}";
    private readonly MsSqlConnectionProvider _connectionProvider;
    private IAmATrippedTopicStore? _store;

    public MsSqlTrippedTopicStoreWriteContractTests()
    {
        var helper = new MsSqlTestHelper(tableName: _tableName);
        _connectionProvider = new MsSqlConnectionProvider(helper.OutboxConfiguration);
    }

    protected override IAmATrippedTopicStore Store => _store!;

    public async Task InitializeAsync()
    {
        // Arrange
        await using var connection = await _connectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MsSqlTrippedTopicStore.GetDDL(_tableName);
        await command.ExecuteNonQueryAsync();

        _store = new MsSqlTrippedTopicStore(_connectionProvider, _tableName);
    }

    public async Task DisposeAsync()
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
        await command.ExecuteNonQueryAsync();
    }
}

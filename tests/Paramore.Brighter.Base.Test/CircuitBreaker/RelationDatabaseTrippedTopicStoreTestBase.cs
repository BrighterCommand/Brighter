using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Base.Test.CircuitBreaker;

public abstract class RelationDatabaseTrippedTopicStoreTestBase : IAsyncLifetime
{
    private IAmATrippedTopicStore? _store;

    protected string TableName { get; } = $"trips_{Guid.NewGuid():N}";
    protected IAmATrippedTopicStore Store => _store!;

    protected abstract IAmARelationalDbConnectionProvider ConnectionProvider { get; }
    protected abstract string CreateTableSql { get; }
    protected abstract string DropTableSql { get; }
    protected abstract IAmATrippedTopicStore CreateStore();

    public async Task InitializeAsync()
    {
        await ExecuteAsync(CreateTableSql);
        _store = CreateStore();
    }

    public async Task DisposeAsync() => await ExecuteAsync(DropTableSql);

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

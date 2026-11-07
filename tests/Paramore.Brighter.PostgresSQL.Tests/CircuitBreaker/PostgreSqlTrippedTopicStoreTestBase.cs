using System;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public abstract class PostgreSqlTrippedTopicStoreTestBase : IAsyncLifetime
{
    private IAmATrippedTopicStore? _store;

    protected string TableName { get; } = $"trips_{Guid.NewGuid():N}";
    protected PostgreSqlConnectionProvider ConnectionProvider { get; }
    protected IAmATrippedTopicStore Store => _store!;

    protected PostgreSqlTrippedTopicStoreTestBase()
    {
        new PostgresSqlTestHelper().SetupDatabase();
        ConnectionProvider = new PostgreSqlConnectionProvider(
            new RelationalDatabaseConfiguration(PostgreSqlSettings.TestsBrighterConnectionString));
    }

    public async Task InitializeAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlTrippedTopicStore.GetDDL(TableName);
        await command.ExecuteNonQueryAsync();

        _store = new PostgreSqlTrippedTopicStore(ConnectionProvider, TableName);
    }

    public async Task DisposeAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {PgIdentifier.Quote(TableName)}";
        await command.ExecuteNonQueryAsync();
    }
}

using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public abstract class PostgreSqlTrippedTopicStoreTestBase : RelationDatabaseTrippedTopicStoreTestBase
{
    protected override PostgreSqlConnectionProvider ConnectionProvider { get; }
    protected override string CreateTableSql => PostgreSqlTrippedTopicStore.GetDDL(TableName);
    protected override string DropTableSql => $"DROP TABLE IF EXISTS {PgIdentifier.Quote(TableName)}";

    protected PostgreSqlTrippedTopicStoreTestBase()
    {
        new PostgresSqlTestHelper().SetupDatabase();
        ConnectionProvider = new PostgreSqlConnectionProvider(
            new RelationalDatabaseConfiguration(PostgreSqlSettings.TestsBrighterConnectionString));
    }

    protected override IAmATrippedTopicStore CreateStore() => new PostgreSqlTrippedTopicStore(ConnectionProvider, TableName);
}

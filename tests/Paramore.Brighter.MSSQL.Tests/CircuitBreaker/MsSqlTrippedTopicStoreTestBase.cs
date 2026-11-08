using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public abstract class MsSqlTrippedTopicStoreTestBase : RelationDatabaseTrippedTopicStoreTestBase
{
    protected override MsSqlConnectionProvider ConnectionProvider { get; }
    protected override string CreateTableSql => MsSqlTrippedTopicStore.GetDDL(TableName);
    protected override string DropTableSql => $"DROP TABLE IF EXISTS [{TableName}]";

    protected MsSqlTrippedTopicStoreTestBase()
    {
        var helper = new MsSqlTestHelper(tableName: TableName);
        ConnectionProvider = new MsSqlConnectionProvider(helper.OutboxConfiguration);
    }

    protected override IAmATrippedTopicStore CreateStore() => new MsSqlTrippedTopicStore(ConnectionProvider, TableName);
}

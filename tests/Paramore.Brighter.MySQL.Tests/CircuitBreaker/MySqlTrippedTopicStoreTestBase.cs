using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public abstract class MySqlTrippedTopicStoreTestBase : RelationDatabaseTrippedTopicStoreTestBase
{
    protected override MySqlConnectionProvider ConnectionProvider { get; } =
        new MySqlConnectionProvider(new RelationalDatabaseConfiguration(Const.DefaultConnectingString));
    protected override string CreateTableSql => MySqlTrippedTopicStore.GetDDL(TableName);
    protected override string DropTableSql => $"DROP TABLE IF EXISTS `{TableName}`";

    protected override IAmATrippedTopicStore CreateStore() => new MySqlTrippedTopicStore(ConnectionProvider, TableName);
}

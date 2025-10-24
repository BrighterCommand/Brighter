using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class SqliteTextOutboxAsyncTest : RelationDatabaseOutboxAsyncTest
{
    protected override string DefaultConnectingString => Tests.Configuration.ConnectionString;
    protected override string TableNamePrefix => Tests.Configuration.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new SqliteOutbox(configuration);
    }

    protected override async Task CreateOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await Tests.Configuration.CreateTableAsync(configuration.ConnectionString, SqliteOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload));
    }

    protected override async Task DeleteOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await Tests.Configuration.DeleteTableAsync(configuration.ConnectionString, configuration.OutBoxTableName);
    }

    protected override IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new SqliteTransactionProvider(Configuration);
    }
}

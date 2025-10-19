using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

public class SqliteTextInboxAsyncTest : RelationalDatabaseInboxAsyncTests
{
    protected override string DefaultConnectingString => Tests.Configuration.ConnectionString;
    protected override string TableNamePrefix => Tests.Configuration.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new SqliteInbox(configuration);
    }

    protected override async Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await Tests.Configuration.CreateTableAsync(configuration.ConnectionString, SqliteInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload));
    }

    protected override async Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await Tests.Configuration.DeleteTableAsync(configuration.ConnectionString, configuration.InBoxTableName);
    }
}

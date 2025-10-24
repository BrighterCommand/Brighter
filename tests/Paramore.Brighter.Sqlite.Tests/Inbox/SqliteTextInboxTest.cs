using Microsoft.Data.Sqlite;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

public class SqliteTextInboxTest : RelationalDatabaseInboxTests
{
    protected override string DefaultConnectingString => Tests.Configuration.ConnectionString;
    protected override string TableNamePrefix => Tests.Configuration.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new SqliteInbox(configuration);
    }

    protected override void CreateInboxTable(RelationalDatabaseConfiguration configuration)
    {
        Tests.Configuration.CreateTable(configuration.ConnectionString, SqliteInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload));
    }

    protected override void DeleteInboxTable(RelationalDatabaseConfiguration configuration)
    {
        Tests.Configuration.DeleteTable(configuration.ConnectionString, configuration.InBoxTableName);
    }
}

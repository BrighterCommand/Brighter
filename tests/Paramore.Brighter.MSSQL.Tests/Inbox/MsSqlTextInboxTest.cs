using Microsoft.Data.SqlClient;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.Inbox;

public class MsSqlTextInboxTest : RelationalDatabaseInboxTests 
{
    protected override string DefaultConnectingString => Tests.Configuration.DefaultConnectingString;
    protected override string TableNamePrefix => Tests.Configuration.TablePrefix;
    protected override bool BinaryMessagePayload => false; 
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new MsSqlInbox(configuration);
    }

    protected override void CreateInboxTable(RelationalDatabaseConfiguration configuration)
    {
        Tests.Configuration.EnsureDatabaseExists(configuration.ConnectionString);
        Tests.Configuration.CreateTable(configuration.ConnectionString, SqlInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload));
    }

    protected override void DeleteInboxTable(RelationalDatabaseConfiguration configuration)
    {
        Tests.Configuration.DeleteTable(configuration.ConnectionString, configuration.InBoxTableName);
    }
}

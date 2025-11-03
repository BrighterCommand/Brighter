using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Inbox;

[Collection("Inbox")]
public class MsSqlTextInboxAsyncTest : RelationalDatabaseInboxAsyncTests 
{
    protected override string DefaultConnectingString => Tests.Configuration.DefaultConnectingString;
    protected override string TableNamePrefix => Tests.Configuration.TablePrefix;
    protected override bool BinaryMessagePayload => false; 
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new MsSqlInbox(configuration);
    }

    protected override async Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await Tests.Configuration.EnsureDatabaseExistsAsync(configuration.ConnectionString);
        await Tests.Configuration.CreateTableAsync(configuration.ConnectionString, SqlInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload));
    }

    protected override async Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await Tests.Configuration.DeleteTableAsync(configuration.ConnectionString, configuration.InBoxTableName);
    }
}

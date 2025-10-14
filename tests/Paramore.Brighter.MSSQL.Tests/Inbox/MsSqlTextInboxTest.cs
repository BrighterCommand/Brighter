using Microsoft.Data.SqlClient;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.Inbox;

public class MsSqlTextInboxTest : RelationalDatabaseInboxTests 
{
    protected override string DefaultConnectingString => Const.DefaultConnectingString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false; 
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new MsSqlInbox(configuration);
    }

    protected override void CreateInboxTable(RelationalDatabaseConfiguration configuration)
    {
        MsSqlTestHelper.EnsureDatabaseExists(configuration.ConnectionString);
        
        using var connection = new SqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SqlInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteInboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new SqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }
}

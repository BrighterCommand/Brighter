using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.Inbox;

public class MsSqlTextInboxAsyncTest : RelationalDatabaseInboxAsyncTests 
{
    protected override string DefaultConnectingString => Const.DefaultConnectingString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false; 
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new MsSqlInbox(configuration);
    }

    protected override async Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await MsSqlTestHelper.EnsureDatabaseExistsAsync(configuration.ConnectionString);
        
        await using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SqlInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}

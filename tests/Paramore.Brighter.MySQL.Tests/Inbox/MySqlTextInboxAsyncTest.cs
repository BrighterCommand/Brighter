using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Inbox;

public class MySqlTextInboxAsyncTest : RelationalDatabaseInboxAsyncTests 
{
    protected override string DefaultConnectingString => "Server=localhost;Uid=root;Pwd=root;Database=BrighterTests";
    protected override string TableNamePrefix => "table_";
    protected override bool BinaryMessagePayload => false; 
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration) 
        => new MySqlInbox(configuration);

    protected override async Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MySqlInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}

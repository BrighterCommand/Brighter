using MySqlConnector;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Inbox;

public class MySqlTextInboxTest : RelationalDatabaseInboxTests 
{
    protected override string DefaultConnectingString => "Server=localhost;Uid=root;Pwd=root;Database=BrighterTests";
    protected override string TableNamePrefix => "table_";
    protected override bool BinaryMessagePayload => false; 
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration) 
        => new MySqlInbox(configuration);

    protected override void CreateInboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new MySqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = MySqlInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteInboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new MySqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }
}

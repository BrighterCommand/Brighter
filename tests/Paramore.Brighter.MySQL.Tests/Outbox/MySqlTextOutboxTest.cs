using System.Collections.Generic;
using MySqlConnector;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

public class MySqlTextOutboxTest : RelationDatabaseOutboxTest
{
    protected override string DefaultConnectingString => "Server=localhost;Uid=root;Pwd=root;Database=BrighterTests";
    protected override string TableNamePrefix => "table_";
    protected override bool BinaryMessagePayload => false; 
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration) 
        => new MySqlOutbox(configuration);

    protected override void CreateOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new MySqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = MySqlOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new MySqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }
}

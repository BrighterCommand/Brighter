using MySqlConnector;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Inbox;

public class MySqlCausationTrackingInboxTest : CausationTrackingInboxBaseTests
{
    private RelationalDatabaseConfiguration _configuration = null!;
    private MySqlInbox _inbox = null!;

    protected override IAmAnInboxSync Inbox => _inbox;

    protected override void BeforeEachTest()
    {
        _configuration = new RelationalDatabaseConfiguration(
            Const.DefaultConnectingString,
            inboxTableName: $"{Const.TablePrefix}{Uuid.New():N}");
        _inbox = new MySqlInbox(_configuration);
        base.BeforeEachTest();
    }

    protected override void CreateStore()
    {
        using var connection = new MySqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = MySqlInboxBuilder.GetDDL(_configuration.InBoxTableName);
        command.ExecuteNonQuery();
    }

    protected override void DeleteStore()
    {
        using var connection = new MySqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }
}

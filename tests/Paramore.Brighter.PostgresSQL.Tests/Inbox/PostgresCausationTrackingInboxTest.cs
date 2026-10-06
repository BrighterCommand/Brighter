using Npgsql;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Postgres;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;

public class PostgresCausationTrackingInboxTest : CausationTrackingInboxBaseTests
{
    private RelationalDatabaseConfiguration _configuration = null!;
    private PostgreSqlInbox _inbox = null!;

    protected override IAmAnInboxSync Inbox => _inbox;

    protected override void BeforeEachTest()
    {
        _configuration = new RelationalDatabaseConfiguration(
            Const.ConnectionString,
            inboxTableName: $"{Const.TablePrefix}{Uuid.New():N}");
        _inbox = new PostgreSqlInbox(_configuration);
        base.BeforeEachTest();
    }

    protected override void CreateStore()
    {
        using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlInboxBuilder.GetDDL(_configuration.InBoxTableName);
        command.ExecuteNonQuery();
    }

    protected override void DeleteStore()
    {
        using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }
}

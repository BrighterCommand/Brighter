using Npgsql;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Postgres;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;

public class PostgresTextInboxTest : RelationalDatabaseInboxTests
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new PostgreSqlInbox(configuration);
    }

    protected override void CreateInboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new NpgsqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteInboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new NpgsqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }
}

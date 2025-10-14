using Npgsql;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

public class PostgresTextOutboxTest : RelationDatabaseOutboxTest
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new PostgreSqlOutbox(configuration);
    }

    protected override void CreateOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new NpgsqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new NpgsqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }
}

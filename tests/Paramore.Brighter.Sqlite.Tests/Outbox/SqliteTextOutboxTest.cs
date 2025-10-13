using Microsoft.Data.Sqlite;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class SqliteTextOutboxTest : RelationDatabaseOutboxTest
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new SqliteOutbox(configuration);
    }

    protected override void CreateOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new SqliteConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SqliteOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new SqliteConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }
}

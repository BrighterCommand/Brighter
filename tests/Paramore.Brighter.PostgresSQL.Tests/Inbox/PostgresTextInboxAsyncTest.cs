using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Postgres;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;

public class PostgresTextInboxAsyncTest : RelationalDatabaseInboxAsyncTests
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new PostgreSqlInbox(configuration);
    }

    protected override async Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new NpgsqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new NpgsqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}

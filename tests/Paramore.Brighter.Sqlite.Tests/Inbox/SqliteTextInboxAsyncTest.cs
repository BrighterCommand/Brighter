using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

public class SqliteTextInboxAsyncTest : RelationalDatabaseInboxAsyncTests
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new SqliteInbox(configuration);
    }

    protected override async Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SqliteInboxBuilder.GetDDL(configuration.InBoxTableName, BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}

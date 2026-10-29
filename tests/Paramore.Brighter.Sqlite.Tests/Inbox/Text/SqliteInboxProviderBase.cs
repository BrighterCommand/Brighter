using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Inbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Inbox.Text;

public abstract class SqliteInboxProviderBase
{
    protected RelationalDatabaseConfiguration Configuration;

    protected SqliteInboxProviderBase(bool binaryMessagePayload)
    {
        Configuration = new RelationalDatabaseConfiguration(Tests.Configuration.ConnectionString,
            databaseName: "brightertests",
            inboxTableName: $"{Tests.Configuration.TablePrefix}{Uuid.New():N}",
            binaryMessagePayload: binaryMessagePayload);
    }

    public void CreateStore()
    {
        using var connection = new SqliteConnection(Configuration.ConnectionString);
        connection.Open();
        using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            walCommand.ExecuteNonQuery();
        }
        using var command = connection.CreateCommand();
        command.CommandText = SqliteInboxBuilder.GetDDL(Configuration.InBoxTableName, Configuration.BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    public void DeleteStore()
    {
        using var connection = new SqliteConnection(Configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {Configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }

    public IAmAnInboxSync CreateInbox()
    {
        return new SqliteInbox(Configuration);
    }

    public async Task CreateStoreAsync()
    {
        await using var connection = new SqliteConnection(Configuration.ConnectionString);
        await connection.OpenAsync();
        await using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            await walCommand.ExecuteNonQueryAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText = SqliteInboxBuilder.GetDDL(Configuration.InBoxTableName, Configuration.BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteStoreAsync()
    {
        await using var connection = new SqliteConnection(Configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {Configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    public IAmAnInboxAsync CreateInboxAsync()
    {
        return new SqliteInbox(Configuration);
    }
}

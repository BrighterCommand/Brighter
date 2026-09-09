using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Inbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Inbox.Text;

public abstract class MySqlInboxProviderBase
{
    protected RelationalDatabaseConfiguration Configuration;

    protected MySqlInboxProviderBase(bool binaryMessagePayload, bool jsonMessagePayload)
    {
        Configuration = new RelationalDatabaseConfiguration(Const.DefaultConnectingString,
            databaseName: "brightertests",
            inboxTableName: $"{Const.TablePrefix}{Uuid.New():N}",
            binaryMessagePayload: binaryMessagePayload,
            jsonMessagePayload: jsonMessagePayload);
    }

    public void CreateStore()
    {
        using var connection = new MySqlConnection(Configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = MySqlInboxBuilder.GetDDL(Configuration.InBoxTableName, Configuration.BinaryMessagePayload, Configuration.JsonMessagePayload);
        command.ExecuteNonQuery();
    }

    public void DeleteStore()
    {
        using var connection = new MySqlConnection(Configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {Configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }

    public IAmAnInboxSync CreateInbox()
    {
        return new MySqlInbox(Configuration);
    }

    public async Task CreateStoreAsync()
    {
        using var connection = new MySqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = MySqlInboxBuilder.GetDDL(Configuration.InBoxTableName, Configuration.BinaryMessagePayload, Configuration.JsonMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteStoreAsync()
    {
        using var connection = new MySqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {Configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    public IAmAnInboxAsync CreateInboxAsync()
    {
        return new MySqlInbox(Configuration);
    }
}

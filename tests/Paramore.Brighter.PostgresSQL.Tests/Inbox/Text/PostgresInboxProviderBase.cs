using Npgsql;
using Paramore.Brighter.Inbox.Postgres;
using System.Threading.Tasks;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox.Text;

public abstract class PostgresInboxProviderBase
{
    protected RelationalDatabaseConfiguration Configuration;

    protected PostgresInboxProviderBase(bool binaryMessagePayload, bool jsonMessagePayload)
    {
        Configuration = new RelationalDatabaseConfiguration(Const.ConnectionString,
            databaseName: "brightertests",
            inboxTableName: $"{Const.TablePrefix}{Uuid.New():N}",
            binaryMessagePayload: binaryMessagePayload,
            jsonMessagePayload: jsonMessagePayload);
    }

    public void CreateStore()
    {
        using var connection = new NpgsqlConnection(Configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlInboxBuilder.GetDDL(Configuration.InBoxTableName, Configuration.BinaryMessagePayload, Configuration.JsonMessagePayload);
        command.ExecuteNonQuery();
    }

    public void DeleteStore()
    {
        using var connection = new NpgsqlConnection(Configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {Configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }

    public IAmAnInboxSync CreateInbox()
    {
        return new PostgreSqlInbox(Configuration);
    }

    public async Task CreateStoreAsync()
    {
        await using var connection = new NpgsqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlInboxBuilder.GetDDL(Configuration.InBoxTableName, Configuration.BinaryMessagePayload, Configuration.JsonMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteStoreAsync()
    {
        await using var connection = new NpgsqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {Configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    public IAmAnInboxAsync CreateInboxAsync()
    {
        return new PostgreSqlInbox(Configuration);
    }
}

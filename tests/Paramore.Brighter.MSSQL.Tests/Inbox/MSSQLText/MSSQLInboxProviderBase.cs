using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.Inbox.MSSQLText;

public abstract class MSSQLInboxProviderBase
{
    protected RelationalDatabaseConfiguration _configuration;

    protected MSSQLInboxProviderBase(bool binaryMessagePayload)
    {
        _configuration = new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString,
            databaseName: "brightertests",
            inboxTableName: $"{Configuration.TablePrefix}{Uuid.New():N}",
            binaryMessagePayload: binaryMessagePayload);
    }

    public IAmAnInboxSync CreateInbox()
    {
        return new MsSqlInbox(_configuration);
    }

    public IAmAnInboxAsync CreateInboxAsync()
    {
        return new MsSqlInbox(_configuration);
    }

    public void CreateStore()
    {
        Configuration.EnsureDatabaseExists(_configuration.ConnectionString);

        using var connection = new SqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SqlInboxBuilder.GetDDL(_configuration.InBoxTableName, _configuration.BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    public async Task CreateStoreAsync()
    {
        await Configuration.EnsureDatabaseExistsAsync(_configuration.ConnectionString);

        await using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SqlInboxBuilder.GetDDL(_configuration.InBoxTableName, _configuration.BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    public void DeleteStore()
    {
        using var connection = new SqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }

    public async Task DeleteStoreAsync()
    {
        await using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}

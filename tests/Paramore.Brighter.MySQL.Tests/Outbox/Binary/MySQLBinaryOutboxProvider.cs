using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.MySql;
using Paramore.Brighter.MySQL.Tests.Outbox.Binary.Async;
using Paramore.Brighter.MySQL.Tests.Outbox.Binary.Sync;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Outbox.Binary;

public class MySQLBinaryOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
    private readonly RelationalDatabaseConfiguration _configuration = new(Const.DefaultConnectingString,
        databaseName: "brightertests",
        outBoxTableName: $"table_{Uuid.New():N}",
        binaryMessagePayload: true);


    public IAmAnOutboxSync<Message, DbTransaction> CreateOutbox()
    {
        return new MySqlOutbox(_configuration);
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync()
    {
        return new MySqlOutbox(_configuration);
    }

    public void CreateStore()
    {
        using var connection = new MySqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = MySqlOutboxBuilder.GetDDL(_configuration.OutBoxTableName, true);
        command.ExecuteNonQuery();
    }

    public async Task CreateStoreAsync()
    {
        using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = MySqlOutboxBuilder.GetDDL(_configuration.OutBoxTableName, true);
        await command.ExecuteNonQueryAsync();
    }

    public IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new MySqlTransactionProvider(_configuration);
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        using var connection = new MySqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    public IEnumerable<Message> GetAllMessages()
    {
        var outbox = new MySqlOutbox(_configuration);
        return outbox.Get(new RequestContext());
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var outbox = new MySqlOutbox(_configuration);
        return await outbox.GetAsync(new RequestContext());
    }
}

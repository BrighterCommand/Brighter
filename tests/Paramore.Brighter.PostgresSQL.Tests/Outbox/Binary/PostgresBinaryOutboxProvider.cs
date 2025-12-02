using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.Outbox.Binary.Async;
using Paramore.Brighter.PostgresSQL.Tests.Outbox.Binary.Sync;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox.Binary;

public class PostgresBinaryOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
    private readonly RelationalDatabaseConfiguration _configuration = new(Const.ConnectionString, $"Table{Uuid.New():N}", binaryMessagePayload: true);
    
    public void CreateStore()
    {
        using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        command.ExecuteNonQuery();
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }

    public IAmAnOutboxSync<Message, DbTransaction> CreateOutbox()
    {
        return new PostgreSqlOutbox(_configuration);
    }

    public IEnumerable<Message> GetAllMessages()
    {
        throw new System.NotImplementedException();
    }

    public IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new PostgreSqlTransactionProvider(_configuration);
    }

    public async Task CreateStoreAsync()
    {
        await using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        await command.ExecuteNonQueryAsync();
    }

    public Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        throw new System.NotImplementedException();
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync()
    {
        throw new System.NotImplementedException();
    }
}

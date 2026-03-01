using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.Outbox.Text.Async;
using Paramore.Brighter.PostgresSQL.Tests.Outbox.Text.Sync;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox.Text;

public class PostgresTextOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
    private readonly RelationalDatabaseConfiguration _configuration = new(Const.ConnectionString, 
        databaseName: "brightertests",
        outBoxTableName: $"Table{Uuid.New():N}",
        binaryMessagePayload: false);
    
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
        var outbox = new PostgreSqlOutbox(_configuration);
        return outbox.Get(new RequestContext());
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var outbox = new PostgreSqlOutbox(_configuration);
        return await outbox.GetAsync(new RequestContext());
    }

    public IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new PostgreSqlTransactionProvider(_configuration);
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync()
    {
        return new PostgreSqlOutbox(_configuration);
    }
    
    public async Task CreateStoreAsync()
    {
        await using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        await using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}

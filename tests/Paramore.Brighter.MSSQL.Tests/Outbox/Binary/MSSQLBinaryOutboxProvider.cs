using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MSSQL.Tests.Outbox.Binary.Async;
using Paramore.Brighter.MSSQL.Tests.Outbox.Binary.Sync;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.Outbox.Binary;

public class MSSQLTextOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
    private readonly RelationalDatabaseConfiguration _configuration = new(Configuration.DefaultConnectingString,
        databaseName: "brightertests",
        outBoxTableName: $"Table{Uuid.New():N}",
        binaryMessagePayload: true);

    public IAmAnOutboxSync<Message, DbTransaction> CreateOutbox()
    {
        return new MsSqlOutbox(_configuration);
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync()
    {
        return new MsSqlOutbox(_configuration);
    }

    public void CreateStore()
    {
        using var connection = new SqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SqlOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        command.ExecuteNonQuery();
    }

    public async Task CreateStoreAsync()
    {
        await using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SqlOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        await command.ExecuteNonQueryAsync();
    }

    public IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new MsSqlTransactionProvider(_configuration);
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        using var connection = new SqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        await using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    public IEnumerable<Message> GetAllMessages()
    {
        var outbox = new MsSqlOutbox(_configuration);
        return outbox.Get(new RequestContext());
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var outbox = new MsSqlOutbox(_configuration);
        return await outbox.GetAsync(new RequestContext());
    }
}

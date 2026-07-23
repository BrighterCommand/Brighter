using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Paramore.Brighter.Oracle.Tests.Outbox.Text.Async;
using Paramore.Brighter.Oracle.Tests.Outbox.Text.Sync;
using Paramore.Brighter.Outbox.Oracle;

namespace Paramore.Brighter.Oracle.Tests.Outbox.Text;

public class OracleTextOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
    private readonly RelationalDatabaseConfiguration _configuration = new(
        Const.DefaultConnectingString,
        outBoxTableName: $"{Const.TablePrefix}{Uuid.New():N}",
        binaryMessagePayload: false);

    public IAmAnOutboxSync<Message, DbTransaction> CreateOutbox()
    {
        return new OracleOutbox(_configuration);
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync()
    {
        return new OracleOutbox(_configuration);
    }

    public void CreateStore()
    {
        using var connection = new OracleConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = OracleOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        command.ExecuteNonQuery();
    }

    public async Task CreateStoreAsync()
    {
        await using var connection = new OracleConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = OracleOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        await command.ExecuteNonQueryAsync();
    }

    public IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new OracleTransactionProvider(_configuration);
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        using var connection = new OracleConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        await using var connection = new OracleConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    public IEnumerable<Message> GetAllMessages()
    {
        var outbox = new OracleOutbox(_configuration);
        return outbox.Get(new RequestContext());
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var outbox = new OracleOutbox(_configuration);
        return await outbox.GetAsync(new RequestContext());
    }
}

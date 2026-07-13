using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Paramore.Brighter.Oracle;
using Paramore.Brighter.Oracle.Tests.Outbox.Binary.Async;
using Paramore.Brighter.Oracle.Tests.Outbox.Binary.Sync;
using Paramore.Brighter.Outbox.Oracle;

namespace Paramore.Brighter.Oracle.Tests.Outbox.Binary;

public class OracleBinaryOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
    private readonly RelationalDatabaseConfiguration _configuration = new(
        Const.DefaultConnectingString,
        outBoxTableName: $"{Const.TablePrefix}{Uuid.New():N}",
        binaryMessagePayload: true);

    private readonly OracleTransactionProvider _transactionProvider;
    private readonly OracleOutbox _outbox;

    public OracleBinaryOutboxProvider()
    {
        _transactionProvider = new OracleTransactionProvider(_configuration);
        _outbox = new OracleOutbox(_configuration, _transactionProvider);
    }

    public IAmAnOutboxSync<Message, DbTransaction> CreateOutbox()
    {
        return _outbox;
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync()
    {
        return _outbox;
    }

    public void CreateStore()
    {
        using var connection = new OracleConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = OracleOutboxBuilder.GetDDL(_configuration.OutBoxTableName, true);
        command.ExecuteNonQuery();
    }

    public async Task CreateStoreAsync()
    {
        await using var connection = new OracleConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = OracleOutboxBuilder.GetDDL(_configuration.OutBoxTableName, true);
        await command.ExecuteNonQueryAsync();
    }

    public IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return _transactionProvider;
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

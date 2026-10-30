using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Sqlite.Tests.Outbox.Binary.Async;
using Paramore.Brighter.Sqlite.Tests.Outbox.Binary.OutstandingCount;
using Paramore.Brighter.Sqlite.Tests.Outbox.Binary.Sync;

namespace Paramore.Brighter.Sqlite.Tests.Outbox.Binary;

public class SqliteBinaryOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync, IAmAnOutstandingCountOutboxProvider
{
    private readonly RelationalDatabaseConfiguration _configuration = new(Configuration.ConnectionString,
        databaseName: "brightertests",
        outBoxTableName: $"table_{Uuid.New():N}",
        binaryMessagePayload: true);


    public IAmAnOutboxSync<Message, DbTransaction> CreateOutbox()
    {
        return new SqliteOutbox(_configuration);
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync()
    {
        return new SqliteOutbox(_configuration);
    }

    public void CreateStore()
    {
        using var connection = new SqliteConnection(_configuration.ConnectionString);
        connection.Open();
        using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            walCommand.ExecuteNonQuery();
        }
        using var command = connection.CreateCommand();
        command.CommandText = SqliteOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        command.ExecuteNonQuery();
    }

    public async Task CreateStoreAsync()
    {
        using var connection = new SqliteConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            await walCommand.ExecuteNonQueryAsync();
        }
        using var command = connection.CreateCommand();
        command.CommandText = SqliteOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        await command.ExecuteNonQueryAsync();
    }

    public IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new SqliteTransactionProvider(_configuration);
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        using var connection = new SqliteConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        await using var connection = new SqliteConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    public IEnumerable<Message> GetAllMessages()
    {
        var outbox = new SqliteOutbox(_configuration);
        return outbox.Get(new RequestContext());
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var outbox = new SqliteOutbox(_configuration);
        return await outbox.GetAsync(new RequestContext());
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutstandingCountStore()
    {
        CreateStore();
        return new SqliteOutbox(_configuration);
    }

    public void DeleteOutstandingCountStore()
    {
        try
        {
            DeleteStore(Array.Empty<Message>());
        }
        catch (Exception)
        {
        }
    }

    public Task<OutstandingCountCancellationSetup<DbTransaction>> PrepareCancellationAsync(TimeSpan holdFor) =>
        HeldSqliteOutstandingCountStore.PrepareAsync(binaryMessagePayload: true, holdFor);
}

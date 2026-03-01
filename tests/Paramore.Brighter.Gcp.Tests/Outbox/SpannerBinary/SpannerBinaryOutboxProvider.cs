using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Gcp.Tests.Outbox.SpannerBinary.Async;
using Paramore.Brighter.Gcp.Tests.Outbox.SpannerBinary.Sync;
using Paramore.Brighter.Gcp.Tests.Spanner;
using Paramore.Brighter.Outbox.Spanner;
using Paramore.Brighter.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Outbox.SpannerBinary;

public class SpannerBinaryOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
    private readonly RelationalDatabaseConfiguration _configuration = new(
        Const.ConnectionString,
        databaseName: "brightertests",
        outBoxTableName: $"test_{Uuid.New():N}",
        binaryMessagePayload: true
    );

    public IAmAnOutboxSync<Message, DbTransaction> CreateOutbox()
    {
        return new SpannerOutbox(_configuration);
    }

    public IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync()
    {
        return new SpannerOutbox(_configuration);
    }

    public void CreateStore()
    {
        using var connection = new SpannerConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SpannerOutboxBuilder.GetDDL(_configuration.OutBoxTableName, true);
        command.ExecuteNonQuery();
    }

    public async Task CreateStoreAsync()
    {
        using var connection = new SpannerConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = SpannerOutboxBuilder.GetDDL(_configuration.OutBoxTableName, true);
        await command.ExecuteNonQueryAsync();
    }

    public IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new SpannerUnitOfWork(_configuration);
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        using var connection = new SpannerConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        await using var connection = new SpannerConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    public IEnumerable<Message> GetAllMessages()
    {
        var outbox = new SpannerOutbox(_configuration);
        return outbox.Get(new RequestContext());
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var outbox = new SpannerOutbox(_configuration);
        return await outbox.GetAsync(new RequestContext());
    }
}

using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Gcp.Tests.Inbox.Spanner.Async;
using Paramore.Brighter.Gcp.Tests.Inbox.Spanner.Sync;
using Paramore.Brighter.Gcp.Tests.Spanner;
using Paramore.Brighter.Inbox.Spanner;
using Paramore.Brighter.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Inbox.Spanner;

public class SpannerInboxProvider : IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    private readonly RelationalDatabaseConfiguration _configuration = new(
        Const.ConnectionString,
        databaseName: "brightertests",
        inboxTableName: $"{Const.TablePrefix}{Uuid.New():N}",
        binaryMessagePayload: false
    );

    public IAmAnInboxSync CreateInbox()
    {
        return new SpannerInboxAsync(_configuration);
    }

    public IAmAnInboxAsync CreateInboxAsync()
    {
        return new SpannerInboxAsync(_configuration);
    }

    public void CreateStore()
    {
        using var connection = new SpannerConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SpannerInboxBuilder.GetDDL(_configuration.InBoxTableName);
        command.ExecuteNonQuery();
    }

    public async Task CreateStoreAsync()
    {
        await using var connection = new SpannerConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SpannerInboxBuilder.GetDDL(_configuration.InBoxTableName);
        await command.ExecuteNonQueryAsync();
    }

    public void DeleteStore()
    {
        using var connection = new SpannerConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }

    public async Task DeleteStoreAsync()
    {
        await using var connection = new SpannerConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}

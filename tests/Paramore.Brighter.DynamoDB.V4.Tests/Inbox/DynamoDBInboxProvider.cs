using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Paramore.Brighter.DynamoDB.V4.Tests.Inbox.DynamoDB.Async;
using Paramore.Brighter.DynamoDB.V4.Tests.Inbox.DynamoDB.Sync;
using Paramore.Brighter.Inbox.DynamoDB.V4;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Inbox.DynamoDB;

public class DynamoDBInboxProvider : IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    private string _tableName = "";

    public IAmAnInboxSync CreateInbox()
    {
        _tableName = DynamoDbInboxTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient)
            .GetAwaiter()
            .GetResult();

        return new DynamoDbInbox(Const.DynamoDbClient,
            new DynamoDbInboxConfiguration { TableName = _tableName });
    }

    public IAmAnInboxAsync CreateInboxAsync()
    {
        _tableName = DynamoDbInboxTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient)
            .GetAwaiter()
            .GetResult();

        return new DynamoDbInbox(Const.DynamoDbClient,
            new DynamoDbInboxConfiguration { TableName = _tableName });
    }

    public void CreateStore()
    {
        _tableName = DynamoDbInboxTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient)
            .GetAwaiter()
            .GetResult();
    }

    public async Task CreateStoreAsync()
    {
        _tableName = await DynamoDbInboxTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient);
    }

    public void DeleteStore()
    {
        DeleteStoreAsync().GetAwaiter().GetResult();
    }

    public async Task DeleteStoreAsync()
    {
        var client = Const.DynamoDbClient;
        try
        {
            await client.DeleteTableAsync(_tableName);
        }
        catch
        {
            // Ignoring any error during delete, it's not important at this point
        }

        DynamoDbInboxTable.Reset();
    }
}

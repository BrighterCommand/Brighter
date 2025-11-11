using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.Inbox;

public class DynamoDBInboxAsyncTest : InboxAsyncTest
{
    private DynamoDbInbox? _inbox;
    protected override IAmAnInboxAsync Inbox => _inbox!;

    protected override async Task CreateStoreAsync()
    {
        var tableName = await DynamoDbInboxTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient);

        _inbox = new DynamoDbInbox(Const.DynamoDbClient,
            new DynamoDbInboxConfiguration { TableName = tableName });
    }
}

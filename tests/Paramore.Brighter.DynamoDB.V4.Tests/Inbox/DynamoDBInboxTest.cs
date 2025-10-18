using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.DynamoDB.V4;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Inbox;

public class DynamoDBInboxTest : InboxTests
{
    private DynamoDbInbox? _inbox;
    protected override IAmAnInboxSync Inbox => _inbox!;

    protected override void CreateStore()
    {
        var tableName = DynamoDbInboxTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient)
            .GetAwaiter()
            .GetResult();

        _inbox = new DynamoDbInbox(Const.DynamoDbClient,
            new DynamoDbInboxConfiguration { TableName = tableName });
    }
}

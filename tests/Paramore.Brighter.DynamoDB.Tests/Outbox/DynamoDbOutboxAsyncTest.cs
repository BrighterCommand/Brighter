using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

public class DynamoDbOutboxAsyncTest : OutboxAsyncTest<TransactWriteItemsRequest>
{
    private string? _tableName;
    private DynamoDbOutbox? _outbox;
    protected override IAmAnOutboxAsync<Message, TransactWriteItemsRequest> Outbox => _outbox!;

    protected override async Task CreateStoreAsync()
    {
        _tableName = await DynamoDbOutboxTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient);

        _outbox = new DynamoDbOutbox(Const.DynamoDbClient,
            new DynamoDbConfiguration  { TableName = _tableName });
    }

    protected override async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var messages =  new List<Message>();
        var client = Const.DynamoDbClient;
        Dictionary<string,AttributeValue>? lastKey =  null;
        do
        {
            var scan = client.ScanAsync(new ScanRequest
                {
                    TableName = _tableName,
                    AttributesToGet = { nameof(MessageItem.MessageId) },
                    ExclusiveStartKey = lastKey,
                    Select = Select.SPECIFIC_ATTRIBUTES
                })
                .GetAwaiter()
                .GetResult();

            lastKey = scan.LastEvaluatedKey;

            var ids = scan.Items.Select(x => Id.Create(x[nameof(MessageItem.MessageId)].S));
             messages.AddRange(await Outbox.GetAsync(ids, new RequestContext()));
        } while (lastKey != null && lastKey.Keys.Count != 0);

        return messages;
    }

    protected override IAmABoxTransactionProvider<TransactWriteItemsRequest> CreateTransactionProvider()
    {
        return new DynamoDbUnitOfWork(Const.DynamoDbClient);
    }

    protected override async Task DeleteStoreAsync()
    {
        var client =  Const.DynamoDbClient;
        foreach (var message in CreatedMessages)
        {
            try
            {
                await client.DeleteItemAsync(new DeleteItemRequest(_tableName,
                    new Dictionary<string, AttributeValue>
                    {
                        [nameof(MessageItem.MessageId)] = new() { S = message.Id }
                    }));  
            }
            catch 
            {
                // Ignoring any error
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Outbox.DynamoDB.V4;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

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
        var messages = new List<Message>();
        var client = Const.DynamoDbClient;
        Dictionary<string, AttributeValue>? lastKey = null;
        do
        {
            var request  = new ScanRequest();
            request.TableName = _tableName;
            request.ExclusiveStartKey = lastKey;
            request.Select = Select.SPECIFIC_ATTRIBUTES;
            request.AttributesToGet = [nameof(MessageItem.MessageId)];
                
            var scan = await client.ScanAsync(request);

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

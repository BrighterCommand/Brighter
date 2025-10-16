using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

public class DynamoDbOutboxTest : OutboxTest<TransactWriteItemsRequest>
{
    private string? _tableName;
    private DynamoDbOutbox? _outbox;
    protected override IAmAnOutboxSync<Message, TransactWriteItemsRequest> Outbox => _outbox!;

    protected override void CreateStore()
    {
        _tableName = DynamoDbOutboxTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient)
            .GetAwaiter()
            .GetResult();

        _outbox = new DynamoDbOutbox(Const.DynamoDbClient,
            new DynamoDbConfiguration  { TableName = _tableName });
    }

    protected override IEnumerable<Message> GetAllMessages()
    {
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
            var messages = Outbox.Get(ids, new RequestContext());
            foreach (var message in messages)
            {
                yield return message;
            }
        } while (lastKey != null && lastKey.Keys.Count != 0);
    }

    protected override IAmABoxTransactionProvider<TransactWriteItemsRequest> CreateTransactionProvider()
    {
        return new DynamoDbUnitOfWork(Const.DynamoDbClient);
    }

    // protected override void DeleteStore()
    // {
    //     var client =  Const.DynamoDbClient;
    //     foreach (var message in CreatedMessages)
    //     {
    //         try
    //         {
    //             client.DeleteItemAsync(new DeleteItemRequest(_tableName,
    //                 new Dictionary<string, AttributeValue>
    //                 {
    //                     [nameof(MessageItem.MessageId)] = new() { S = message.Id }
    //                 })).GetAwaiter().GetResult();
    //         }
    //         catch 
    //         {
    //             // Ignoring any error
    //         }
    //     }
    // }
}

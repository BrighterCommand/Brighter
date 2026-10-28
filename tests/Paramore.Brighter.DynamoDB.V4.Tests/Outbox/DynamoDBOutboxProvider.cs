using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.DynamoDB.V4.Tests.Outbox.Async;
using Paramore.Brighter.DynamoDB.V4.Tests.Outbox.Sync;
using Paramore.Brighter.DynamoDB.V4.Tests.Outbox.OutstandingCount;
using Paramore.Brighter.Outbox.DynamoDB.V4;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

public class DynamoDBOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync, IAmAnOutstandingCountOutboxProvider
{
    private string _tableName = "";
    private readonly List<string> _outstandingCountTables = [];

    public IAmAnOutboxSync<Message, TransactWriteItemsRequest> CreateOutbox()
    {
        _tableName = DynamoDbOutboxTable
            .EnsureTableIsCreatedAsync(Const.DynamoDbClient)
            .GetAwaiter()
            .GetResult();

        return new DynamoDbOutbox(
            Const.DynamoDbClient,
            new DynamoDbConfiguration(_tableName, timeout: Const.OUTBOX_TIMEOUT_IN_MILLISECONDS)
        );
    }

    public IAmAnOutboxAsync<Message, TransactWriteItemsRequest> CreateOutboxAsync()
    {
        _tableName = DynamoDbOutboxTable
            .EnsureTableIsCreatedAsync(Const.DynamoDbClient)
            .GetAwaiter()
            .GetResult();

        return new DynamoDbOutbox(
            Const.DynamoDbClient,
            new DynamoDbConfiguration(_tableName, timeout: Const.OUTBOX_TIMEOUT_IN_MILLISECONDS)
        );
    }

    public void CreateStore() { }

    public Task CreateStoreAsync()
    {
        return Task.CompletedTask;
    }

    public IAmABoxTransactionProvider<TransactWriteItemsRequest> CreateTransactionProvider()
    {
        return new DynamoDbUnitOfWork(Const.DynamoDbClient);
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        DeleteStoreAsync(messages).GetAwaiter().GetResult();
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        var client = Const.DynamoDbClient;
        foreach (var message in messages)
        {
            try
            {
                await client.DeleteItemAsync(
                    new DeleteItemRequest(
                        _tableName,
                        new Dictionary<string, AttributeValue>
                        {
                            [nameof(MessageItem.MessageId)] = new() { S = message.Id },
                        }
                    )
                );
            }
            catch
            {
                // Ignoring any error during delete, it's not important at this point
            }
        }
    }

    public IEnumerable<Message> GetAllMessages()
    {
        return GetAllMessagesAsync().GetAwaiter().GetResult();
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var receivedMessages = new List<Message>();

        var outbox = new DynamoDbOutbox(
            Const.DynamoDbClient,
            new DynamoDbConfiguration(_tableName, timeout: Const.OUTBOX_TIMEOUT_IN_MILLISECONDS)
        );

        var client = Const.DynamoDbClient;
        Dictionary<string, AttributeValue>? lastKey = null;
        do
        {
            var scan = await client.ScanAsync(
                new ScanRequest
                {
                    TableName = _tableName,
                    AttributesToGet = [nameof(MessageItem.MessageId)],
                    ExclusiveStartKey = lastKey,
                    Select = Select.SPECIFIC_ATTRIBUTES,
                }
            );

            lastKey = scan.LastEvaluatedKey;

            var ids = scan.Items.Select(x => Id.Create(x[nameof(MessageItem.MessageId)].S));
            var messages = await outbox.GetAsync(ids, new RequestContext());
            foreach (var message in messages)
            {
                receivedMessages.Add(message);
            }
        } while (lastKey != null && lastKey.Keys.Count != 0);

        return receivedMessages;
    }

    public void DeleteStore()
    {
        foreach (var tableName in _outstandingCountTables)
        {
            DynamoDbOutboxTable.DeleteTableAsync(Const.DynamoDbClient, tableName).GetAwaiter().GetResult();
        }

        _outstandingCountTables.Clear();
    }

    IAmAnOutboxAsync<Message, TransactWriteItemsRequest> IAmAnOutstandingCountOutboxProvider.CreateOutbox()
    {
        return CreateOutstandingCountOutbox(Const.OUTBOX_TIMEOUT_IN_MILLISECONDS);
    }

    public Task<OutstandingCountCancellationSetup<TransactWriteItemsRequest>> PrepareCancellationAsync(TimeSpan holdFor)
    {
        var outbox = CreateOutstandingCountOutbox(timeout: 0);

        return Task.FromResult(new OutstandingCountCancellationSetup<TransactWriteItemsRequest>(
            outbox,
            storeHeld: false,
            clientCancellationException: typeof(OperationCanceledException),
            release: () => ValueTask.CompletedTask));
    }

    private DynamoDbOutbox CreateOutstandingCountOutbox(int timeout)
    {
        var tableName = DynamoDbOutboxTable.CreateTableAsync(Const.DynamoDbClient).GetAwaiter().GetResult();
        _outstandingCountTables.Add(tableName);

        return new DynamoDbOutbox(Const.DynamoDbClient, new DynamoDbConfiguration(tableName, timeout: timeout));
    }
}

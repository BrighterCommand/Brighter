using System;
using System.Collections.Generic;
using System.Net;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

public class DynamoDbOutboxTransactionTests : DynamoDBOutboxBaseTest
{
    private readonly DynamoDbOutbox _dynamoDbOutbox;
    private readonly string _entityTableName;

    public DynamoDbOutboxTransactionTests()
    {
        var tableRequestFactory = new DynamoDbTableFactory();

        //act
        CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<MyEntity>(
            new DynamoDbCreateProvisionedThroughput
            (
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                new Dictionary<string, ProvisionedThroughput>
                {
                    { "GlobalSecondaryIndex", new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 } }
                }
            )
        );

        _entityTableName = tableRequest.TableName;
        (bool exist, IEnumerable<string> tables) hasTables = DbTableBuilder.HasTables(new string[] { _entityTableName }).Result;
        if (!hasTables.exist)
        {
            var buildTable = DbTableBuilder.Build(tableRequest).Result;
            DbTableBuilder.EnsureTablesReady(new[] { tableRequest.TableName }, TableStatus.ACTIVE).Wait();
        }

        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(Credentials, RegionEndpoint.EUWest1, OutboxTableName));
    }

    [Fact]
    public async void When_There_Is_A_Transaction_Between_Outbox_And_Entity()
    {
        var context = new DynamoDBContext(Client);
        var myItem = new MyEntity { Id = Guid.NewGuid().ToString(), Value = "Test Value for Transaction Checking" };
        var attributes = context.ToDocument(myItem).ToAttributeMap();
        var myMessageHeader = new MessageHeader(
            messageId: Guid.NewGuid(),
            topic: "test_topic",
            messageType: MessageType.MT_DOCUMENT,
            timeStamp: DateTime.UtcNow.AddDays(-1),
            handledCount: 5,
            delayedMilliseconds: 5,
            correlationId: Guid.NewGuid(),
            replyTo: "ReplyAddress",
            contentType: "text/plain");

        var body = new MessageBody(myItem.Value);
        var myMessage = new MessageItem(new Message(myMessageHeader, body));
        var messageAttributes = context.ToDocument(myMessage).ToAttributeMap();

        var uow = new DynamoDbUnitOfWork(Client);
        TransactWriteItemsResponse response = null;
        try
        {
            var transaction = uow.BeginOrGetTransaction();
            transaction.TransactItems.Add(new TransactWriteItem { Put = new Put { TableName = _entityTableName, Item = attributes, } });
            transaction.TransactItems.Add(new TransactWriteItem { Put = new Put { TableName = OutboxTableName, Item = messageAttributes}});

            response = await uow.CommitAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        Assert.NotNull(response); 
        Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
        Assert.Equal(2, response.ContentLength);    //number of tables in the transaction
    }
}

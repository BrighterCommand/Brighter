using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

public class DynamoDbOutboxTransactionTests : DynamoDBOutboxBaseTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly DynamoDbOutbox _dynamoDbOutbox;
    private readonly string _entityTableName;
    private readonly Dictionary<string, AttributeValue?> _entityAttributes;
    private readonly Message _message;

    public DynamoDbOutboxTransactionTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var tableRequestFactory = new DynamoDbTableFactory();
        var fakeTimeProvider = new FakeTimeProvider();

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

        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName), fakeTimeProvider);

        var context = new DynamoDBContext(Client);
        var myItem = new MyEntity { Id = Guid.NewGuid().ToString(), Value = "Test Value for Transaction Checking" };
        _entityAttributes = context.ToDocument(myItem).ToAttributeMap();

        var myMessageHeader = new MessageHeader(
            messageId: Guid.NewGuid().ToString(),
            topic: new RoutingKey("test_topic"),
            messageType: MessageType.MT_DOCUMENT,
            timeStamp: DateTime.UtcNow.AddDays(-1),
            handledCount: 5,
            delayed: TimeSpan.FromMilliseconds(5),
            correlationId: Guid.NewGuid().ToString(),
            replyTo: new RoutingKey("ReplyAddress"),
            contentType: "text/plain");
        var body = new MessageBody(myItem.Value);
        _message = new Message(myMessageHeader, body);
    }

    [Fact]
    public void When_There_Is_A_Transaction_Between_Outbox_And_Entity()
    {
        var uow = new DynamoDbUnitOfWork(Client);
        TransactWriteItemsResponse response;
        try
        {
            _dynamoDbOutbox.Add(_message, new RequestContext(), transactionProvider: uow);

            var transaction = uow.GetTransaction();
            transaction.TransactItems.Add(new TransactWriteItem { Put = new Put { TableName = _entityTableName, Item = _entityAttributes, } });

            transaction.TransactItems.Count.Should().Be(2);

            uow.Commit();
            response = uow.LastResponse;
        }
        catch (Exception e)
        {
            _testOutputHelper.WriteLine(e.ToString());
            throw;
        }

        response.Should().NotBeNull();
        response.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        response.ContentLength.Should().Be(2); //number of tables in the transaction 
    }

    [Fact]
    public async Task When_There_Is_A_Transaction_Between_Outbox_And_Entity_Async()
    {
        var uow = new DynamoDbUnitOfWork(Client);
        TransactWriteItemsResponse response;
        try
        {
            await _dynamoDbOutbox.AddAsync(_message, new RequestContext(), transactionProvider: uow);

            var transaction = await uow.GetTransactionAsync();
            transaction.TransactItems.Add(new TransactWriteItem { Put = new Put { TableName = _entityTableName, Item = _entityAttributes, } });

            transaction.TransactItems.Count.Should().Be(2);

            await uow.CommitAsync();
            response = uow.LastResponse;
        }
        catch (Exception e)
        {
            _testOutputHelper.WriteLine(e.ToString());
            throw;
        }

        response.Should().NotBeNull();
        response.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        response.ContentLength.Should().Be(2); //number of tables in the transaction 
    }
}

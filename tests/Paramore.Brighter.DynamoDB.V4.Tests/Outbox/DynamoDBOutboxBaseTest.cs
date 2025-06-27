using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;
using Paramore.Brighter.Outbox.DynamoDB.V4;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

public class DynamoDBOutboxBaseTest : IDisposable
{
    private bool _disposed;
    protected DynamoDbTableBuilder DbTableBuilder { get; }
    protected string OutboxTableName { get; }

    protected AWSCredentials Credentials { get; set; }

    protected IAmazonDynamoDB Client { get; }

    protected DynamoDBOutboxBaseTest ()
    {
        Client = CreateClient();
        DbTableBuilder = new DynamoDbTableBuilder(Client);
        //create a table request
        var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                new Dictionary<string, ProvisionedThroughput>
                {
                    {"Outstanding", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}},
                    {"Delivered", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}}
                }
            ));
        OutboxTableName = createTableRequest.TableName;
        (bool exist, IEnumerable<string> tables) hasTables = DbTableBuilder.HasTables(new string[] {OutboxTableName}).Result;
        if (!hasTables.exist)
        {
            var buildTable = DbTableBuilder.Build(createTableRequest).Result;
            DbTableBuilder.EnsureTablesReady(new[] {createTableRequest.TableName}, TableStatus.ACTIVE).Wait();
        }
    }

    private IAmazonDynamoDB CreateClient()
    {
        Credentials = new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");

        var clientConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:8000"
        };

        return new AmazonDynamoDBClient(Credentials, clientConfig);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~DynamoDBOutboxBaseTest()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // free other managed objects that implement
            // IDisposable only
        }

        var tableNames = new[] {OutboxTableName};
        //var deleteTables =_dynamoDbTableBuilder.Delete(tableNames).Result;
        // _dynamoDbTableBuilder.EnsureTablesDeleted(tableNames).Wait();

        _disposed = true;
    }
}
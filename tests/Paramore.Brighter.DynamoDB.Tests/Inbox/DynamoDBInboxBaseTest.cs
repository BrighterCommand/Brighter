using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Paramore.Brighter.DynamoDb.Extensions;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.Inbox
{
    public abstract class DynamoDBInboxBaseTest : IDisposable
    {
        private bool _disposed;
        private DynamoDbTableBuilder _dynamoDbTableBuilder;
        protected AWSCredentials Credentials { get; private set; }
        protected string TableName { get; }
        public IAmazonDynamoDB Client { get; }

        protected DynamoDBInboxBaseTest()
        {
            //required by AWS 2.2
            Environment.SetEnvironmentVariable("AWS_ENABLE_ENDPOINT_DISCOVERY", "false");
            
            Client = CreateClient();
            _dynamoDbTableBuilder = new DynamoDbTableBuilder(Client);
            //create a table request
            var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableMapper<CommandItem<MyCommand>>(
                new DynamoDbCreateProvisionedThroughput(
                    new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                    new Dictionary<string, ProvisionedThroughput>()
                ));
            TableName = createTableRequest.TableName;
            (bool exist, IEnumerable<string> tables) hasTables = _dynamoDbTableBuilder.HasTables(new string[] {TableName}).Result;
            if (!hasTables.exist)
            {
                var buildTable = _dynamoDbTableBuilder.Build(createTableRequest).Result;
                _dynamoDbTableBuilder.EnsureTablesReady(new[] {createTableRequest.TableName}, TableStatus.ACTIVE).Wait();
            }
        }

        private IAmazonDynamoDB CreateClient()
        {
            Credentials = new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");

            var clientConfig = new AmazonDynamoDBConfig();
            clientConfig.ServiceURL = "http://localhost:8000";

            return new AmazonDynamoDBClient(Credentials, clientConfig);
 
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DynamoDBInboxBaseTest()
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

            var tableNames = new string[] {TableName};
            //var deleteTables =_dynamoDbTableBuilder.Delete(tableNames).Result;
           // _dynamoDbTableBuilder.EnsureTablesDeleted(tableNames).Wait();
 
            _disposed = true;
       }
    }
}

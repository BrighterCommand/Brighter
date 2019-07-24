using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Newtonsoft.Json;
using Paramore.Brighter.DynamoDb.Extensions;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.Outbox.DynamoDB;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Tests.Inbox.DynamoDB
{
    public abstract class DynamoDBInboxBaseTest : IDisposable
    {
        private DynamoDbTableBuilder _dynamoDbTableBuilder;
        protected string TableName { get; }
        protected AWSCredentials Credentials { get; private set; }

        protected DynamoDBInboxBaseTest()
        {
            var client = CreateClient();
            _dynamoDbTableBuilder = new DynamoDbTableBuilder(client);
            //create a table request
            var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableMapper<CommandItem<MyCommand>>(
                new DynamoDbCreateProvisionedThroughput(
                    new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                    new Dictionary<string, ProvisionedThroughput>()
                ));
            TableName = createTableRequest.TableName;
            (bool missing, IEnumerable<string> tables) hasTables = _dynamoDbTableBuilder.HasTables(new string[] {TableName}).Result;
            if (hasTables.missing)
            {
                var buildTable = _dynamoDbTableBuilder.Build(createTableRequest).Result;
                _dynamoDbTableBuilder.EnsureTablesReady(new[] {createTableRequest.TableName}, TableStatus.ACTIVE).Wait();
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
            var tableNames = new string[] {TableName};
            var deleteTables =_dynamoDbTableBuilder.Delete(tableNames).Result;
            _dynamoDbTableBuilder.EnsureTablesDeleted(tableNames).Wait();
        }
    }
}

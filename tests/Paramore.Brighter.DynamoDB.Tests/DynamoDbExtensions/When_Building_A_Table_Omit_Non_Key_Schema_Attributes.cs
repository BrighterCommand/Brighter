using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.DynamoDbExtensions
{
    public class DynamoDbDropNonKeyAttributesForCreationTests
    {
        [Test]
        public async Task When_Building_A_Table_Omit_Non_Key_Schema_Attributes()
        {
            var tableRequestFactory = new DynamoDbTableFactory();
            var builder = new DynamoDbTableBuilder(CreateClient());

            //act
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(
                new DynamoDbCreateProvisionedThroughput
                (
                    new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                    new Dictionary<string, ProvisionedThroughput>
                    {
                        {
                            "GlobalSecondaryIndex", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}
                        }
                    }
                )
            );

            var modifiedTableRequest = builder.RemoveNonSchemaAttributes(tableRequest);

            //assert
            await Assert.That(modifiedTableRequest.AttributeDefinitions).DoesNotContain(attr => attr.AttributeName == "StringProperty" && attr.AttributeType == ScalarAttributeType.S);
            await Assert.That(modifiedTableRequest.AttributeDefinitions).DoesNotContain(attr => attr.AttributeName == "NumberProperty" && attr.AttributeType == ScalarAttributeType.N);
            await Assert.That(modifiedTableRequest.AttributeDefinitions).DoesNotContain(attr => attr.AttributeName == "ByteArrayProperty" && attr.AttributeType == ScalarAttributeType.B);
            await Assert.That(tableRequest.AttributeDefinitions).Contains(attr => attr.AttributeName == "Id" && attr.AttributeType == ScalarAttributeType.S);
            await Assert.That(tableRequest.AttributeDefinitions).Contains(attr => attr.AttributeName == "RangeKey" && attr.AttributeType == ScalarAttributeType.S);
            await Assert.That(tableRequest.AttributeDefinitions).Contains(attr => attr.AttributeName == "GlobalSecondaryId" && attr.AttributeType == ScalarAttributeType.S);
            await Assert.That(tableRequest.AttributeDefinitions).Contains(attr => attr.AttributeName == "GlobalSecondaryRangeKey" && attr.AttributeType == ScalarAttributeType.S);
            await Assert.That(tableRequest.AttributeDefinitions).Contains(attr => attr.AttributeName == "LocalSecondaryRangeKey" && attr.AttributeType == ScalarAttributeType.S);
        }

        private AmazonDynamoDBClient CreateClient()
        {
            var credentials = new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");

            var clientConfig = new AmazonDynamoDBConfig();
            clientConfig.ServiceURL = "http://localhost:8000";

            return new AmazonDynamoDBClient(credentials, clientConfig);
        }

        [DynamoDBTable("MyEntity")]
        private sealed class DynamoDbEntity
        {
            [DynamoDBProperty]
            public string StringProperty { get; set; }

            [DynamoDBProperty]
            public int NumberProperty { get; set; }

            [DynamoDBProperty]
            public byte[] ByteArrayProperty { get; set; }

            [DynamoDBHashKey]
            [DynamoDBProperty]
            public string Id { get; set; }

            [DynamoDBRangeKey]
            [DynamoDBProperty]
            public string RangeKey { get; set; }

            [DynamoDBGlobalSecondaryIndexHashKey("GlobalSecondaryIndex")]
            [DynamoDBProperty]
            public string GlobalSecondaryId { get; set; }

            [DynamoDBGlobalSecondaryIndexRangeKey("GlobalSecondaryIndex")]
            [DynamoDBProperty]
            public string GlobalSecondaryRangeKey { get; set; }

            [DynamoDBLocalSecondaryIndexRangeKey(indexName:"LocalSecondaryIndex")]
            [DynamoDBProperty]
            public string LocalSecondaryRangeKey { get; set; }
        }
    }
}

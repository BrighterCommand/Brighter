using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.DynamoDbExtensions
{
    public class DynamboDbFactoryMultipleGSIIndexesTests
    {
        [Fact]
        public void When_Creating_A_Table_With_Multiple_GSI_Inxdexes_Per_Field()
        {
            //arrange
            var tableRequestFactory = new DynamoDbTableFactory();

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

            //assert
            Assert.Equal("MyEntity", tableRequest.TableName);
            Assert.Contains(tableRequest.GlobalSecondaryIndexes,
                gsi => gsi.IndexName == "GlobalSecondaryIndex"
                       && Enumerable.Any<KeySchemaElement>(gsi.KeySchema, kse => kse.AttributeName == "GlobalSecondaryId" && kse.KeyType == KeyType.HASH)
                       && Enumerable.Any<KeySchemaElement>(gsi.KeySchema, kse => kse.AttributeName == "GlobalSecondaryRangeKey" && kse.KeyType == KeyType.RANGE));
            Assert.Contains(tableRequest.GlobalSecondaryIndexes,
                gsi => gsi.IndexName == "AnotherGlobalSecondaryIndex"
                       && Enumerable.Any<KeySchemaElement>(gsi.KeySchema, kse => kse.AttributeName == "GlobalSecondaryId" && kse.KeyType == KeyType.HASH)
                       && Enumerable.Any<KeySchemaElement>(gsi.KeySchema, kse => kse.AttributeName == "GlobalSecondaryRangeKey" && kse.KeyType == KeyType.RANGE));
        }

        //Required
        [DynamoDBTable("MyEntity")]
        private class DynamoDbEntity
        {
            [DynamoDBHashKey]
            public string Id { get; set; }

            [DynamoDBRangeKey]
            public string RangeKey { get; set; }

            [DynamoDBGlobalSecondaryIndexHashKey("GlobalSecondaryIndex", "AnotherGlobalSecondaryIndex")]
            public string GlobalSecondaryId { get; set; }

            [DynamoDBGlobalSecondaryIndexRangeKey("GlobalSecondaryIndex", "AnotherGlobalSecondaryIndex")]
            public string GlobalSecondaryRangeKey { get; set; }
        }
    }
}

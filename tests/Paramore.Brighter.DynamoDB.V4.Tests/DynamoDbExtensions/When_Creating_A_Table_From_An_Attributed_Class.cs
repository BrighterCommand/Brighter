using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.V4.Tests.DynamoDbExtensions;

public class DynamboDbFactoryGenerateCreateRequestTests
{
    [Test]
    public async Task When_Creating_A_Table_From_An_Attributed_Class()
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
        await Assert.That(tableRequest.TableName).IsEqualTo("MyEntity");
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "StringProperty" && attr.AttributeType == ScalarAttributeType.S)).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "NumberProperty" && attr.AttributeType == ScalarAttributeType.N)).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "ByteArrayProperty" && attr.AttributeType == ScalarAttributeType.B)).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "UnmarkedProperty")).IsFalse();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "MappedName" && attr.AttributeType == ScalarAttributeType.S)).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "IgnoredProperty")).IsFalse();
        await Assert.That((tableRequest.KeySchema).Any(kse => kse.AttributeName == "Id" && kse.KeyType == KeyType.HASH)).IsTrue();
        await Assert.That((tableRequest.GlobalSecondaryIndexes).Any(
            gsi => gsi.IndexName == "GlobalSecondaryIndex"
                   && Enumerable.Any<KeySchemaElement>(gsi.KeySchema, kse => kse.AttributeName == "GlobalSecondaryId" && kse.KeyType == KeyType.HASH)
                   && Enumerable.Any<KeySchemaElement>(gsi.KeySchema, kse => kse.AttributeName == "GlobalSecondaryRangeKey" && kse.KeyType == KeyType.RANGE))).IsTrue();
        await Assert.That((tableRequest.LocalSecondaryIndexes).Any(lsi => lsi.IndexName == "LocalSecondaryIndex"
                                                                    && Enumerable.Any<KeySchemaElement>(lsi.KeySchema, kse => kse.AttributeName == "LocalSecondaryRangeKey" && kse.KeyType == KeyType.RANGE))).IsTrue();
    }

    //Required
    [DynamoDBTable("MyEntity")]
    private sealed class DynamoDbEntity
    {
        [DynamoDBProperty]
        public string StringProperty { get; set; }

        [DynamoDBProperty]
        public int NumberProperty { get; set; }

        [DynamoDBProperty]
        public byte[] ByteArrayProperty { get; set; }

        //We only issue create table statements for explicitly marked fields; others can still be persisted though
        public string UnMarkedProperty { get; set; }

        [DynamoDBProperty("MappedName")]
        public string RenamedProperty { get; set; }

        [DynamoDBIgnore]
        public string IgnoredProperty { get; set; }

        //Required
        [DynamoDBHashKey]
        public string Id { get; set; }

        [DynamoDBRangeKey]
        public string RangeKey { get; set; }

        [DynamoDBVersion]
        public int? Version { get; set; }

        [DynamoDBGlobalSecondaryIndexHashKey("GlobalSecondaryIndex")]
        public string GlobalSecondaryId { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GlobalSecondaryIndex")]
        public string GlobalSecondaryRangeKey { get; set; }

        [DynamoDBLocalSecondaryIndexRangeKey(indexName:"LocalSecondaryIndex")]
        public string LocalSecondaryRangeKey { get; set; }
    }
}
